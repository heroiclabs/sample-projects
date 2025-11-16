---
title: Event Leaderboard (Royal Match King's Cup)
layout: article
menu:
  products:
    identifier: hiro_guides_gameplay-mechanics_event-leaderboard
    parent: hiro_guides_gameplay-mechanics
aliases:
  - /hiro/guides/event-leaderboard/
---

# Event Leaderboard System

{{< note "important" "Reconstructing Fun video series" >}}
This guide is adapted from our Reconstructing Fun video series, where we explore how to build popular game mechanics using Nakama and Hiro. You can watch the full video below and follow [our channel on YouTube](https://www.youtube.com/@heroiclabs) for the latest updates.
{{< /note >}}

In the fast-paced realm of mobile gaming, leaderboards and competitive play have become an integral aspect of player engagement, where the thrill of climbing ranks and earning exclusive rewards keeps players coming back for more, time and time again.

In this guide we'll explore how you can quickly integrate an event leaderboard system using Nakama and Hiro to produce gameplay experiences similar to that of Royal Match's hugely successful King's Cup weekly event.

{{< youtube "DRFr3VBeCcQ" >}}

## Prerequisites

To follow this guide you'll need to:

* [Install Nakama](../../../../nakama/getting-started/install/docker/)
* [Install Hiro](../../../../hiro/concepts/getting-started/install/)
* [Install Unity](https://unity3d.com/get-unity/download)

Once that's out of the way, you can familiarize yourself with the full project code we'll be using in this guide by cloning the [Event Leaderboard repository](https://github.com/heroiclabs/reconstructing-fun/tree/main/event-leaderboard) from GitHub.

## Server-side

Let's start by taking a look at the server-side code we'll be using to implement the event leaderboard mechanics, beginning with the [`main.go` file](https://github.com/heroiclabs/reconstructing-fun/blob/main/event-leaderboard/server/main.go).

### `main.go`

You can reference the full code for this file in the linked repository above. Here we'll break down the key components of the code.

#### Define error messages

First we define the error messages we'll use throughout the codebase:

```go
var (
	errBadInput        = runtime.NewError("input contained invalid data", 3) // INVALID_ARGUMENT
	errInternal        = runtime.NewError("internal server error", 13)       // INTERNAL
	errMarshal         = runtime.NewError("cannot marshal type", 13)         // INTERNAL
	errNoInputAllowed  = runtime.NewError("no input allowed", 3)             // INVALID_ARGUMENT
	errNoInputGiven    = runtime.NewError("no input was given", 3)           // INVALID_ARGUMENT
	errNoUserIdFound   = runtime.NewError("no user ID in context", 3)        // INVALID_ARGUMENT
	errNoUsernameFound = runtime.NewError("no username in context", 3)       // INVALID_ARGUMENT
	errUnmarshal       = runtime.NewError("cannot unmarshal type", 13)       // INTERNAL
)
```

#### `InitModule` function

Next we define our `InitModule` function, which is called when the server starts up. Here we'll initialize the Hiro systems - Economy, Inventory and Event Leaderboards - we'll be using, and register the RPC functions we'll be implementing.

```go
func InitModule(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, initializer runtime.Initializer) error {
	props, ok := ctx.Value(runtime.RUNTIME_CTX_ENV).(map[string]string)
	if !ok {
		return errors.New("invalid context runtime env")
	}

	env, ok := props["ENV"]
	if !ok || env == "" {
		return errors.New("'ENV' key missing or invalid in env")
	}

	hiroLicense, ok := props["HIRO_LICENSE"]
	if !ok || hiroLicense == "" {
		return errors.New("'HIRO_LICENSE' key missing or invalid in env")
	}

	binPath := "hiro.bin"
	systems, err := hiro.Init(ctx, logger, nk, initializer, binPath, hiroLicense,
		hiro.WithEconomySystem(fmt.Sprintf("base-economy-%s.json", env), true),
		hiro.WithInventorySystem(fmt.Sprintf("base-inventory-%s.json", env), true),
        hiro.WithEventLeaderboardsSystem(fmt.Sprintf("base-eventleaderboards-%s.json", env), true))
	if err != nil {
		return err
	}

	return nil
}
```

### Hiro system definitions

Next we define the Hiro system definitions we'll be using to implement the event leaderboard. These are defined in the [`base-inventory-dev1` file](https://github.com/heroiclabs/reconstructing-fun/blob/main/event-leaderboard/server/base-inventory-dev1.json), [`base-economy-dev1` file](https://github.com/heroiclabs/reconstructing-fun/blob/main/event-leaderboard/server/base-economy-dev1.json), and [`base-eventleaderboards-dev1` file](https://github.com/heroiclabs/reconstructing-fun/blob/main/event-leaderboard/server/base-eventleaderboards-dev1.json) respectively.

#### Inventory

The Hiro Inventory system enables you to define and manage the items that can be collected and used by players in your game. In this example, we'll use the Inventory system to define the various powerups that players can acquire and use in the game, setting attributes like their name, description, maximum count, stackability, and rarity.

```json
// ...
"powerup_cannon": {
            "name": "Cannon",
            "description": "Clears a column of tiles.",
            "category": "powerups",
            "item_sets": ["powerups"],
            "max_count": 10,
            "stackable": true,
            "consumable": true,
            "consume_reward": null,
            "string_properties": {},
            "numeric_properties": {}
        }
// ...
```

Each powerup is specified as both `stackable` (which means the player will own a single instance of the item with a defined `count`) and `consumable` (which means the item can be consumed by the player, thereby reducing the count the player owns).

#### Economy

The Hiro Economy system enables you to define and manage the currencies that players can earn and spend in your game, and also define the currencies and amounts that each player begins the game with.

```json
{
    "initialize_user": {
        "currencies": {
            "coins": 9999999,
            "gems": 999,
            "tokens": 0
        },
        "items": {
            "powerup_hammer": 2
        }
    },
    "store_items": {}
}
```

Here we define the currencies and amounts that each player begins the game with, as well as the items that each player begins the game with. In this example, we've given each player a couple of hammer powerups to begin with, as well as a large amount of coins and a smaller amount of gems.

#### Event Leaderboard

The Hiro Event Leaderboard system enables you to define and manage the various event leaderboards within your game. These are recurring events that players can participate in to earn various rewards. Event leaderboards also allow you to group your players into cohorts, as well as segregate them by tiers if this is something that matches your game's design.

```json
{
  "event_leaderboards": {
    "kings_cup": {
      "name": "Kings Cup",
      "description": "Take the Crown!",
      "category": "Weekly",
      "ascending": false,
      "operator": "incr",
      "reset_schedule": "0 0 * * 1",
      "cohort_size": 50,
      "max_num_score": 0,
      "tiers": 1,
      "max_idle_tier_drop": 0,
      "reward_tiers": {
        "0": [
          {
            "name": "gold",
            "tier_change": 0,
            "rank_min": 1,
            "rank_max": 1,
            "reward": {
              "guaranteed": {
                "energy_modifiers": [{
                  "id": "lives",
                  "operator": "infinite",
                  "value": {
                    "min": 0
                  },
                  "duration_sec": {
                    "min": 10800
                  }
                }],
                "item_sets": [{
                  "set": ["cards_legendary"],
                  "count": {
                    "min": 1
                  },
                  "max_repeats": 1
                }],
                "items": {
                  "powerup_hammer": {
                    "min": 1
                  },
                  "powerup_arrow": {
                    "min": 1
                  },
                  "powerup_cannon": {
                    "min": 1
                  },
                  "powerup_jester_hat": {
                    "min": 1
                  },
                  "powerup_light_ball": {
                    "min": 3
                  },
                  "powerup_tnt": {
                    "min": 3
                  },
                  "powerup_rocket": {
                    "min": 3
                  }
                }
              }
            }
          },
          {
            "name": "silver",
            "tier_change": 0,
            "rank_min": 2,
            "rank_max": 2,
            "reward": {
              "guaranteed": {
                "energy_modifiers": [{
                  "id": "lives",
                  "operator": "infinite",
                  "value": {
                    "min": 0
                  },
                  "duration_sec": {
                    "min": 7200
                  }
                }],
                "item_sets": [{
                  "set": ["cards"],
                  "count": {
                    "min": 4
                  },
                  "max_repeats": 4
                }],
                "items": {
                  "powerup_hammer": {
                    "min": 1
                  },
                  "powerup_jester_hat": {
                    "min": 1
                  },
                  "powerup_light_ball": {
                    "min": 2
                  },
                  "powerup_tnt": {
                    "min": 2
                  },
                  "powerup_rocket": {
                    "min": 2
                  }
                }
              }
            }
          },
          {
            "name": "bronze",
            "tier_change": 0,
            "rank_min": 3,
            "rank_max": 3,
            "reward": {
              "guaranteed": {
                "energy_modifiers": [{
                  "id": "lives",
                  "operator": "infinite",
                  "value": {
                    "min": 0
                  },
                  "duration_sec": {
                    "min": 3600
                  }
                }],
                "item_sets": [{
                  "set": ["cards"],
                  "count": {
                    "min": 2
                  },
                  "max_repeats": 2
                }],
                "items": {
                  "powerup_light_ball": {
                    "min": 2
                  },
                  "powerup_tnt": {
                    "min": 2
                  },
                  "powerup_rocket": {
                    "min": 2
                  }
                }
              }
            }
          },
          {
            "name": "standard",
            "tier_change": 0,
            "rank_min": 4,
            "rank_max": 10,
            "reward": {
              "guaranteed": {
                "currencies": {
                  "tokens": {
                    "min": 10
                  }
                },
                "items": {
                  "powerup_hammer": {
                    "min": 1
                  },
                  "powerup_arrow": {
                    "min": 1
                  },
                  "powerup_cannon": {
                    "min": 1
                  },
                  "powerup_jester_hat": {
                    "min": 1
                  }
                }
              }
            }
          },
          {
            "name": "participation",
            "tier_change": 0,
            "rank_min": 11,
            "rank_max": 50,
            "reward": {
              "guaranteed": {
                "currencies": {
                  "coins": {
                    "min": 100
                  }
                }
              }
            }
          }
        ]
      },
      "change_zones": {},
      "start_time_sec": 0,
      "end_time_sec": 0,
      "duration": 604800,
      "additional_properties": {
        "some_value": "some_property"
      }
    }
  }
}
```

For our "King's Cup" event leaderboard, we have specified that it should repeat every Monday at 00:00AM and last for the full duration of 1 week (604800 seconds). We have also specified that players should be bucketed into cohorts of 50 (this ensures leaderboards remain fair and competitive), as well as defined the various rewards available by rank at the end of each iteration.

## Client-side

### `KingsCupGameCoordinator`

This file bootstraps our game with a list of systems to be used, and provides a list of systems for deterministic start-up. In our case, we're initializing the Inventory and Economy core systems from Hiro, and finally our custom King's Cup Game system.

```csharp
// ...
systems.Add(nakamaSystem);
        
 // Add the Inventory system
var inventorySystem = new InventorySystem(logger, nakamaSystem);
systems.Add(inventorySystem);

// Add the Economy system
var economySystem = new EconomySystem(logger, nakamaSystem, EconomyStoreType.Unspecified);
systems.Add(economySystem);

// Add the Event Leaderboards system
var eventLeaderboardsSystem = new EventLeaderboardsSystem(logger, nakamaSystem);
systems.Add(eventLeaderboardsSystem);

// Add the Kings Cup System
var kingsCupSystem = new KingsCupSystem(logger, eventLeaderboardsSystem);
systems.Add(kingsCupSystem);


return Task.FromResult(systems);
// ...
```

### `KingsCupSystem`

This file contains the client logic for the kings cup event leaderboard system, including functions to fetch the player's current event leaderboard information, submit scores and claim their reward.

To start with, we have a cached representation of the player's current event leaderboard data:

```csharp
// ...
public IEventLeaderboard EventLeaderboard => _eventLeaderboard;
// ...
```

We also have a few helper properties to determine whether the player can claim a reward, can roll for the next iteration, and whether the event leaderboard is currently active or not:

```csharp
public bool CanClaim => _eventLeaderboard != null && _eventLeaderboard.CanClaim;
public bool CanRoll => _eventLeaderboard != null && _eventLeaderboard.CanRoll;
public bool IsActive => _eventLeaderboard != null && _eventLeaderboard.IsActive;
```

Then we have several functions to handle getting the event leaderboard, rolling for the next iteration, submitting scores and claiming rewards, each of which calls the corresponding function within the `EventLeaderboardSystem`, which ultimately calls the appropriate RPC on the server-side.

```csharp
// ...
public async Task GetAndRollAsync()
{
    // Get the event leaderboard for the user
    _eventLeaderboard = await _eventLeaderboardsSystem.GetEventLeaderboardAsync(LeaderboardId);
    
    // If we can roll for the next iteration and we don't have a reward to claim, automatically roll.
    if (_eventLeaderboard.CanRoll && !_eventLeaderboard.CanClaim)
    {
        _eventLeaderboard = await _eventLeaderboardsSystem.RollEventLeaderboardAsync(LeaderboardId);
    }

    NotifyObservers();
}

public async Task<IEventLeaderboard> RollAsync()
{
    _eventLeaderboard = await _eventLeaderboardsSystem.RollEventLeaderboardAsync(LeaderboardId);
    NotifyObservers();

    return _eventLeaderboard;
}

public async Task<IEventLeaderboard> SubmitScoreAsync(long score)
{
    _eventLeaderboard = await _eventLeaderboardsSystem.UpdateEventLeaderboardAsync(LeaderboardId, score);
    NotifyObservers();

    return _eventLeaderboard;
}

public async Task<IEventLeaderboard> ClaimAsync()
{
    _eventLeaderboard = await _eventLeaderboardsSystem.ClaimEventLeaderboardAsync(LeaderboardId);
    NotifyObservers();

    return _eventLeaderboard;
}
// ...
```

Note that while a most of these functions look like simple wrappers for functionality that already exists within the `EventLeaderboardSystem` there is one strong distinction, the caching of the `_eventLeaderboard` data as well as calling out to `NotifyObservers()`.

The `EventLeaderboardSystem` is a stateless system and therefore does not cache any data, nor does it notify observers when new data is available. Our `KingsCupSystem` allows other parts of the client application to monitor it for changes, allowing things such as the user interface to be updated appropriately.

### `KingsCupManager`

The `KingsCupManager` manages all calls to our Hiro systems, and creates system observers for each system to handle UI updates based on system changes.

```csharp
// ...
public async Task InitAsync()
{
    _nakamaSystem = this.GetSystem<NakamaSystem>();
    _kingsCupSystem = this.GetSystem<KingsCupSystem>();
    SystemObserver<KingsCupSystem>.Create(_kingsCupSystem, OnKingsCupSystemChanged);

    // Get the user's current event leaderboard
    await _kingsCupSystem.GetAndRollAsync();

    GoToEventScreen();
}

private void OnKingsCupSystemChanged(KingsCupSystem system)
{
    if (system.EventLeaderboard == null)
    {
        return;
    }

    // Update the end time seconds value
    _endTimeSeconds = system.EventLeaderboard.EndTimeSec;
    
    // Display the active time or the claim button
    if (system.CanClaim)
    {
        activeTime.SetActive(false);
        claimButton.SetActive(true);
    }
    else
    {
        activeTime.SetActive(true);
        claimButton.SetActive(false);
    }
    
    // Clear the current scores list
    foreach (Transform child in scoresContainer)
    {
        Destroy(child.gameObject);
    }
    
    // Refresh the scores list
    foreach (var score in system.EventLeaderboard.Scores)
    {
        if (score.Username == _nakamaSystem.Account.User.Username)
        {
            ownListItem.Init(score);
        }
        
        var listItem = Instantiate(scoreListItemPrefab, scoresContainer);
        listItem.GetComponent<KingsCupListItemUI>().Init(score);
    }
}

public async void SubmitScore()
{
    try
    {
        await _kingsCupSystem.RefreshAsync();

        // Only submit a score if the event is still active
        if (_kingsCupSystem.IsActive)
        {
            var score = new Random().Next(1, 10);
            await _kingsCupSystem.SubmitScoreAsync(score);
        }
    }
    catch (ApiResponseException)
    {
        Debug.Log("Unable to submit score, event leaderboard is likely inactive.");
    }
    
    GoToEventScreen();
}

// These functions have a void return type so we can hook this up to the Claim button in the Unity inspector
public async void Claim()
{
    // Claim the reward
    await _kingsCupSystem.RefreshAsync();

    if (_kingsCupSystem.CanClaim)
    {
        var result = await _kingsCupSystem.ClaimAsync();
        rewardPanel.SetActive(true);

        // Clear the current reward list
        foreach (Transform child in rewardItemsContainer)
        {
            Destroy(child.gameObject);
        }
        
        // Update the reward list
        foreach (var item in result.Reward.Items)
        {
            var rewardItem = Instantiate(rewardItemPrefab, rewardItemsContainer);
            rewardItem.GetComponent<KingsCupRewardItemUI>().Init(item.Key, item.Value);
        }
        
        foreach (var energyModifier in result.Reward.EnergyModifiers)
        {
            var rewardItem = Instantiate(rewardItemPrefab, rewardItemsContainer);
            var text = energyModifier.Operator == "infinite" ? "∞" : energyModifier.Value.ToString();

            var timespan = TimeSpan.FromSeconds(energyModifier.DurationSec);
            text += $" ({timespan.TotalMinutes}m) ";
            
            rewardItem.GetComponent<KingsCupRewardItemUI>().Init(energyModifier.Id, text);
        }
        
        foreach (var currency in result.Reward.Currencies)
        {
            var rewardItem = Instantiate(rewardItemPrefab, rewardItemsContainer);
            rewardItem.GetComponent<KingsCupRewardItemUI>().Init(currency.Key, currency.Value);
        }
    }
    
    // Re-join the event if we can
    if (_kingsCupSystem.CanRoll)
    {
        await _kingsCupSystem.RollAsync();
    }
}
// ...
```
