// Copyright 2025 The Nakama Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Hiro;

namespace HiroChallenges
{
    /// <summary>
    /// Computes what actions a user can take on a challenge.
    /// Pure business logic - no UI concerns.
    /// </summary>
    public class ChallengePermissions
    {
        public bool CanJoin { get; }
        public bool CanLeave { get; }
        public bool CanSubmitScore { get; }
        public bool CanInvite { get; }
        public bool CanClaim { get; }
        public long ScoresSubmitted { get; }
        public long MaxScores { get; }

        public ChallengePermissions(IChallenge challenge, IChallengeScore participant)
        {
            var isActive = challenge.IsActive;
            var isParticipant = participant != null;
            var isOwner = isParticipant && participant.Id == challenge.OwnerId;
            var hasRoomForMore = challenge.Size < challenge.MaxSize;
            var canClaimRewards = challenge.CanClaim;

            ScoresSubmitted = participant?.NumScores ?? 0;
            MaxScores = challenge.MaxNumScore;

            CanJoin = isActive && !isParticipant;
            CanLeave = !isActive && isParticipant && !canClaimRewards;
            CanSubmitScore = isActive && isParticipant && ScoresSubmitted < MaxScores;
            CanInvite = isActive && isOwner && hasRoomForMore;
            CanClaim = !isActive && isParticipant && canClaimRewards;
        }
    }
}
