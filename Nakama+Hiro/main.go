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
		hiro.WithTeamsSystem(fmt.Sprintf("definitions/%s/base-teams.json", env), true))
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
		rpcTeamStatsUpdateWithAchievements(systems),
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

// rpcTeamStatsUpdateWithAchievements returns a custom RPC that updates team stats and triggers achievements
func rpcTeamStatsUpdateWithAchievements(systems hiro.Hiro) func(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	return func(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
		userID, ok := ctx.Value(runtime.RUNTIME_CTX_USER_ID).(string)
		if !ok {
			return "", errors.New("no user ID in context")
		}

		// Parse the request using protojson (protobuf JSON format)
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

		// Now trigger achievements based on stat values
		// Only update when there's meaningful progress to report
		achievementUpdates := make(map[string]int64)

		// Handle level stat - milestone-based achievements
		// Only update sub-achievements directly (they have independent counts from parent)
		if levelStat, ok := statList.Public["level"]; ok {
			level := levelStat.Value
			logger.Debug("Team %s level is %d, checking milestones", request.Id, level)

			// Update sub-achievements when milestones are reached
			// Each sub-achievement has max_count: 1, so we set to 1 when milestone is reached
			if level >= 2 {
				achievementUpdates["TeamLevel_2"] = 1
			}
			if level >= 5 {
				achievementUpdates["TeamLevel_5"] = 1
			}
			if level >= 10 {
				achievementUpdates["TeamLevel_10"] = 1
			}
		}

		// Handle wins stat - cumulative achievements
		// Only update sub-achievements (they track independently toward their max_count)
		if winsStat, ok := statList.Public["wins"]; ok {
			wins := winsStat.Value
			if wins > 0 {
				logger.Debug("Team %s wins is %d, updating achievements", request.Id, wins)
				achievementUpdates["TeamWins_5"] = wins
				achievementUpdates["TeamWins_10"] = wins
				achievementUpdates["TeamWins_25"] = wins
			}
		}

		// Handle points stat - cumulative achievements
		// Only update sub-achievements (they track independently toward their max_count)
		if pointsStat, ok := statList.Public["points"]; ok {
			points := pointsStat.Value
			if points > 0 {
				logger.Debug("Team %s points is %d, updating achievements", request.Id, points)
				achievementUpdates["TeamPoints_100"] = points
				achievementUpdates["TeamPoints_250"] = points
				achievementUpdates["TeamPoints_500"] = points
			}
		}

		// Update achievements if any updates to make
		if len(achievementUpdates) > 0 {
			logger.Info("Updating team achievements: %v", achievementUpdates)
			_, _, err := teamsSystem.UpdateAchievements(ctx, logger, nk, userID, request.Id, achievementUpdates)
			if err != nil {
				logger.Warn("Failed to update team achievements: %v", err)
				// Don't return error - stat update already succeeded
			}
		} else {
			logger.Debug("No achievement updates needed")
		}

		// Return the stat list response using protojson
		response, err := protojson.Marshal(statList)
		if err != nil {
			return "", err
		}

		return string(response), nil
	}
}
