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

	// The store is part of the Economy system. The Inventory system holds the
	// items players receive when they purchase a store item, and the Base system
	// provides the shared configuration the others build on.
	systems, err := hiro.Init(ctx, logger, nk, initializer, binPath, hiroLicense,
		hiro.WithBaseSystem(fmt.Sprintf("definitions/%s/base-system.json", env), true),
		hiro.WithEconomySystem(fmt.Sprintf("definitions/%s/base-economy.json", env), true),
		hiro.WithInventorySystem(fmt.Sprintf("definitions/%s/base-inventory.json", env), true))
	if err != nil {
		return err
	}

	// Satori personalization: merges Satori feature flag values (e.g. "Hiro-Economy")
	// onto the base configs per player, enabling audience-based offers and live events
	// in the store. Requires the Satori integration to be configured on the Nakama instance.
	// PublishAuthenticateEvents also creates the Satori identity (as the Nakama user ID)
	// on login, so players are targetable before they send any client-side events.
	systems.AddPersonalizer(hiro.NewSatoriPersonalizer(ctx,
		hiro.SatoriPersonalizerPublishAuthenticateEvents(),
		hiro.SatoriPersonalizerPublishEconomyEvents(),
	))
	logger.Info("Satori personalizer registered")

	logger.Info("Module loaded in %dms", time.Since(initStart).Milliseconds())

	return nil
}
