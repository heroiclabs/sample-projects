---
title: Event Leaderboards
summary:
layout: article
menu:
  products:
    parent: gdk-concepts
    weight: 7
categories:
  -
tags:
  -
keywords:
  -
---

# Event Leaderboards

![Timed leaderboard event used in Candy Crush Saga by King]({{< fingerprint_image "/images/pages/hiro/concepts/event-leaderboard.png" >}})

## Overview

Event Leaderboards enable structured, time-bound competitions where players compete to achieve top rankings and earn rewards. They systematically boost player retention and generate excitement around your core gameplay loops.

Developers configure Event Leaderboards on the server, specifying the timing, cadence, and progression rules. When a player joins an event, they are assigned to an Event Leaderboard along with a select group of opponents (called a "cohort"). At the end of the event, rewards are distributed based on final rankings or achievement of target scores.

Event Leaderboards serve as competitive frameworks that connect players, track performance, and distribute rewards automatically - all while providing game developers with flexible configuration options to create engaging competitive experiences.

## Use Cases

Event Leaderboards can be configured to create different types of competitions. Here are some common implementation patterns that game developers use:

### Time-limited Events

Players compete for rankings within a fixed time window. This popular pattern works well for:

- Weekly tournaments
- Holiday events
- Season-based competitions

**Example: Weekly Tournaments in Puzzle Games**

Your players enjoy the core gameplay of your match 3 game, but engagement tends to drop after a few weeks. By implementing a weekly Event Leaderboard, you can:

- Have players compete for the highest score over a 5-day period
- Create a regular cadence of competition that brings players back every week
- Provide progression through tiers, giving players something to strive for long-term

### Target Score Events

Players race to reach a target score before their opponents can. This approach is great for:

- Achievement-based competitions
- "First past the finish line" challenges
- Multi-stage competitive events

Once a certain number of players reach the target score, you can trigger a "reroll" — creating fresh cohorts of opponents and extending the competitive experience. This can continue multiple times until the event's time period expires.

**Example: Dungeon Trials in RPGs**

Your RPG has challenging dungeons that players enjoy running, but once they've cleared them a few times, the experience becomes routine. Event Leaderboards can transform this familiar content into thrilling speed competitions where players race to clear featured dungeons as quickly as possible. The system can:

- Create time-limited competitions on existing content that players are already familiar with
- Automatically group players into smaller competitive brackets after each attempt
- Reward both the fastest global times and top performers within each bracket

The result transforms routine content into speedrunning events where players constantly push to optimize their strategies, while newcomers aren't immediately discouraged by facing the most elite players, keeping the entire community engaged in mastering their dungeon-crawling skills.

## Key Terms

- **Cohorts**: Groups of players that compete against each other in an event. Each player sees their own personalized cohort of opponents, selected to provide appropriate challenge.

- **Rewards**: The reward system distributes prizes based on final rankings or achievement of target scores. Rewards can be tiered, providing different prizes for different performance levels.

- **Tiers**: Tiers represent skill or progression levels in a competition, persisting across events. Players can move up or down tiers based on their performance, creating a sense of achievement beyond individual events.

- **Change zones**: The percentage of top and bottom players who advance or drop tiers on the Event Leaderboard.

## Tier-Based Cohort Matchmaking

When a player is placed on an Event Leaderboard:

1. The system determines the player's current tier:

   - **New players**: Start at tier 0.
   - **Returning players**: Tier is calculated based on previous performance using change zones or reward tier rules.

2. The system generates a personalized cohort of opponents by:

   - **Primary grouping**: Players are grouped by tier (tier 0, tier 1, tier 2, etc.).
   - **Cohort size**: Each cohort contains up to `cohort_size` players (configurable per event).
   - **First-come-first-served**: Within each tier, players are assigned to cohorts on a first-come-first-served basis.
   - **Simple fill logic**: The system searches for existing cohorts with available slots before creating new ones.

3. **Custom Matchmaking (Optional)**: Developers can implement custom cohort selection logic through the `onEventLeaderboardCohortSelection` callback, allowing for:
   - Skill-based matching beyond just tiers.
   - Custom cohort assignments.
   - Forced new cohort creation.

## Player Progression

Event Leaderboards implement a sophisticated tier system that promotes progression over time:

- Players are assigned to tiers (e.g., Bronze, Silver, Gold, etc) with tier 0 being the lowest.
- The system supports configurable numbers of tiers.
- Each tier can have different reward structures and competitive dynamics.

### Promotion and Demotion

**Change Zones (Primary Method)**: The system uses configurable promotion and demotion percentages to determine tier movement. A promotion percentage defines what portion of top performers advance to higher tiers, while a demotion percentage determines how many bottom performers drop down. These percentages can be configured per tier to create different competitive dynamics at various skill levels.

**Reward Tiers (Fallback Method)**: When change zones aren't configured, the system uses reward tiers for advancement. Each reward tier defines specific rank ranges and their associated tier changes, allowing players within certain positions to move up, down, or remain in their current tier.

### Handling Idle Players

The system includes configurable rules for handling inactive players. When idle demotion is enabled, players who fail to submit scores are automatically demoted. The system calculates tier drops based on how many complete event iterations they've missed, with a configurable maximum limit (`max_idle_tier_drop`) that caps the number of tiers they can drop when they return to the game.

### Calculating Tiers

Tier changes are calculated when players join new Event Leaderboard instances, considering:

- Previous performance and rank within the cohort
- Whether the player was idle (no score submissions)
- Applicable change zones or reward tier rules
- Maximum tier drop limits for idle players

Players cannot drop below tier 0 or exceed the maximum configured tier, ensuring the system maintains appropriate boundaries for all participants.

## Event Leaderboards vs Challenges

While both Event Leaderboards and Challenges provide competitive gameplay features, they serve different purposes and have distinct characteristics. Event Leaderboards are designed for structured, recurring competitive events with predefined cohorts and tiered progression. Challenges, on the other hand, are intended for on-demand and player-driven competitive experiences.

The following table outlines their differences:

{{< table name="gdk.concepts.event-leaderboards.event-leaderboard-comparison" >}}

The key distinction is that Event Leaderboards are designed for creating fair, engaging competitive experiences that scale well with large player bases, while maintaining player engagement through tiered progression systems.

### Comparison to Leaderboards and Tournaments

Event Leaderboards build upon Nakama's [Leaderboard](../../../nakama/concepts/leaderboards/) and [Tournament](../../../nakama/concepts/tournaments/) functionality but add several key enhancements:

- **Compared to Nakama Leaderboards**: Event Leaderboards add tiered progression, automated cohort formation, and the option to implement sophisticated matchmaking capabilities. While Nakama Leaderboards provide basic score tracking, Event Leaderboards create structured competitive experiences with tier-based matchmaking.

- **Compared to Nakama Tournaments**: Event Leaderboards offer more flexible scheduling and progression systems. While Tournaments are focused on time-bound competitions with fixed rules, Event Leaderboards provide ongoing competitive experiences with tiered progression and automated cohort management.

## Configuring Event Leaderboards

Event Leaderboards are highly customizable, allowing you to tailor the competitive experience to your specific game:

- Set duration, cohort sizes, and timing parameters
- Define reward tiers and distribution rules
- Customize scoring methods (ascending/descending, best/sum/latest/etc.)
- Group events into categories for easier player navigation

### Event Leaderboard Config

{{< table name="gdk.concepts.event-leaderboards.event-leaderboard">}}

### Reward Tier Config

{{< table name="gdk.concepts.event-leaderboards.reward-tier">}}

### Change Zone Config

{{< table name="gdk.concepts.event-leaderboards.change-zone">}}

### Score Operators

{{< table name="gdk.concepts.event-leaderboards.score-operators">}}

### Example: Event Leaderboards JSON

The JSON schema defines an `event_leaderboards` object which _must contain an individual object for each event leaderboard_ you wish to define in the system. You can configure as few or as many Event Leaderboards as needed for your game.

{{< table name="gdk.concepts.event-leaderboards.event-leaderboards-system">}}

The following JSON demonstrates the customization parameters you can use to configure the default user experience for Event Leaderboards.

```json
{
  "event_leaderboards": {
    "leaderboard_id1": {
      "name": "Chef Tournament",
      "description": "Play a tournament against other chefs for great rewards!",
      "category": "Challenges",
      "ascending": false,
      "operator": "best",
      "reset_schedule": "0 0 * * *",
      "cohort_size": 100,
      "additional_properties": {
        "key": "value"
      },
      "max_num_score": 0,
      "reward_tiers": {
        "0": [
          {
            "name": "<tiername>",
            "rank_max": 10,
            "rank_min": 1,
            "reward": {
              "guaranteed": {
                "currencies": {
                  "coins": {
                    "min": 1000,
                    "max": 2000
                  }
                }
              }
            },
            "tier_change": 1
          },
          {
            "name": "<tiername2>",
            "rank_max": 90,
            "rank_min": 11,
            "reward": {},
            "tier_change": 0
          },
          {
            "name": "<tiername3>",
            "rank_max": 100,
            "rank_min": 91,
            "reward": {},
            "tier_change": -1
          }
        ]
      },
      "change_zones": {
        "0": {
          "promotion": 0.5,
          "demotion": 0,
          "demote_idle": false
        },
        "1": {
          "promotion": 0.2,
          "demotion": 0.3,
          "demote_idle": true
        }
      },
      "tiers": 5,
      "max_idle_tier_drop": 1,
      "start_time_sec": 0,
      "end_time_sec": 0,
      "duration": 86400
    }
  }
}
```

### Event Leaderboard State

When the server returns an Event Leaderboard to the client, its current state is represented by a combination of three boolean values:

{{< table name="gdk.concepts.event-leaderboards.event-leaderboard-state">}}

## Additional Information

**How-to Guides**

- [How to implement weekly event leaderboards in a match 3 game](../../guides/gameplay-mechanics/event-leaderboard/)

**Linked Concepts**

- [Nakama Leaderboards](../../../nakama/concepts/leaderboards/)

**Reference Docs**

- [Server-side implementation of event leaderboards](../../server-framework/event-leaderboards/)
- [Event leaderboards in Unity](../../unity/event-leaderboards/)
- [Event leaderboards in Unreal](../../unreal/event-leaderboards/)
