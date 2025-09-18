package main

import (
	"context"
	"database/sql"
	"fmt"
	"github.com/heroiclabs/nakama-common/api"
	"math/rand"
	"sync"
	"time"

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
