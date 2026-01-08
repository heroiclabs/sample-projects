package main

import (
	"context"
	"database/sql"
	"errors"
	"fmt"
	"path/filepath"
	"time"

	osruntime "runtime"

	"github.com/heroiclabs/hiro"
	"github.com/heroiclabs/nakama-common/runtime"
	"google.golang.org/protobuf/encoding/protojson"
)

func InitModule(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, initializer runtime.Initializer) error {
	err := createTournament(ctx, logger, nk, "daily-dash", "0 12 * * *", "Daily Dash", "Dash past your opponents for high scores and big rewards!", 86400, 0, 1, false)
	if err != nil {
		// Handle error.
	}

	err = createTournament(ctx, logger, nk, "limited-dash", "0 * * * *", "Limited Dash", "Limited spaces available, join now!", 3600, 10000, 3, true)
	if err != nil {
		// Handle error.
	}

	initStart := time.Now()

	props, ok := ctx.Value(runtime.RUNTIME_CTX_ENV).(map[string]string)
	if !ok {
		return errors.New("invalid context runtime env")
	}

	env, ok := props["ENV"]
	if !ok || env == "" {
		return errors.New("'ENV' key missing or invalid in env")
	}
	logger.Info("Using env named %q", env)

	hiroLicense, ok := props["HIRO_LICENSE"]
	if !ok || hiroLicense == "" {
		return errors.New("'HIRO_LICENSE' key missing or invalid in env")
	}

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

	systems, err := hiro.Init(ctx, logger, nk, initializer, binPath, hiroLicense,
		hiro.WithBaseSystem(fmt.Sprintf("definitions/%s/base-system.json", env), true),
		hiro.WithLeaderboardsSystem(fmt.Sprintf("definitions/%s/base-leaderboards.json", env), true),
		hiro.WithChallengesSystem(fmt.Sprintf("definitions/%s/base-challenges.json", env), true),
		hiro.WithEconomySystem(fmt.Sprintf("definitions/%s/base-economy.json", env), true),
		hiro.WithEventLeaderboardsSystem(fmt.Sprintf("definitions/%s/base-event-leaderboards.json", env), true),
		hiro.WithTeamsSystem(fmt.Sprintf("definitions/%s/base-teams.json", env), true),
		hiro.WithInventorySystem(fmt.Sprintf("definitions/%s/base-inventory.json", env), true))
	if err != nil {
		return err
	}

	// Activity calculator for Teams
	// For simplicity, member count is a proxy for team activity - more members = more active team
	if teamsSystem := systems.GetTeamsSystem(); teamsSystem != nil {
		teamsSystem.SetActivityCalculator(calculateTeamActivity)
		logger.Info("Teams activity calculator registered")
	}

	// Unregister the default team stats update RPC and replace with custom version that triggers achievements
	if err := hiro.UnregisterRpc(initializer, hiro.RpcId_RPC_ID_TEAMS_STATS_UPDATE); err != nil {
		return err
	}
	if err := initializer.RegisterRpc(
		hiro.RpcId_RPC_ID_TEAMS_STATS_UPDATE.String(),
		rpcTeamStatsUpdateWithMailboxRewardGrant(systems),
	); err != nil {
		return err
	}
	logger.Info("Custom team stats update RPC registered with achievement triggers")

	logger.Info("Module loaded in %dms", time.Since(initStart).Milliseconds())

	return nil
}

func calculateTeamActivity(ctx context.Context, logger runtime.Logger, nk runtime.NakamaModule, team *hiro.Team) int64 {
	if team == nil {
		return 0
	}

	return int64(len(team.Members))
}

func createTournament(ctx context.Context, logger runtime.Logger, nk runtime.NakamaModule, id, resetSchedule, title, description string, duration, maxSize, maxNumScore int, joinRequired bool) error {
	authoritative := false // true by default
	sortOrder := "desc"    // one of: "desc", "asc"
	operator := "best"     // one of: "best", "set", "incr"
	metadata := map[string]interface{}{}
	category := 1
	startTime := int(time.Now().UTC().Unix()) // start now
	endTime := 0                              // never end, repeat the tournament forever
	enableRanks := true                       // ranks are enabled
	err := nk.TournamentCreate(ctx, id, authoritative, sortOrder, operator, resetSchedule, metadata, title, description, category, startTime, endTime, duration, maxSize, maxNumScore, joinRequired, enableRanks)
	if err != nil {
		logger.Debug("unable to create tournament: %q", err.Error())
		return runtime.NewError("failed to create tournament", 3)
	}
	return nil
}

func rpcTeamStatsUpdateWithMailboxRewardGrant(systems hiro.Hiro) func(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	return func(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
		userID, ok := ctx.Value(runtime.RUNTIME_CTX_USER_ID).(string)
		if !ok {
			return "", errors.New("no user ID in context")
		}

		request := &hiro.TeamStatUpdateRequest{}
		if err := protojson.Unmarshal([]byte(payload), request); err != nil {
			return "", err
		}

		teamsSystem := systems.GetTeamsSystem()

		// Call the actual stats update
		statList, err := teamsSystem.StatsUpdate(ctx, logger, nk, userID, request.Id, request.Public, request.Private)
		if err != nil {
			return "", err
		}

		// Grant rewards to mailbox when level milestones are hit
		if levelStat, ok := statList.Public["level"]; ok {
			var reward *hiro.Reward

			switch levelStat.Value {
			case 2:
				reward = &hiro.Reward{Currencies: map[string]int64{"team_coins": 50}}
			case 5:
				reward = &hiro.Reward{Currencies: map[string]int64{"team_coins": 100}}
			case 10:
				reward = &hiro.Reward{Currencies: map[string]int64{"team_coins": 200}}
			}

			if reward != nil {
				_, err := teamsSystem.RewardMailboxGrant(ctx, logger, nk, userID, request.Id, reward)
				if err != nil {
					logger.Warn("Failed to grant level milestone reward: %v", err)
				}
			}
		}

		response, err := protojson.Marshal(statList)
		if err != nil {
			return "", err
		}

		return string(response), nil
	}
}
