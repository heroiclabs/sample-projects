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

using System.Threading.Tasks;
using Hiro.Unity;
using Hiro;
using Nakama;

namespace HiroChallenges
{
    public static class AccountSwitcher
    {
        public static async Task<ISession> SwitchAccountAsync(
            NakamaSystem nakamaSystem,
            ChallengesController controller,
            int accountIndex)
        {
            var newSession = await HiroChallengesCoordinator.NakamaAuthorizerFunc(accountIndex)
                .Invoke(nakamaSystem.Client);
            await SwitchToSessionAsync(nakamaSystem, controller, newSession);
            return newSession;
        }

        public static async Task SwitchToSessionAsync(
            NakamaSystem nakamaSystem,
            ChallengesController controller,
            ISession newSession)
        {
            (nakamaSystem.Session as Session).Update(newSession.AuthToken, newSession.RefreshToken);
            await nakamaSystem.RefreshAsync();
            await controller.SwitchCompleteAsync();
        }
    }
}
