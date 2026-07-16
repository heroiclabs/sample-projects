// This file demonstrates how to integrate Hiro's Economy and Achievements systems
// in a Nakama server plugin to build an XP-based player progression system.
//
// Flow: a client calls rpc_grant_xp → a reward containing the XP currency is
// rolled and granted via the Economy reward APIs → the publisher detects the
// currencyGranted event and advances the player's level achievements.
package main

import (
	"context"
	"database/sql"
	"encoding/json"
	"errors"
	"fmt"
	"path/filepath"
	"strconv"
	"time"

	osruntime "runtime"

	"github.com/heroiclabs/hiro"
	"github.com/heroiclabs/nakama-common/runtime"
)

// grantXPRequest is the JSON payload the client sends when calling rpc_grant_xp.
type grantXPRequest struct {
	// Amount is the base XP to grant. Active economy modifiers (e.g. a double-XP
	// weekend) may cause the actual amount added to the wallet to differ.
	Amount int64 `json:"amount"`
}

// XPLevelPublisher advances a player's level achievements in response to the
// currencyGranted events Hiro emits when XP is granted.
type XPLevelPublisher struct {
	achievements hiro.AchievementsSystem
}

// Compile-time assertion to ensure that XPLevelPublisher implements hiro.Publisher.
var _ hiro.Publisher = (*XPLevelPublisher)(nil)

// InitModule is the Nakama plugin entry point. It runs once on server startup
// and is responsible for initialising Hiro systems and registering all RPCs.
func InitModule(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, initializer runtime.Initializer) error {
	initStart := time.Now()

	// Read runtime environment variables defined in local.yml (or your deployment config).
	props, ok := ctx.Value(runtime.RUNTIME_CTX_ENV).(map[string]string)
	if !ok {
		return errors.New("invalid context runtime env")
	}

	// ENV controls which definitions folder is loaded (e.g. "dev1", "prod").
	// This lets you maintain separate economy/achievement configs per environment.
	env, ok := props["ENV"]
	if !ok || env == "" {
		return errors.New("'ENV' key missing or invalid in env")
	}
	logger.Info("Using env named %q", env)

	hiroLicense, ok := props["HIRO_LICENSE"]
	if !ok || hiroLicense == "" {
		return errors.New("'HIRO_LICENSE' key missing or invalid in env")
	}

	// Hiro ships as a pre-compiled binary. Select the correct one for the
	// platform this server is running on.
	binName := "hiro.bin"
	switch osruntime.GOOS {
	case "darwin":
		switch osruntime.GOARCH {
		case "arm64":
			binName = "hiro-darwin-arm64.bin"
		}
	case "linux":
		switch osruntime.GOARCH {
		case "arm64":
			binName = "hiro-linux-arm64.bin"
		}
	}
	binPath := filepath.Join("lib", binName)
	logger.Info("binPath set as %q", binPath)

	// Initialise Hiro with the systems this guide uses.
	// Each system points at a JSON definition file that describes its configuration.
	systems, err := hiro.Init(ctx, logger, nk, initializer, binPath, hiroLicense,
		hiro.WithBaseSystem(fmt.Sprintf("definitions/%s/base-system.json", env), true),
		hiro.WithEconomySystem(fmt.Sprintf("definitions/%s/base-economy.json", env), true),
		hiro.WithInventorySystem(fmt.Sprintf("definitions/%s/base-inventory.json", env), false),
		hiro.WithAchievementsSystem(fmt.Sprintf("definitions/%s/base-achievements.json", env), true))
	if err != nil {
		return err
	}

	// Register the XP level publisher. Hiro calls Send on every registered publisher
	// when a system event occurs. XPLevelPublisher listens for currencyGranted events
	// on the "xp" currency and advances the player's level achievements accordingly.
	systems.AddPublisher(&XPLevelPublisher{achievements: systems.GetAchievementsSystem()})

	if err := initializer.RegisterRpc("rpc_grant_xp", rpcGrantXP(systems)); err != nil {
		return err
	}

	if err := initializer.RegisterRpc("rpc_reset_data", rpcResetData(systems)); err != nil {
		return err
	}

	logger.Info("Module loaded in %dms", time.Since(initStart).Milliseconds())
	return nil
}

// The Publisher interface in Hiro requires two methods: Send and Authenticate.
// This publisher doesn't need to act on authentication, so it's a no-op.
func (p *XPLevelPublisher) Authenticate(_ context.Context, _ runtime.Logger, _ runtime.NakamaModule, _ string, _ bool) {
}

// The publisher listens for currencyGranted event on the "xp" currency and advances
// the player's level achievements by the actual granted amount.
//
// Because Hiro emits currencyGranted with the post-modifier value (reward
// multipliers are applied inside batch.apply before the event is constructed),
// no wallet snapshot is needed. The event Value is always the real delta.
func (p *XPLevelPublisher) Send(ctx context.Context, logger runtime.Logger, nk runtime.NakamaModule, userID string, events []*hiro.PublisherEvent) {
	for _, event := range events {
		if event.Name != "currencyGranted" || event.Metadata["currencyId"] != "xp" {
			continue
		}
		xp, err := strconv.ParseInt(event.Value, 10, 64)
		if err != nil || xp <= 0 {
			continue
		}
		if err := advanceLevelsFromXP(ctx, logger, nk, p.achievements, userID, xp); err != nil {
			logger.WithField("error", err.Error()).Error("advanceLevelsFromXP failed")
		}
	}
}

// Advances the player's level sub-achievements by xp points.
//
// Levels are modelled as sub-achievements inside the "player_levels" group.
// Each sub-achievement has a max_count representing the XP required to complete
// that level. XP is applied in order, capping each level at its max_count so
// overflow carries into the next level.
func advanceLevelsFromXP(ctx context.Context, logger runtime.Logger, nk runtime.NakamaModule, achievements hiro.AchievementsSystem, userID string, xp int64) error {
	achMap, _, err := achievements.GetAchievements(ctx, logger, nk, userID)
	if err != nil {
		return err
	}

	playerLevels, ok := achMap["player_levels"]
	if !ok {
		return errors.New("player_levels achievement not found")
	}

	// Build a single batch of level updates to apply in one database call.
	//
	// Levels are keyed "level_1", "level_2", ... so we walk them in order by
	// constructing each key directly rather than sorting the map. A missing key
	// marks the end of the defined levels (or a malformed config).
	//
	// XP can overflow across levels (e.g. a large grant may complete level 3 and
	// partially fill level 4). Hiro evaluates preconditions sequentially within a
	// single UpdateAchievements call, so levels that become eligible mid-batch are
	// handled correctly.
	updates := make(map[string]int64)
	remaining := xp
	for i := 1; remaining > 0; i++ {
		levelID := "level_" + strconv.Itoa(i)
		sub, ok := playerLevels.SubAchievements[levelID]
		if !ok {
			break
		}
		if sub.Count >= sub.MaxCount {
			continue
		}
		toApply := remaining
		if needed := sub.MaxCount - sub.Count; toApply > needed {
			toApply = needed
		}
		updates[levelID] = toApply
		remaining -= toApply
	}

	if len(updates) > 0 {
		if _, _, err := achievements.UpdateAchievements(ctx, logger, nk, userID, updates); err != nil {
			return err
		}
	}

	return nil
}

// rpcGrantXP grants XP to the calling player. Level progression is handled
// automatically by the XPLevelPublisher, which intercepts the currencyGranted
// event emitted by Economy.Grant.
func rpcGrantXP(systems hiro.Hiro) func(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	return func(ctx context.Context, logger runtime.Logger, _ *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
		userID, ok := ctx.Value(runtime.RUNTIME_CTX_USER_ID).(string)
		if !ok {
			return "", errors.New("no user ID in context")
		}

		var req grantXPRequest
		if err := json.Unmarshal([]byte(payload), &req); err != nil {
			return "", err
		}

		if req.Amount <= 0 {
			return "", runtime.NewError("amount must be greater than 0", 3)
		}

		// Roll and grant the XP as an Economy reward. Even though the amount is
		// fixed, going through RewardRoll is what applies the player's active
		// reward modifiers (e.g. a double-XP booster) to the granted amount.
		// RewardGrant only deposits the already-rolled contents into the wallet.
		econ := systems.GetEconomySystem()
		rewardConfig := econ.RewardCreate()
		rewardConfig.Guaranteed = &hiro.EconomyConfigRewardContents{
			Currencies: map[string]*hiro.EconomyConfigRewardCurrency{
				"xp": {EconomyConfigRewardRangeInt64: hiro.EconomyConfigRewardRangeInt64{Min: req.Amount, Max: req.Amount}},
			},
		}

		reward, err := econ.RewardRoll(ctx, logger, nk, userID, rewardConfig)
		if err != nil {
			return "", err
		}

		if _, _, _, err := econ.RewardGrant(ctx, logger, nk, userID, reward, nil, false); err != nil {
			return "", err
		}

		return "{}", nil
	}
}

// Internal helper to clear the calling player's progression and wallet to a clean state.
// It clears all achievement progress, reward modifiers and sets all currencies to 0.
func rpcResetData(systems hiro.Hiro) func(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	return func(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
		userID, ok := ctx.Value(runtime.RUNTIME_CTX_USER_ID).(string)
		if !ok {
			return "", errors.New("no user ID in context")
		}

		// Clear achievement progress for both the level progression and quest groups.
		if _, _, err := systems.GetAchievementsSystem().ResetAchievements(ctx, logger, nk, userID, []string{"player_levels", "defeat_thunder_world"}); err != nil {
			return "", err
		}

		// Read the current wallet so we can calculate exact negative deltas.
		account, err := nk.AccountGetId(ctx, userID)
		if err != nil {
			return "", err
		}
		wallet, err := systems.GetEconomySystem().UnmarshalWallet(account)
		if err != nil {
			return "", err
		}

		// Build a changeset that brings xp, gems, and coins to zero.
		changeset := make(map[string]int64)
		for _, key := range []string{"xp", "gems", "coins"} {
			if balance := wallet[key]; balance != 0 {
				changeset[key] = -balance
			}
		}
		if len(changeset) > 0 {
			if _, _, err := nk.WalletUpdate(ctx, userID, changeset, nil, false); err != nil {
				return "", err
			}
		}

		// Delete the reward modifier storage object so active boosters are cleared.
		if err := nk.StorageDelete(ctx, []*runtime.StorageDelete{
			{Collection: "economy", Key: "reward_modifiers", UserID: userID},
		}); err != nil {
			return "", err
		}

		return "{}", nil
	}
}
