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

using NUnit.Framework;

namespace HiroChallenges.Tests.Editor
{
    [TestFixture]
    public class UsernameAutocompleteTests
    {
        private static readonly string[] TestUsernames = { "Player123", "Player234", "Admin", "PlayerOne" };

        [Test]
        public void Complete_WithCommonPrefix_ReturnsLongestCommonPrefix()
        {
            var result = UsernameAutocomplete.Complete("P", TestUsernames);

            Assert.AreEqual("Player", result);
        }

        [Test]
        public void Complete_WithSingleMatch_ReturnsFullUsername()
        {
            var result = UsernameAutocomplete.Complete("A", TestUsernames);

            Assert.AreEqual("Admin", result);
        }

        [Test]
        public void Complete_WithNoMatches_ReturnsInput()
        {
            var result = UsernameAutocomplete.Complete("X", TestUsernames);

            Assert.AreEqual("X", result);
        }

        [Test]
        public void Complete_WhenInputEqualsCommonPrefix_ReturnsFirstMatch()
        {
            var result = UsernameAutocomplete.Complete("Player", TestUsernames);

            // Player123, Player234, PlayerOne all match - common prefix is "Player"
            // Input equals common prefix, so returns first match
            Assert.AreEqual("Player123", result);
        }

        [Test]
        public void Complete_WithMoreSpecificInput_ReturnsLongestCommonPrefix()
        {
            var result = UsernameAutocomplete.Complete("Player2", TestUsernames);

            // Only Player234 matches
            Assert.AreEqual("Player234", result);
        }

        [Test]
        public void Complete_WithEmptyInput_ReturnsCommonPrefixOrFirst()
        {
            // TestUsernames: Player123, Player234, Admin, PlayerOne
            // No common prefix among all (Admin breaks it), so returns first match
            var result = UsernameAutocomplete.Complete("", TestUsernames);
            Assert.AreEqual("Player123", result);

            // With common prefix
            var playerUsernames = new[] { "Player123", "Player234" };
            var result2 = UsernameAutocomplete.Complete("", playerUsernames);
            Assert.AreEqual("Player", result2); // Common prefix
        }

        [Test]
        public void Complete_WithNullInput_TreatedAsEmpty()
        {
            // null treated as empty, so autocompletes like empty input
            // TestUsernames have no common prefix, returns first
            var result = UsernameAutocomplete.Complete(null, TestUsernames);

            Assert.AreEqual("Player123", result);
        }

        [Test]
        public void Complete_WithNullUsernames_ReturnsInput()
        {
            var result = UsernameAutocomplete.Complete("Test", null);

            Assert.AreEqual("Test", result);
        }

        [Test]
        public void Complete_CaseInsensitive_MatchesRegardlessOfCase()
        {
            var result = UsernameAutocomplete.Complete("player", TestUsernames);

            // "player" matches Player123, Player234, PlayerOne (case insensitive)
            // Common prefix "Player" equals input (case insensitive), returns first match
            Assert.AreEqual("Player123", result);
        }

        [Test]
        public void Complete_WithDivergingMatches_StopsAtDivergence()
        {
            var usernames = new[] { "PlayerAlpha", "PlayerBeta" };

            var result = UsernameAutocomplete.Complete("P", usernames);

            Assert.AreEqual("Player", result);
        }

        [Test]
        public void Complete_WithIdenticalMatches_ReturnsFullMatch()
        {
            var usernames = new[] { "Player", "Player" };

            var result = UsernameAutocomplete.Complete("P", usernames);

            Assert.AreEqual("Player", result);
        }

        [Test]
        public void Complete_CommaSeparated_AutocompletesLastSegment()
        {
            var result = UsernameAutocomplete.Complete("Player123,P", TestUsernames);

            // Player123 is excluded, so only Player234 and PlayerOne match "P"
            Assert.AreEqual("Player123,Player", result);
        }

        [Test]
        public void Complete_CommaSeparated_ExcludesAlreadySelected()
        {
            var usernames = new[] { "Player123", "Player234" };

            var result = UsernameAutocomplete.Complete("Player123,P", usernames);

            // Only Player234 matches since Player123 is excluded
            Assert.AreEqual("Player123,Player234", result);
        }

        [Test]
        public void Complete_CommaSeparated_MultipleAlreadySelected()
        {
            var usernames = new[] { "Alice", "Bob", "Charlie", "Carol" };

            var result = UsernameAutocomplete.Complete("Alice,Bob,C", usernames);

            // Charlie and Carol match "C", common prefix is "C" which equals input
            // So picks first match "Charlie"
            Assert.AreEqual("Alice,Bob,Charlie", result);
        }

        [Test]
        public void Complete_CommaSeparated_SingleRemainingMatch()
        {
            var usernames = new[] { "Alice", "Bob", "Charlie" };

            var result = UsernameAutocomplete.Complete("Alice,Bob,C", usernames);

            Assert.AreEqual("Alice,Bob,Charlie", result);
        }

        [Test]
        public void Complete_CommaSeparated_WithSpacesAfterComma()
        {
            var usernames = new[] { "Player123", "Player234" };

            var result = UsernameAutocomplete.Complete("Player123, P", usernames);

            Assert.AreEqual("Player123, Player234", result);
        }

        [Test]
        public void Complete_CommaSeparated_EmptyLastSegment_ReturnsFirstRemaining()
        {
            // TestUsernames: Player123, Player234, Admin, PlayerOne
            // Player123 excluded, remaining: Player234, Admin, PlayerOne
            // No common prefix, returns first remaining
            var result = UsernameAutocomplete.Complete("Player123,", TestUsernames);

            Assert.AreEqual("Player123,Player234", result);
        }

        [Test]
        public void Complete_CommaSeparated_NoRemainingMatches_ReturnsInput()
        {
            var usernames = new[] { "Player123" };

            var result = UsernameAutocomplete.Complete("Player123,P", usernames);

            // Player123 is excluded, no other matches for "P"
            Assert.AreEqual("Player123,P", result);
        }

        [Test]
        public void Complete_TwoTabs_FirstCommonPrefixThenFirstMatch()
        {
            var usernames = new[] { "Player123", "Player234" };

            // First tab: "P" -> "Player"
            var firstTab = UsernameAutocomplete.Complete("P", usernames);
            Assert.AreEqual("Player", firstTab);

            // Second tab: "Player" -> "Player123" (first match)
            var secondTab = UsernameAutocomplete.Complete("Player", usernames);
            Assert.AreEqual("Player123", secondTab);
        }

        [Test]
        public void Complete_EmptyInput_NoCommonPrefix_ReturnsFirst()
        {
            var usernames = new[] { "Alice", "Bob" };

            // No common prefix between Alice and Bob, returns first
            var result = UsernameAutocomplete.Complete("", usernames);
            Assert.AreEqual("Alice", result);
        }

        [Test]
        public void Complete_CommaSeparated_WhenCommonPrefixEqualsInput_PicksFirst()
        {
            var usernames = new[] { "Alice", "Bob", "Charlie", "Carol" };

            // "Alice,C" -> common prefix of Charlie/Carol is "C", which equals input
            // So it picks first match immediately
            var result = UsernameAutocomplete.Complete("Alice,C", usernames);
            Assert.AreEqual("Alice,Charlie", result);
        }
    }
}
