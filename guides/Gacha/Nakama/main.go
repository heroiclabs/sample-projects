package main

import (
	"context"
	"database/sql"
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"time"

	osruntime "runtime"

	"github.com/heroiclabs/hiro"
	"github.com/heroiclabs/nakama-common/runtime"
)

func InitModule(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, initializer runtime.Initializer) error {
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

	hiroLicense := os.Getenv("HIRO_LICENSE")
	if hiroLicense == "" {
		hiroLicense = props["HIRO_LICENSE"]
	}
	if hiroLicense == "" {
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
		hiro.WithEconomySystem(fmt.Sprintf("definitions/%s/base-economy.json", env), true),
		hiro.WithInventorySystem(fmt.Sprintf("definitions/%s/base-inventory.json", env), true),
		hiro.WithStatsSystem(fmt.Sprintf("definitions/%s/base-stats.json", env), true))
	if err != nil {
		return err
	}

	// Make sure that users can't update their stats directly to prevent cheating.
	if err = hiro.UnregisterRpc(initializer,
		hiro.RpcId_RPC_ID_STATS_UPDATE,
	); err != nil {
		return err
	}

	// Run our custom log when an inventory item is consumed. (i.e. "pulling" a gacha ticket)
	systems.GetInventorySystem().SetOnConsumeReward(OnConsumeReward(
		systems.GetEconomySystem(), systems.GetInventorySystem(), systems.GetStatsSystem()))

	logger.Info("Module loaded in %dms", time.Since(initStart).Milliseconds())

	return nil
}

func OnConsumeReward(economySystem hiro.EconomySystem, inventorySystem hiro.InventorySystem, statsSystem hiro.StatsSystem) func(ctx context.Context, logger runtime.Logger, nk runtime.NakamaModule, userID, sourceID string, source *hiro.InventoryConfigItem, rewardConfig *hiro.EconomyConfigReward, reward *hiro.Reward) (*hiro.Reward, error) {
	return func(ctx context.Context, logger runtime.Logger, nk runtime.NakamaModule, userID, sourceID string, source *hiro.InventoryConfigItem, rewardConfig *hiro.EconomyConfigReward, reward *hiro.Reward) (*hiro.Reward, error) {
		// Gacha logic is separated into gacha.go
		return handleGachaConsumeReward(ctx, logger, nk, economySystem, inventorySystem, statsSystem, userID, sourceID, source, reward)
	}
}
