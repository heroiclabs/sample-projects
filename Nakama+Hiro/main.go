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
	if err := nk.LeaderboardCreate(ctx, "weekly_leaderboard", false, "desc", "best",
		"0 0 * * 1", map[string]interface{}{}, true); err != nil {
		// Handle error.
	}

	if err := nk.LeaderboardCreate(ctx, "global_leaderboard", false, "desc", "best",
		"", map[string]interface{}{}, true); err != nil {
		// Handle error.
	}

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
		hiro.WithChallengesSystem(fmt.Sprintf("definitions/%s/base-challenges.json", env), true),
		hiro.WithEconomySystem(fmt.Sprintf("definitions/%s/base-economy.json", env), true),
		hiro.WithInventorySystem(fmt.Sprintf("definitions/%s/base-inventory.json", env), true))
	if err != nil {
		return err
	}
	_ = systems

	logger.Info("Module loaded in %dms", time.Since(initStart).Milliseconds())

	return nil
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
