# Unity Hiro Event Leaderboards Sample

This Unity project demonstrates the Hiro Event Leaderboards system, showcasing time-bound competitive events where players compete in cohorts for rankings, tier progression, and rewards.

## Features

- **Event Leaderboard List**: Browse available event leaderboards with category filtering
- **Tier System**: Players progress through tiers based on performance
- **Cohort Matchmaking**: Automatic grouping with players of similar skill level
- **Score Submission**: Submit scores during active events
- **Rewards**: Claim rewards when events end
- **Re-Roll**: Enter new cohorts after claiming rewards
- **Debug Tools**: Fill cohorts and randomize scores for testing
- **Account Switcher**: Test with multiple accounts in the Unity Editor

## Requirements

- Unity 6000.2.6f2 or later
- Nakama + Hiro server running locally (default: http://127.0.0.1:7350)

## Getting Started

1. **Start the server:**
   ```bash
   cd ../Nakama+Hiro
   docker-compose up
   ```

2. **Open the project in Unity 6000.2.6f2+**

3. **Open the main scene:**
   - Navigate to `Assets/UnityHiroEventLeaderboards/Scenes/Main.unity`
   - Click Play

4. **Testing with multiple accounts:**
   - While in Play mode, open `Tools → Nakama → Account Switcher`
   - Use the dropdown to switch between test accounts

## Architecture

This project uses:
- **Hiro SDK**: Event Leaderboards system for time-bound competitions
- **UI Toolkit**: Modern Unity UI with UXML/USS
- **MVC Pattern**: Separation of controller and view logic
- **System Architecture**: Hiro's systems-based approach for game services

## Key Concepts

### Event Leaderboards vs Challenges

| Feature | Event Leaderboards | Challenges |
|---------|-------------------|------------|
| Creation | Server-configured | Player-created |
| Matchmaking | Automatic cohorts by tier | Invite-based |
| Lifecycle | Recurring, scheduled events | One-time, on-demand |
| Progression | Tier-based across events | Per-challenge only |

### Tier System

- Players start at tier 0
- Performance determines tier progression (up or down)
- Matchmaking groups players by tier
- Provides long-term progression across events

### Cohorts

- Small groups (typically 20-100 players) competing together
- Formed automatically based on tier and timing
- Provides fair competition at appropriate skill levels

## Documentation

See [CLAUDE.md](./CLAUDE.md) for detailed architecture and development information.

## Resources

- [Hiro Documentation](https://heroiclabs.com/docs/hiro/)
- [Event Leaderboards Concept Guide](https://heroiclabs.com/docs/hiro/concepts/event-leaderboards/)
- [Event Leaderboards Unity SDK Guide](https://heroiclabs.com/docs/hiro/unity/event-leaderboards/)
- [Community Forum](https://forum.heroiclabs.com/)

## License

Copyright 2025 The Nakama Authors

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
