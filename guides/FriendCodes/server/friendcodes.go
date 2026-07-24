package main

import (
	"context"
	"crypto/rand"
	"database/sql"
	"encoding/json"
	"time"

	"github.com/heroiclabs/nakama-common/runtime"
)

const (
	friendCodesCollection = "invite_codes"
	userInviteCollection  = "invite_codes_user"
	codeLength            = 6
	codeAlphabet          = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789" // no O/0, I/1 to avoid ambiguity
	codeTTL               = 72 * time.Hour                     // time to live (how long the code is valid for after creation
	deepLinkScheme        = "myunityapp"
)

type inviteRecord struct {
	OwnerID   string `json:"owner_id"`
	ExpiresAt int64  `json:"expires_at"`
}

type userInviteRecord struct {
	Code      string `json:"code"`
	ExpiresAt int64  `json:"expires_at"`
}

type redeemRequest struct {
	Code string `json:"code"`
}

// RpcGenerateFriendCode Try to generate a new friend code for the user, first checking if a valid code already exists.
func RpcGenerateFriendCode(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, ok := ctx.Value(runtime.RUNTIME_CTX_USER_ID).(string)
	if !ok || userID == "" {
		return "", runtime.NewError("no user id in context", 3)
	}

	now := time.Now()

	// Reuse an existing, valid code for this user rather than generating a new one.
	if existing, err := readUserInvite(ctx, nk, userID); err == nil && existing != nil {
		if existing.ExpiresAt > now.Unix() {
			return marshalCodeResponse(existing.Code, existing.ExpiresAt)
		}
	}

	code, err := mintUniqueCode(ctx, nk)
	if err != nil {
		logger.Error("failed to mint invite code: %v", err)
		return "", runtime.NewError("could not generate code", 13)
	}

	expiresAt := now.Add(codeTTL).Unix()

	globalRec := inviteRecord{
		OwnerID:   userID,
		ExpiresAt: expiresAt,
	}
	globalVal, _ := json.Marshal(globalRec)

	userRec := userInviteRecord{Code: code, ExpiresAt: expiresAt}
	userVal, _ := json.Marshal(userRec)

	writes := []*runtime.StorageWrite{
		{
			Collection:      friendCodesCollection,
			Key:             code,
			Value:           string(globalVal),
			PermissionRead:  0,
			PermissionWrite: 0,
		},
		{
			Collection:      userInviteCollection,
			Key:             "active",
			UserID:          userID,
			Value:           string(userVal),
			PermissionRead:  1,
			PermissionWrite: 0,
		},
	}

	if _, err := nk.StorageWrite(ctx, writes); err != nil {
		logger.Error("failed to write invite code: %v", err)
		return "", runtime.NewError("could not save code", 13)
	}

	return marshalCodeResponse(code, expiresAt)
}

// RpcRedeemFriendCode Try to redeem a friend code for the calling user.
func RpcRedeemFriendCode(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	redeemerID, ok := ctx.Value(runtime.RUNTIME_CTX_USER_ID).(string)
	if !ok || redeemerID == "" {
		return "", runtime.NewError("no user id in context", 3)
	}

	var req redeemRequest
	if err := json.Unmarshal([]byte(payload), &req); err != nil || req.Code == "" {
		return "", runtime.NewError("code is required", 3)
	}

	objs, err := nk.StorageRead(ctx, []*runtime.StorageRead{
		{Collection: friendCodesCollection, Key: req.Code},
	})
	if err != nil || len(objs) == 0 {
		return "", runtime.NewError("invalid or expired code", 5)
	}

	var rec inviteRecord
	if err := json.Unmarshal([]byte(objs[0].Value), &rec); err != nil {
		return "", runtime.NewError("invalid or expired code", 5)
	}
	if time.Now().Unix() > rec.ExpiresAt {
		return "", runtime.NewError("invalid or expired code", 5)
	}
	if rec.OwnerID == redeemerID {
		return "", runtime.NewError("you can't redeem your own code", 3)
	}

	// Add in both directions so the request is auto-confirmed
	if err := nk.FriendsAdd(ctx, redeemerID, "", []string{rec.OwnerID}, nil, nil); err != nil {
		logger.Error("friendsAdd (redeemer->owner) failed: %v", err)
		return "", runtime.NewError("could not add friend", 13)
	}
	if err := nk.FriendsAdd(ctx, rec.OwnerID, "", []string{redeemerID}, nil, nil); err != nil {
		logger.Error("friendsAdd (owner->redeemer) failed: %v", err)
		return "", runtime.NewError("could not add friend", 13)
	}

	resp, _ := json.Marshal(map[string]any{
		"success": true,
	})
	return string(resp), nil
}

// Try to find an existing code for the user.
func readUserInvite(ctx context.Context, nk runtime.NakamaModule, userID string) (*userInviteRecord, error) {
	objs, err := nk.StorageRead(ctx, []*runtime.StorageRead{
		{Collection: userInviteCollection, Key: "active", UserID: userID},
	})
	if err != nil || len(objs) == 0 {
		return nil, err
	}
	var rec userInviteRecord
	if err := json.Unmarshal([]byte(objs[0].Value), &rec); err != nil {
		return nil, err
	}
	return &rec, nil
}

// Call GenerateRandomCode until the value is unique, stops after 5 attempts.
func mintUniqueCode(ctx context.Context, nk runtime.NakamaModule) (string, error) {
	for attempt := 0; attempt < 5; attempt++ {
		code, err := generateRandomCode()
		if err != nil {
			return "", err
		}
		objs, err := nk.StorageRead(ctx, []*runtime.StorageRead{
			{Collection: friendCodesCollection, Key: code},
		})
		if err == nil && len(objs) == 0 {
			return code, nil
		}
	}
	return "", runtime.NewError("could not generate a unique code, try again", 13)
}

// Generate a random codeLength code only using characters in the codeAlphabet.
func generateRandomCode() (string, error) {
	bytes := make([]byte, codeLength)
	if _, err := rand.Read(bytes); err != nil {
		return "", err
	}
	out := make([]byte, codeLength)
	for i, b := range bytes {
		out[i] = codeAlphabet[int(b)%len(codeAlphabet)]
	}
	return string(out), nil
}

func marshalCodeResponse(code string, expiresAt int64) (string, error) {
	resp, err := json.Marshal(map[string]any{
		"code":       code,
		"expires_at": expiresAt,
		"deep_link":  deepLinkScheme + "://friendcode?code=" + code,
	})
	return string(resp), err
}
