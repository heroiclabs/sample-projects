package main

import (
	"context"
	"database/sql"
	"errors"
	"fmt"
	"math/rand"
	"path/filepath"
	"sync"
	"time"

	osruntime "runtime"

	"github.com/heroiclabs/hiro"
	"github.com/heroiclabs/nakama-common/api"
	"github.com/heroiclabs/nakama-common/runtime"
)

var (
	usernameOverrideMutex  = &sync.Mutex{}
	usernameOverrideRandom = rand.New(rand.NewSource(time.Now().UTC().UnixNano()))
)

func InitModule(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, initializer runtime.Initializer) error {
	// Register username overrides.
	if err := initializer.RegisterBeforeAuthenticateApple(func(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, in *api.AuthenticateAppleRequest) (*api.AuthenticateAppleRequest, error) {
		in.Username = usernameOverrideFn(in.Username)
		return in, nil
	}); err != nil {
		return nil
	}
	if err := initializer.RegisterBeforeAuthenticateCustom(func(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, in *api.AuthenticateCustomRequest) (*api.AuthenticateCustomRequest, error) {
		in.Username = usernameOverrideFn(in.Username)
		return in, nil
	}); err != nil {
		return nil
	}
	if err := initializer.RegisterBeforeAuthenticateDevice(func(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, in *api.AuthenticateDeviceRequest) (*api.AuthenticateDeviceRequest, error) {
		in.Username = usernameOverrideFn(in.Username)
		return in, nil
	}); err != nil {
		return nil
	}
	if err := initializer.RegisterBeforeAuthenticateEmail(func(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, in *api.AuthenticateEmailRequest) (*api.AuthenticateEmailRequest, error) {
		in.Username = usernameOverrideFn(in.Username)
		return in, nil
	}); err != nil {
		return nil
	}
	if err := initializer.RegisterBeforeAuthenticateFacebook(func(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, in *api.AuthenticateFacebookRequest) (*api.AuthenticateFacebookRequest, error) {
		in.Username = usernameOverrideFn(in.Username)
		return in, nil
	}); err != nil {
		return nil
	}
	if err := initializer.RegisterBeforeAuthenticateFacebookInstantGame(func(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, in *api.AuthenticateFacebookInstantGameRequest) (*api.AuthenticateFacebookInstantGameRequest, error) {
		in.Username = usernameOverrideFn(in.Username)
		return in, nil
	}); err != nil {
		return nil
	}
	if err := initializer.RegisterBeforeAuthenticateGameCenter(func(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, in *api.AuthenticateGameCenterRequest) (*api.AuthenticateGameCenterRequest, error) {
		in.Username = usernameOverrideFn(in.Username)
		return in, nil
	}); err != nil {
		return nil
	}
	if err := initializer.RegisterBeforeAuthenticateGoogle(func(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, in *api.AuthenticateGoogleRequest) (*api.AuthenticateGoogleRequest, error) {
		in.Username = usernameOverrideFn(in.Username)
		return in, nil
	}); err != nil {
		return nil
	}
	if err := initializer.RegisterBeforeAuthenticateSteam(func(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, in *api.AuthenticateSteamRequest) (*api.AuthenticateSteamRequest, error) {
		in.Username = usernameOverrideFn(in.Username)
		return in, nil
	}); err != nil {
		return nil
	}

	if err := nk.LeaderboardCreate(ctx, "weekly_leaderboard", false, "desc", "best",
		"0 0 * * 1", map[string]interface{}{}, true); err != nil {
		// Handle error.
	}

	if err := nk.LeaderboardCreate(ctx, "global_leaderboard", false, "desc", "best",
		"", map[string]interface{}{}, true); err != nil {
		// Handle error.
	}

	err := createTournament(ctx, logger, nk, "daily-dash", "Daily Dash", "Dash past your opponents for high scores and big rewards!", false)
	if err != nil {
		// Handle error.
	}

	err = createTournament(ctx, logger, nk, "limited-dash", "Limited Dash", "Limited spaces available, join now!", true)
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
		hiro.WithChallengesSystem(fmt.Sprintf("definitions/%s/base-challenges.json", env), true))
	if err != nil {
		return err
	}
	_ = systems

	logger.Info("Module loaded in %dms", time.Since(initStart).Milliseconds())

	return nil
}

func createTournament(ctx context.Context, logger runtime.Logger, nk runtime.NakamaModule, id, title, description string, joinRequired bool) error {
	authoritative := false        // true by default
	sortOrder := "desc"           // one of: "desc", "asc"
	operator := "best"            // one of: "best", "set", "incr"
	resetSchedule := "0 12 * * *" // noon UTC each day
	metadata := map[string]interface{}{}
	category := 1
	startTime := int(time.Now().UTC().Unix()) // start now
	endTime := 0                              // never end, repeat the tournament each day forever
	duration := 86400                         // in seconds
	maxSize := 10000                          // first 10,000 players who join
	maxNumScore := 3                          // each player can have 3 attempts to score
	enableRanks := false                      // ranks are disabled
	err := nk.TournamentCreate(ctx, id, authoritative, sortOrder, operator, resetSchedule, metadata, title, description, category, startTime, endTime, duration, maxSize, maxNumScore, joinRequired, enableRanks)
	if err != nil {
		logger.Debug("unable to create tournament: %q", err.Error())
		return runtime.NewError("failed to create tournament", 3)
	}
	return nil
}

var usernameOverrideFn = func(_ string) string {
	usernameOverrideMutex.Lock()
	number := usernameOverrideRandom.Intn(100_000_000)
	usernameOverrideMutex.Unlock()
	return fmt.Sprintf("Player%08d", number)
}
