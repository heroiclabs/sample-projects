---
title: Event Leaderboards
summary: 
layout: article
menu:
  products:
    identifier: hiro-unity-event-leaderboards
    parent: hiro-unity
categories:
  - 
tags:
  - 
keywords:
  - 
---

# Event Leaderboards

Read more about the Event Leaderboards system in Hiro [here](../../concepts/event-leaderboards/).

## Initializing the event leaderboards system

The event leaderboards system system relies on the [Nakama System](../getting-started/nakama-system) and an `ILogger`, both must be passed in as dependencies via the constructor.

```csharp
var eventLeaderboardsSystem = new EventLeaderboardsSystem(logger, nakamaSystem);
systems.Add(eventLeaderboardsSystem);
```

## Listing Event Leaderboards

You can list all the available event leaderboards to the player by passing `null` as an argument. 

```csharp
var eventLeaderboards = await eventSystem.ListEventLeaderboardsAsync(null);
```

You can filter down the event leaderboards to only include those that belong to at least one of the given categories.

```csharp
IEnumerable<string> categories = new List<string> { "level_completion", "race"};
var eventLeaderboards = await eventSystem.ListEventLeaderboardsAsync(categories);
```

By default, the response doesn't include the scores of the event leaderboards. To have the response returning with the scores, set the boolean `with_score` on the request to true.

```csharp
var eventLeaderboards = await eventSystem.ListEventLeaderboardsAsync(null, true);
```

## Subscribing to changes in the event leaderboards system

You can listen for changes in the event leaderboards system so that you can respond appropriately, such as updating the UI, by implementing the `IObserver` pattern, or use the `SystemObserver<T>` type which handles it for you.

```csharp
var disposer = SystemObserver<EventLeaderboardsSystem>.Create(eventLeaderboardsSystem, system => {
    Instance.Logger.Info($"System updated.");

    // Update UI elements etc as necessary here...
});
```

## Getting an individual event leaderboard

You can get an individual event leaderboard, including it's records and information on it's rewards.

```csharp
var eventLeaderboard = await eventLeaderboardsSystem.GetEventLeaderboardAsync("<leaderboardId>");
```

## Submitting an event leaderboard score

You can submit an event leaderboard score for the user.

```csharp
var score = 100;
var subscore = 10;
var eventLeaderboard = await eventLeaderboardsSystem.UpdateEventLeaderboardAsync("<leaderboardId>", score, subscore);
```

## Claiming rewards

You can claim event leaderboard rewards for the user.

```csharp
var eventLeaderboard = await eventLeaderboardsSystem.ClaimEventLeaderboardAsync("<leaderboardId>");
```

## Re-rolling an event leaderboard

You can re-roll the cohort the user is in for a specific event leaderboard. A re-roll would occur when a user has previously joined an event leaderboard and claimed their reward but would now like to re-join again for another chance at claiming a reward, or to play against a different set of opponents.

```csharp
await eventLeaderboardsSystem.RollEventLeaderboardAsync("<leaderboardId>");
```

## Debugging an event leaderboard

You can fill an event leaderboard with dummy users and assign random scores to them for testing purposes.

{{< note "warning" >}}
This is intended for debugging use only.
{{< / note >}}

```csharp
var leaderboardId = "<leaderboardId>";
var targetCount = 50; // Optional target cohort size to fill to, otherwise fill to the max cohort size.

var minScore = 1;
var maxScore = 100;
var @operator = ApiOperator.SET;
var subscoreMin = 1;
var subscoreMax = 100;

// Fills cohort with debug players
await eventLeaderboardsSystem.DebugFillAsync(leaderboardId, targetCount);

// Sets randomly generated scores between a range for other players (does not change the user's score)
await eventLeaderboardsSystem.DebugRandomScoresAsync(leaderboardId, minScore, maxScore, @operator, subscoreMin, subscoreMax);
```