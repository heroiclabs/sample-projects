package main

import (
	"context"
	"github.com/heroiclabs/hiro"
	"github.com/heroiclabs/nakama-common/runtime"
)

const (
	categoryGachaTicket = "gacha_ticket"

	propSixStarPity  = "six_star_pity"
	propFiveStarPity = "five_star_pity"
	propStarRarity   = "star_rarity"

	itemSetSuffixSixStar   = "_six_star"
	itemSetSuffixFiveStar  = "_five_star"
	statSuffixSixStarPity  = "_six_star_pity"
	statSuffixFiveStarPity = "_five_star_pity"
	tokenSuffix            = "_token"

	rarityFiveStar = 5
	raritySixStar  = 6

	pityWeightGuaranteedSixStar = 100
	pityWeightFiveStar          = 99
	pityWeightSixStar           = 1
)

func handleGachaConsumeReward(
	ctx context.Context,
	logger runtime.Logger,
	nk runtime.NakamaModule,
	economySystem hiro.EconomySystem,
	inventorySystem hiro.InventorySystem,
	statsSystem hiro.StatsSystem,
	userID, sourceID string,
	source *hiro.InventoryConfigItem,
	reward *hiro.Reward,
) (*hiro.Reward, error) {
	// Only process gacha ticket items.
	if source.Category != categoryGachaTicket {
		return reward, nil
	}

	// Load inventory config for looking up item properties.
	config, ok := inventorySystem.GetConfig().(*hiro.InventoryConfig)
	if !ok {
		logger.Error("unexpected inventory system config type, using default")
		return nil, nil
	}

	// Load stats for getting and updating pity progress.
	statList, err := statsSystem.List(ctx, logger, nk, userID, []string{userID})
	if err != nil {
		logger.WithField("error", err.Error()).Error("statsSystem.List error")
		return nil, err
	}

	sixStarPity := getPityStat(statList, userID, sourceID+statSuffixSixStarPity)
	maxSixStarPity, hasSixStarPity := source.NumericProperties[propSixStarPity]

	// Time for 6-star pity to kick in.
	if hasSixStarPity && sixStarPity >= int(maxSixStarPity-1) {
		// Roll a new reward with a guaranteed 6-star.
		cfg := buildPityRewardConfig(sourceID, 0, pityWeightGuaranteedSixStar)
		reward, err = rollPityReward(ctx, logger, nk, economySystem, config, userID, reward, cfg, raritySixStar)
		if err != nil {
			return nil, err
		}
	} else {
		// If it's not time for 6-star pity, check if it's time for 5-star pity instead.
		fiveStarPity := getPityStat(statList, userID, sourceID+statSuffixFiveStarPity)
		maxFiveStarPity, hasFiveStarPity := source.NumericProperties[propFiveStarPity]

		// Time for 5-star pity to kick in.
		if hasFiveStarPity && fiveStarPity >= int(maxFiveStarPity-1) {
			// Roll a new reward with an extremely likely 5-star, whilst keeping the small chance for a 6-star.
			cfg := buildPityRewardConfig(sourceID, pityWeightFiveStar, pityWeightSixStar)
			reward, err = rollPityReward(ctx, logger, nk, economySystem, config, userID, reward, cfg, rarityFiveStar)
			if err != nil {
				return nil, err
			}
		}
	}

	// After deciding what reward the user will receive,
	// update pity stats accordingly (based on the rarity of the reward item).
	if err := updatePityStats(ctx, logger, nk, statsSystem, config, userID, sourceID, reward); err != nil {
		return nil, err
	}

	// Finally, check if the user already has the reward item.
	// If they do, then replace the reward with a stackable token representing a duplicate.
	// This could be used to upgrade the item later, for example.
	return replaceDuplicateWithToken(ctx, logger, nk, inventorySystem, userID, reward)
}

func getPityStat(statList map[string]*hiro.StatList, userID, statKey string) int {
	if stats, found := statList[userID]; found {
		if stat, found := stats.GetPrivate()[statKey]; found {
			return int(stat.GetValue())
		}
	}
	return 0
}

func getItemRarity(config *hiro.InventoryConfig, itemID string) float64 {
	if item, found := config.Items[itemID]; found {
		return item.NumericProperties[propStarRarity]
	}
	return 0
}

func firstRewardItemID(reward *hiro.Reward) string {
	for itemID := range reward.Items {
		return itemID
	}
	return ""
}

func buildPityRewardConfig(sourceID string, fiveStarWeight, sixStarWeight int64) *hiro.EconomyConfigReward {
	var contents []*hiro.EconomyConfigRewardContents

	if fiveStarWeight > 0 {
		contents = append(contents, &hiro.EconomyConfigRewardContents{
			ItemSets: []*hiro.EconomyConfigRewardItemSet{{
				Set:                           []string{sourceID + itemSetSuffixFiveStar},
				EconomyConfigRewardRangeInt64: hiro.EconomyConfigRewardRangeInt64{Min: 1},
			}},
			Weight: fiveStarWeight,
		})
	}

	if sixStarWeight > 0 {
		contents = append(contents, &hiro.EconomyConfigRewardContents{
			ItemSets: []*hiro.EconomyConfigRewardItemSet{{
				Set:                           []string{sourceID + itemSetSuffixSixStar},
				EconomyConfigRewardRangeInt64: hiro.EconomyConfigRewardRangeInt64{Min: 1},
			}},
			Weight: sixStarWeight,
		})
	}

	// This reward keeps the same item sets as the original gacha ticket, but with modified weights.
	return &hiro.EconomyConfigReward{
		Weighted: contents,
		MaxRolls: 1,
	}
}

func rollPityReward(
	ctx context.Context,
	logger runtime.Logger,
	nk runtime.NakamaModule,
	economySystem hiro.EconomySystem,
	config *hiro.InventoryConfig,
	userID string,
	reward *hiro.Reward,
	rewardConfig *hiro.EconomyConfigReward,
	minRarity float64,
) (*hiro.Reward, error) {
	// If the reward is already the same, or higher rarity than the pity, then we don't need to roll a new reward.
	itemID := firstRewardItemID(reward)
	if itemID == "" || getItemRarity(config, itemID) >= minRarity {
		return reward, nil
	}

	rolledReward, err := economySystem.RewardRoll(ctx, logger, nk, userID, rewardConfig)
	if err != nil {
		return nil, err
	}

	return rolledReward, nil
}

func updatePityStats(
	ctx context.Context,
	logger runtime.Logger,
	nk runtime.NakamaModule,
	statsSystem hiro.StatsSystem,
	config *hiro.InventoryConfig,
	userID, sourceID string,
	reward *hiro.Reward,
) error {
	itemID := firstRewardItemID(reward)
	if itemID == "" {
		return nil
	}

	rarity := getItemRarity(config, itemID)

	var statUpdates []*hiro.StatUpdate

	switch {
	case rarity >= raritySixStar:
		// Reset both pity counters on a six-star pull.
		statUpdates = []*hiro.StatUpdate{
			{Name: sourceID + statSuffixSixStarPity, Value: 0, Operator: hiro.StatUpdateOperator_STAT_UPDATE_OPERATOR_SET},
			{Name: sourceID + statSuffixFiveStarPity, Value: 0, Operator: hiro.StatUpdateOperator_STAT_UPDATE_OPERATOR_SET},
		}
	case rarity >= rarityFiveStar:
		// Increment six-star pity, and reset five-star pity on a five-star pull.
		statUpdates = []*hiro.StatUpdate{
			{Name: sourceID + statSuffixSixStarPity, Value: 1, Operator: hiro.StatUpdateOperator_STAT_UPDATE_OPERATOR_DELTA},
			{Name: sourceID + statSuffixFiveStarPity, Value: 0, Operator: hiro.StatUpdateOperator_STAT_UPDATE_OPERATOR_SET},
		}
	default:
		// Increment both pity counters on a sub-five-star pull.
		statUpdates = []*hiro.StatUpdate{
			{Name: sourceID + statSuffixSixStarPity, Value: 1, Operator: hiro.StatUpdateOperator_STAT_UPDATE_OPERATOR_DELTA},
			{Name: sourceID + statSuffixFiveStarPity, Value: 1, Operator: hiro.StatUpdateOperator_STAT_UPDATE_OPERATOR_DELTA},
		}
	}

	if _, err := statsSystem.Update(ctx, logger, nk, userID, nil, statUpdates); err != nil {
		logger.Error("Failed to update pity stats for user %s: %v", userID, err)
		return err
	}

	return nil
}

func replaceDuplicateWithToken(
	ctx context.Context,
	logger runtime.Logger,
	nk runtime.NakamaModule,
	inventorySystem hiro.InventorySystem,
	userID string,
	reward *hiro.Reward,
) (*hiro.Reward, error) {
	itemID := firstRewardItemID(reward)
	if itemID == "" {
		return reward, nil
	}

	// Load the user's inventory to check if the reward item is already owned.
	inventoryItems, err := inventorySystem.ListInventoryItems(ctx, logger, nk, userID, "")
	if err != nil {
		logger.Error("Failed to list inventory items for user %s: %v", userID, err)
		return nil, err
	}

	// If the user already has the item, replace the reward item with a token for that item.
	// All gacha items have a counterpart with the "_token" suffix.
	for _, existing := range inventoryItems.Items {
		if existing.Id == itemID {
			reward.Items = map[string]int64{
				itemID + tokenSuffix: 1,
			}
			return reward, nil
		}
	}

	return reward, nil
}
