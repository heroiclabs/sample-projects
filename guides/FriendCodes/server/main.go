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

	if err := initializer.RegisterRpc("generate_friend_code", RpcGenerateFriendCode); err != nil {
		return err
	}
	if err := initializer.RegisterRpc("redeem_friend_code", RpcRedeemFriendCode); err != nil {
		return err
	}

	return nil
}

var usernameOverrideFn = func(_ string) string {
	usernameOverrideMutex.Lock()
	number := usernameOverrideRandom.Intn(100_000_000)
	usernameOverrideMutex.Unlock()
	return fmt.Sprintf("Player%08d", number)
}
