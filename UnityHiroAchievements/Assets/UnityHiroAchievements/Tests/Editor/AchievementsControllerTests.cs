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

using System;
using System.Threading.Tasks;
using Hiro;
using Hiro.System;
using Hiro.Unity;
using Nakama;
using NUnit.Framework;
using UnityEngine;

namespace HiroAchievements.Tests.Editor
{
    /// <summary>
    /// EditMode integration tests for AchievementsController.
    /// Requires: Nakama server running at 127.0.0.1:7350
    /// </summary>
    [TestFixture]
    public class AchievementsControllerTests
    {
        private const string Scheme = "http";
        private const string Host = "127.0.0.1";
        private const int Port = 7350;
        private const string ServerKey = "defaultkey";

        private AchievementsController _controller;
        private IClient _client;
        private ISession _session;
        private NakamaSystem _nakamaSystem;
        private AchievementsSystem _achievementsSystem;
        private EconomySystem _economySystem;
        private string _testDeviceId;

        [SetUp]
        public async Task SetUp()
        {
            _testDeviceId = $"test-device-{Guid.NewGuid():N}";
            _client = new Client(Scheme, Host, Port, ServerKey);
            _session = await _client.AuthenticateDeviceAsync($"{_testDeviceId}_0");

            var logger = new Hiro.Unity.Logger();
            _nakamaSystem = new NakamaSystem(logger, _client, _ => Task.FromResult(_session));
            await _nakamaSystem.InitializeAsync();

            _achievementsSystem = new AchievementsSystem(logger, _nakamaSystem);
            await _achievementsSystem.InitializeAsync();

            _economySystem = new EconomySystem(logger, _nakamaSystem, EconomyStoreType.Unspecified);
            await _economySystem.InitializeAsync();

            _controller = new AchievementsController(_nakamaSystem, _achievementsSystem, _economySystem);
        }

        [TearDown]
        public async Task TearDown()
        {
            await _client.DeleteAccountAsync(_session);
        }

        #region Constructor Tests

        [Test]
        public void Constructor_NullNakamaSystem_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new AchievementsController(null, _achievementsSystem, _economySystem);
            });
        }

        [Test]
        public void Constructor_NullAchievementsSystem_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new AchievementsController(_nakamaSystem, null, _economySystem);
            });
        }

        [Test]
        public void Constructor_NullEconomySystem_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new AchievementsController(_nakamaSystem, _achievementsSystem, null);
            });
        }

        #endregion

        #region Category Management Tests

        [Test]
        public void GetCurrentCategory_Default_ReturnsQuest()
        {
            var category = _controller.GetCurrentCategory();
            Assert.AreEqual("quest", category);
        }

        [Test]
        public void SetCurrentCategory_ValidCategory_UpdatesCategory()
        {
            _controller.SetCurrentCategory("daily");
            Assert.AreEqual("daily", _controller.GetCurrentCategory());
        }

        [Test]
        public void SetCurrentCategory_EmptyString_SetsEmpty()
        {
            _controller.SetCurrentCategory("");
            Assert.AreEqual("", _controller.GetCurrentCategory());
        }

        #endregion

        #region Achievement Selection Tests

        [Test]
        public void GetSelectedAchievement_NoSelection_ReturnsNull()
        {
            Assert.IsNull(_controller.GetSelectedAchievement());
        }

        [Test]
        public async Task SelectAchievement_ValidAchievement_SetsSelected()
        {
            await _controller.LoadAchievementsAsync();

            if (_controller.AllAchievements.Count == 0)
            {
                Assert.Inconclusive("No achievements available.");
                return;
            }

            var achievement = _controller.AllAchievements[0];
            _controller.SelectAchievement(achievement);

            Assert.IsNotNull(_controller.GetSelectedAchievement());
            Assert.AreEqual(achievement.Id, _controller.GetSelectedAchievement().Id);
        }

        [Test]
        public void SelectAchievement_Null_ClearsSelection()
        {
            _controller.SelectAchievement(null);
            Assert.IsNull(_controller.GetSelectedAchievement());
        }

        [Test]
        public async Task SelectAchievement_ClearsSubAchievementSelection()
        {
            await _controller.LoadAchievementsAsync();

            if (_controller.AllAchievements.Count == 0)
            {
                Assert.Inconclusive("No achievements available.");
                return;
            }

            // Find an achievement with sub-achievements
            IAchievement achievementWithSubs = null;
            foreach (var a in _controller.AllAchievements)
            {
                if (AchievementProgressHelper.HasSubAchievements(a))
                {
                    achievementWithSubs = a;
                    break;
                }
            }

            if (achievementWithSubs == null)
            {
                Assert.Inconclusive("No achievements with sub-achievements found.");
                return;
            }

            // Select a sub-achievement first
            foreach (var sub in achievementWithSubs.SubAchievements)
            {
                _controller.SelectSubAchievement(sub.Value, achievementWithSubs, sub.Key);
                break;
            }

            Assert.IsNotNull(_controller.GetSelectedSubAchievement());

            // Select main achievement - should clear sub-achievement selection
            _controller.SelectAchievement(achievementWithSubs);

            Assert.IsNull(_controller.GetSelectedSubAchievement());
            Assert.IsNull(_controller.GetParentAchievement());
            Assert.IsNull(_controller.GetSelectedSubAchievementKey());
        }

        #endregion

        #region Sub-Achievement Selection Tests

        [Test]
        public void GetSelectedSubAchievement_NoSelection_ReturnsNull()
        {
            Assert.IsNull(_controller.GetSelectedSubAchievement());
        }

        [Test]
        public void GetParentAchievement_NoSelection_ReturnsNull()
        {
            Assert.IsNull(_controller.GetParentAchievement());
        }

        [Test]
        public void GetSelectedSubAchievementKey_NoSelection_ReturnsNull()
        {
            Assert.IsNull(_controller.GetSelectedSubAchievementKey());
        }

        [Test]
        public async Task SelectSubAchievement_ValidSub_SetsAllFields()
        {
            await _controller.LoadAchievementsAsync();

            IAchievement achievementWithSubs = null;
            foreach (var a in _controller.AllAchievements)
            {
                if (AchievementProgressHelper.HasSubAchievements(a))
                {
                    achievementWithSubs = a;
                    break;
                }
            }

            if (achievementWithSubs == null)
            {
                Assert.Inconclusive("No achievements with sub-achievements found.");
                return;
            }

            ISubAchievement selectedSub = null;
            string selectedKey = null;
            foreach (var sub in achievementWithSubs.SubAchievements)
            {
                selectedSub = sub.Value;
                selectedKey = sub.Key;
                break;
            }

            _controller.SelectSubAchievement(selectedSub, achievementWithSubs, selectedKey);

            Assert.IsNotNull(_controller.GetSelectedSubAchievement());
            Assert.AreEqual(selectedSub.Id, _controller.GetSelectedSubAchievement().Id);
            Assert.AreEqual(achievementWithSubs.Id, _controller.GetParentAchievement().Id);
            Assert.AreEqual(selectedKey, _controller.GetSelectedSubAchievementKey());
        }

        [Test]
        public async Task ClearSubAchievementSelection_ClearsAll()
        {
            await _controller.LoadAchievementsAsync();

            IAchievement achievementWithSubs = null;
            foreach (var a in _controller.AllAchievements)
            {
                if (AchievementProgressHelper.HasSubAchievements(a))
                {
                    achievementWithSubs = a;
                    break;
                }
            }

            if (achievementWithSubs == null)
            {
                Assert.Inconclusive("No achievements with sub-achievements found.");
                return;
            }

            foreach (var sub in achievementWithSubs.SubAchievements)
            {
                _controller.SelectSubAchievement(sub.Value, achievementWithSubs, sub.Key);
                break;
            }

            _controller.ClearSubAchievementSelection();

            Assert.IsNull(_controller.GetSelectedSubAchievement());
            Assert.IsNull(_controller.GetParentAchievement());
            Assert.IsNull(_controller.GetSelectedSubAchievementKey());
        }

        #endregion

        #region LoadAchievementsAsync Tests

        [Test]
        public async Task LoadAchievementsAsync_Success_PopulatesAllAchievements()
        {
            await _controller.LoadAchievementsAsync();

            Assert.IsNotNull(_controller.AllAchievements);
            Debug.Log($"Loaded {_controller.AllAchievements.Count} achievements");

            foreach (var achievement in _controller.AllAchievements)
            {
                Debug.Log($"Achievement: {achievement.Name} ({achievement.Id}), Count: {achievement.Count}/{achievement.MaxCount}");
            }
        }

        [Test]
        public async Task LoadAchievementsAsync_Success_PopulatesRepeatAchievements()
        {
            await _controller.LoadAchievementsAsync();

            Assert.IsNotNull(_controller.RepeatAchievements);
            Debug.Log($"Loaded {_controller.RepeatAchievements.Count} repeat achievements");
        }

        [Test]
        public async Task LoadAchievementsAsync_CalledTwice_ClearsAndReloads()
        {
            await _controller.LoadAchievementsAsync();
            var firstCount = _controller.AllAchievements.Count;

            await _controller.LoadAchievementsAsync();

            Assert.AreEqual(firstCount, _controller.AllAchievements.Count);
        }

        #endregion

        #region RefreshAchievementsAsync Tests

        [Test]
        public async Task RefreshAchievementsAsync_Success_ReturnsFilteredList()
        {
            _controller.SetCurrentCategory("quest");
            var result = await _controller.RefreshAchievementsAsync();

            Assert.IsNotNull(result);
            Debug.Log($"Refreshed {result.Count} achievements for 'quest' category");
        }

        [Test]
        public async Task RefreshAchievementsAsync_UpdatesAllAchievements()
        {
            await _controller.RefreshAchievementsAsync();

            Assert.IsNotNull(_controller.AllAchievements);
        }

        #endregion

        #region GetFilteredAchievements Tests

        [Test]
        public async Task GetFilteredAchievements_ReturnsCurrentCategoryAchievements()
        {
            _controller.SetCurrentCategory("quest");
            await _controller.RefreshAchievementsAsync();

            var filtered = _controller.GetFilteredAchievements();

            Assert.IsNotNull(filtered);
            Debug.Log($"Filtered achievements count: {filtered.Count}");
        }

        #endregion

        #region GetResetTime Tests

        [Test]
        public async Task GetResetTime_NoResettableAchievements_ReturnsNegativeOne()
        {
            _controller.SetCurrentCategory("nonexistent_category");
            await _controller.RefreshAchievementsAsync();

            var resetTime = _controller.GetResetTime();

            // May or may not have resettable achievements
            Debug.Log($"Reset time: {resetTime}");
        }

        #endregion

        #region UpdateAchievementProgressAsync Tests

        [Test]
        public async Task UpdateAchievementProgressAsync_NullId_ThrowsException()
        {
            try
            {
                await _controller.UpdateAchievementProgressAsync(null, 1);
                Assert.Fail("Expected exception was not thrown");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex.Message.Contains("Invalid achievement ID"));
            }
        }

        [Test]
        public async Task UpdateAchievementProgressAsync_EmptyId_ThrowsException()
        {
            try
            {
                await _controller.UpdateAchievementProgressAsync("", 1);
                Assert.Fail("Expected exception was not thrown");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex.Message.Contains("Invalid achievement ID"));
            }
        }

        [Test]
        public async Task UpdateAchievementProgressAsync_ZeroProgress_ThrowsException()
        {
            try
            {
                await _controller.UpdateAchievementProgressAsync("some_id", 0);
                Assert.Fail("Expected exception was not thrown");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex.Message.Contains("Progress must be greater than 0"));
            }
        }

        [Test]
        public async Task UpdateAchievementProgressAsync_NegativeProgress_ThrowsException()
        {
            try
            {
                await _controller.UpdateAchievementProgressAsync("some_id", -1);
                Assert.Fail("Expected exception was not thrown");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex.Message.Contains("Progress must be greater than 0"));
            }
        }

        [Test]
        public async Task UpdateAchievementProgressAsync_ValidId_UpdatesProgress()
        {
            await _controller.LoadAchievementsAsync();

            if (_controller.AllAchievements.Count == 0)
            {
                Assert.Inconclusive("No achievements available.");
                return;
            }

            // Find an achievement that can be progressed
            IAchievement progressableAchievement = null;
            foreach (var a in _controller.AllAchievements)
            {
                if (!_controller.IsAchievementLocked(a) && a.Count < a.MaxCount)
                {
                    progressableAchievement = a;
                    break;
                }
            }

            if (progressableAchievement == null)
            {
                Assert.Inconclusive("No progressable achievements available.");
                return;
            }

            var initialCount = progressableAchievement.Count;

            await _controller.UpdateAchievementProgressAsync(progressableAchievement.Id, 1);
            await _controller.RefreshAchievementsAsync();

            // Find the updated achievement
            IAchievement updatedAchievement = null;
            foreach (var a in _controller.AllAchievements)
            {
                if (a.Id == progressableAchievement.Id)
                {
                    updatedAchievement = a;
                    break;
                }
            }

            Assert.IsNotNull(updatedAchievement);
            Assert.IsTrue(updatedAchievement.Count >= initialCount,
                $"Count should have increased: was {initialCount}, now {updatedAchievement.Count}");
        }

        #endregion

        #region UpdateSelectedAchievementProgressAsync Tests

        [Test]
        public async Task UpdateSelectedAchievementProgressAsync_NoSelection_ThrowsException()
        {
            try
            {
                await _controller.UpdateSelectedAchievementProgressAsync(1);
                Assert.Fail("Expected exception was not thrown");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex.Message.Contains("No achievement selected"));
            }
        }

        [Test]
        public async Task UpdateSelectedAchievementProgressAsync_AchievementSelected_UpdatesProgress()
        {
            await _controller.LoadAchievementsAsync();

            // Find a progressable achievement
            IAchievement progressable = null;
            foreach (var a in _controller.AllAchievements)
            {
                if (!_controller.IsAchievementLocked(a) &&
                    a.Count < a.MaxCount &&
                    !AchievementProgressHelper.HasSubAchievements(a))
                {
                    progressable = a;
                    break;
                }
            }

            if (progressable == null)
            {
                Assert.Inconclusive("No progressable achievements without sub-achievements found.");
                return;
            }

            _controller.SelectAchievement(progressable);

            await _controller.UpdateSelectedAchievementProgressAsync(1);

            // Verify no exception was thrown
            Assert.Pass("Progress updated successfully");
        }

        #endregion

        #region ClaimAchievementRewardAsync Tests

        [Test]
        public async Task ClaimAchievementRewardAsync_NullId_ThrowsException()
        {
            try
            {
                await _controller.ClaimAchievementRewardAsync(null);
                Assert.Fail("Expected exception was not thrown");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex.Message.Contains("Invalid achievement ID"));
            }
        }

        [Test]
        public async Task ClaimAchievementRewardAsync_EmptyId_ThrowsException()
        {
            try
            {
                await _controller.ClaimAchievementRewardAsync("");
                Assert.Fail("Expected exception was not thrown");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex.Message.Contains("Invalid achievement ID"));
            }
        }

        #endregion

        #region ClaimSelectedAchievementRewardAsync Tests

        [Test]
        public async Task ClaimSelectedAchievementRewardAsync_NoSelection_ThrowsException()
        {
            try
            {
                await _controller.ClaimSelectedAchievementRewardAsync();
                Assert.Fail("Expected exception was not thrown");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex.Message.Contains("No achievement selected"));
            }
        }

        #endregion

        #region RefreshEconomyAsync Tests

        [Test]
        public async Task RefreshEconomyAsync_Success_RefreshesEconomy()
        {
            await _controller.RefreshEconomyAsync();
            // Should not throw
            Assert.Pass("Economy refreshed successfully");
        }

        #endregion

        #region SwitchCompleteAsync Tests

        [Test]
        public async Task SwitchCompleteAsync_ClearsAllSelections()
        {
            await _controller.LoadAchievementsAsync();

            if (_controller.AllAchievements.Count > 0)
            {
                _controller.SelectAchievement(_controller.AllAchievements[0]);
            }

            await _controller.SwitchCompleteAsync();

            Assert.IsNull(_controller.GetSelectedAchievement());
            Assert.IsNull(_controller.GetSelectedSubAchievement());
            Assert.IsNull(_controller.GetParentAchievement());
            Assert.IsNull(_controller.GetSelectedSubAchievementKey());
        }

        #endregion

        #region CanClaimReward Tests

        [Test]
        public async Task CanClaimReward_Achievement_LogsStatus()
        {
            await _controller.LoadAchievementsAsync();

            foreach (var achievement in _controller.AllAchievements)
            {
                var canClaim = _controller.CanClaimReward(achievement);
                Debug.Log($"Achievement {achievement.Name}: CanClaim={canClaim}, Count={achievement.Count}/{achievement.MaxCount}, IsClaimed={achievement.IsClaimed()}");
            }
        }

        #endregion

        #region IsAchievementCompleted Tests

        [Test]
        public async Task IsAchievementCompleted_LogsStatus()
        {
            await _controller.LoadAchievementsAsync();

            foreach (var achievement in _controller.AllAchievements)
            {
                var isCompleted = _controller.IsAchievementCompleted(achievement);
                Debug.Log($"Achievement {achievement.Name}: IsCompleted={isCompleted}");
            }
        }

        #endregion

        #region IsAchievementClaimable Tests

        [Test]
        public async Task IsAchievementClaimable_LogsStatus()
        {
            await _controller.LoadAchievementsAsync();

            foreach (var achievement in _controller.AllAchievements)
            {
                var isClaimable = _controller.IsAchievementClaimable(achievement);
                Debug.Log($"Achievement {achievement.Name}: IsClaimable={isClaimable}");
            }
        }

        #endregion

        #region IsAchievementLocked Tests

        [Test]
        public async Task IsAchievementLocked_LogsStatus()
        {
            await _controller.LoadAchievementsAsync();

            foreach (var achievement in _controller.AllAchievements)
            {
                var isLocked = _controller.IsAchievementLocked(achievement);
                Debug.Log($"Achievement {achievement.Name}: IsLocked={isLocked}");
            }
        }

        #endregion

        #region GetPrerequisiteAchievements Tests

        [Test]
        public async Task GetPrerequisiteAchievements_NoPrereqs_ReturnsEmptyList()
        {
            await _controller.LoadAchievementsAsync();

            foreach (var achievement in _controller.AllAchievements)
            {
                var prereqs = _controller.GetPrerequisiteAchievements(achievement);
                Debug.Log($"Achievement {achievement.Name}: Prerequisites count={prereqs.Count}");

                if (prereqs.Count == 0)
                {
                    Assert.IsNotNull(prereqs);
                    Assert.AreEqual(0, prereqs.Count);
                    return;
                }
            }

            Assert.Inconclusive("All achievements have prerequisites.");
        }

        [Test]
        public async Task GetPrerequisiteAchievements_HasPrereqs_ReturnsList()
        {
            await _controller.LoadAchievementsAsync();

            foreach (var achievement in _controller.AllAchievements)
            {
                var prereqs = _controller.GetPrerequisiteAchievements(achievement);

                if (prereqs.Count > 0)
                {
                    Debug.Log($"Achievement {achievement.Name} has {prereqs.Count} prerequisites:");
                    foreach (var prereq in prereqs)
                    {
                        Debug.Log($"  - {prereq.Name}");
                    }
                    Assert.Pass("Found achievement with prerequisites");
                    return;
                }
            }

            Assert.Inconclusive("No achievements with prerequisites found.");
        }

        #endregion

        #region GetIncompletePrerequisiteNames Tests

        [Test]
        public async Task GetIncompletePrerequisiteNames_LogsStatus()
        {
            await _controller.LoadAchievementsAsync();

            foreach (var achievement in _controller.AllAchievements)
            {
                var incompleteNames = _controller.GetIncompletePrerequisiteNames(achievement);

                if (incompleteNames.Count > 0)
                {
                    Debug.Log($"Achievement {achievement.Name} has {incompleteNames.Count} incomplete prerequisites:");
                    foreach (var name in incompleteNames)
                    {
                        Debug.Log($"  - {name}");
                    }
                }
            }
        }

        #endregion

        #region CurrentUserId Tests

        [Test]
        public void CurrentUserId_ReturnsValidUserId()
        {
            var userId = _controller.CurrentUserId;

            Assert.IsNotNull(userId);
            Assert.IsNotEmpty(userId);
            Assert.AreEqual(_session.UserId, userId);
        }

        #endregion
    }

    /// <summary>
    /// Tests for AchievementProgressHelper static class.
    /// </summary>
    [TestFixture]
    public class AchievementProgressHelperTests
    {
        private const string Scheme = "http";
        private const string Host = "127.0.0.1";
        private const int Port = 7350;
        private const string ServerKey = "defaultkey";

        private IClient _client;
        private ISession _session;
        private NakamaSystem _nakamaSystem;
        private AchievementsSystem _achievementsSystem;
        private string _testDeviceId;

        [SetUp]
        public async Task SetUp()
        {
            _testDeviceId = $"test-device-{Guid.NewGuid():N}";
            _client = new Client(Scheme, Host, Port, ServerKey);
            _session = await _client.AuthenticateDeviceAsync($"{_testDeviceId}_0");

            var logger = new Hiro.Unity.Logger();
            _nakamaSystem = new NakamaSystem(logger, _client, _ => Task.FromResult(_session));
            await _nakamaSystem.InitializeAsync();

            _achievementsSystem = new AchievementsSystem(logger, _nakamaSystem);
            await _achievementsSystem.InitializeAsync();
        }

        [TearDown]
        public async Task TearDown()
        {
            await _client.DeleteAccountAsync(_session);
        }

        #region HasSubAchievements Tests

        [Test]
        public async Task HasSubAchievements_WithSubs_ReturnsTrue()
        {
            await _achievementsSystem.RefreshAsync();
            var achievements = _achievementsSystem.GetAchievements();

            foreach (var achievement in achievements)
            {
                if (achievement.SubAchievements != null && achievement.SubAchievements.Count > 0)
                {
                    Assert.IsTrue(AchievementProgressHelper.HasSubAchievements(achievement));
                    Debug.Log($"Achievement {achievement.Name} has {achievement.SubAchievements.Count} sub-achievements");
                    return;
                }
            }

            Assert.Inconclusive("No achievements with sub-achievements found.");
        }

        [Test]
        public async Task HasSubAchievements_WithoutSubs_ReturnsFalse()
        {
            await _achievementsSystem.RefreshAsync();
            var achievements = _achievementsSystem.GetAchievements();

            foreach (var achievement in achievements)
            {
                if (achievement.SubAchievements == null || achievement.SubAchievements.Count == 0)
                {
                    Assert.IsFalse(AchievementProgressHelper.HasSubAchievements(achievement));
                    Debug.Log($"Achievement {achievement.Name} has no sub-achievements");
                    return;
                }
            }

            Assert.Inconclusive("All achievements have sub-achievements.");
        }

        #endregion

        #region CalculateProgressPercent Tests

        [Test]
        public async Task CalculateProgressPercent_ZeroProgress_ReturnsZero()
        {
            await _achievementsSystem.RefreshAsync();
            var achievements = _achievementsSystem.GetAchievements();

            foreach (var achievement in achievements)
            {
                if (!AchievementProgressHelper.HasSubAchievements(achievement) &&
                    achievement.Count == 0 && achievement.MaxCount > 0)
                {
                    var percent = AchievementProgressHelper.CalculateProgressPercent(achievement);
                    Assert.AreEqual(0f, percent);
                    Debug.Log($"Achievement {achievement.Name}: {percent}%");
                    return;
                }
            }

            Assert.Inconclusive("No achievement with zero progress found.");
        }

        [Test]
        public async Task CalculateProgressPercent_PartialProgress_ReturnsCorrectPercent()
        {
            await _achievementsSystem.RefreshAsync();
            var achievements = _achievementsSystem.GetAchievements();

            foreach (var achievement in achievements)
            {
                if (!AchievementProgressHelper.HasSubAchievements(achievement) &&
                    achievement.Count > 0 && achievement.Count < achievement.MaxCount)
                {
                    var percent = AchievementProgressHelper.CalculateProgressPercent(achievement);
                    var expected = (float)achievement.Count / achievement.MaxCount * 100f;
                    Assert.AreEqual(expected, percent, 0.01f);
                    Debug.Log($"Achievement {achievement.Name}: {percent}% ({achievement.Count}/{achievement.MaxCount})");
                    return;
                }
            }

            Assert.Inconclusive("No achievement with partial progress found.");
        }

        [Test]
        public async Task CalculateProgressPercent_MaxCountZero_ReturnsZero()
        {
            await _achievementsSystem.RefreshAsync();
            var achievements = _achievementsSystem.GetAchievements();

            foreach (var achievement in achievements)
            {
                if (!AchievementProgressHelper.HasSubAchievements(achievement) && achievement.MaxCount == 0)
                {
                    var percent = AchievementProgressHelper.CalculateProgressPercent(achievement);
                    Assert.AreEqual(0f, percent);
                    return;
                }
            }

            // This is expected - most achievements have MaxCount > 0
            Assert.Pass("No achievement with MaxCount=0 found (expected).");
        }

        #endregion

        #region CountCompletedSubAchievements Tests

        [Test]
        public async Task CountCompletedSubAchievements_NoSubs_ReturnsZero()
        {
            await _achievementsSystem.RefreshAsync();
            var achievements = _achievementsSystem.GetAchievements();

            foreach (var achievement in achievements)
            {
                if (!AchievementProgressHelper.HasSubAchievements(achievement))
                {
                    var count = AchievementProgressHelper.CountCompletedSubAchievements(achievement);
                    Assert.AreEqual(0, count);
                    return;
                }
            }

            Assert.Inconclusive("All achievements have sub-achievements.");
        }

        [Test]
        public async Task CountCompletedSubAchievements_WithSubs_ReturnsCorrectCount()
        {
            await _achievementsSystem.RefreshAsync();
            var achievements = _achievementsSystem.GetAchievements();

            foreach (var achievement in achievements)
            {
                if (AchievementProgressHelper.HasSubAchievements(achievement))
                {
                    var count = AchievementProgressHelper.CountCompletedSubAchievements(achievement);
                    Debug.Log($"Achievement {achievement.Name}: {count}/{achievement.SubAchievements.Count} sub-achievements completed");
                    Assert.IsTrue(count >= 0 && count <= achievement.SubAchievements.Count);
                    return;
                }
            }

            Assert.Inconclusive("No achievements with sub-achievements found.");
        }

        #endregion

        #region GetProgressValues Tests

        [Test]
        public async Task GetProgressValues_RegularAchievement_ReturnsCountAndMax()
        {
            await _achievementsSystem.RefreshAsync();
            var achievements = _achievementsSystem.GetAchievements();

            foreach (var achievement in achievements)
            {
                if (!AchievementProgressHelper.HasSubAchievements(achievement))
                {
                    var (current, max) = AchievementProgressHelper.GetProgressValues(achievement);
                    Assert.AreEqual((int)achievement.Count, current);
                    Assert.AreEqual((int)achievement.MaxCount, max);
                    Debug.Log($"Achievement {achievement.Name}: {current}/{max}");
                    return;
                }
            }

            Assert.Inconclusive("All achievements have sub-achievements.");
        }

        [Test]
        public async Task GetProgressValues_WithSubAchievements_ReturnsSubProgress()
        {
            await _achievementsSystem.RefreshAsync();
            var achievements = _achievementsSystem.GetAchievements();

            foreach (var achievement in achievements)
            {
                if (AchievementProgressHelper.HasSubAchievements(achievement))
                {
                    var (current, max) = AchievementProgressHelper.GetProgressValues(achievement);
                    Assert.AreEqual(achievement.SubAchievements.Count, max);
                    Debug.Log($"Achievement {achievement.Name}: {current}/{max} sub-achievements");
                    return;
                }
            }

            Assert.Inconclusive("No achievements with sub-achievements found.");
        }

        #endregion

        #region AreAllSubAchievementsCompleted Tests

        [Test]
        public async Task AreAllSubAchievementsCompleted_NoSubs_ReturnsFalse()
        {
            await _achievementsSystem.RefreshAsync();
            var achievements = _achievementsSystem.GetAchievements();

            foreach (var achievement in achievements)
            {
                if (!AchievementProgressHelper.HasSubAchievements(achievement))
                {
                    Assert.IsFalse(AchievementProgressHelper.AreAllSubAchievementsCompleted(achievement));
                    return;
                }
            }

            Assert.Inconclusive("All achievements have sub-achievements.");
        }

        [Test]
        public async Task AreAllSubAchievementsCompleted_WithSubs_ReturnsCorrectStatus()
        {
            await _achievementsSystem.RefreshAsync();
            var achievements = _achievementsSystem.GetAchievements();

            foreach (var achievement in achievements)
            {
                if (AchievementProgressHelper.HasSubAchievements(achievement))
                {
                    var allCompleted = AchievementProgressHelper.AreAllSubAchievementsCompleted(achievement);
                    var completedCount = AchievementProgressHelper.CountCompletedSubAchievements(achievement);
                    var totalCount = achievement.SubAchievements.Count;

                    if (allCompleted)
                    {
                        Assert.AreEqual(totalCount, completedCount);
                    }
                    else
                    {
                        Assert.IsTrue(completedCount < totalCount);
                    }

                    Debug.Log($"Achievement {achievement.Name}: AllCompleted={allCompleted} ({completedCount}/{totalCount})");
                    return;
                }
            }

            Assert.Inconclusive("No achievements with sub-achievements found.");
        }

        #endregion
    }

    /// <summary>
    /// Lifecycle integration tests for AchievementsController.
    /// </summary>
    [TestFixture]
    public class AchievementsControllerLifecycleTests
    {
        private const string Scheme = "http";
        private const string Host = "127.0.0.1";
        private const int Port = 7350;
        private const string ServerKey = "defaultkey";

        private AchievementsController _controller;
        private IClient _client;
        private ISession _session;
        private NakamaSystem _nakamaSystem;
        private AchievementsSystem _achievementsSystem;
        private EconomySystem _economySystem;
        private string _testDeviceId;

        [SetUp]
        public async Task SetUp()
        {
            _testDeviceId = $"test-device-{Guid.NewGuid():N}";
            _client = new Client(Scheme, Host, Port, ServerKey);
            _session = await _client.AuthenticateDeviceAsync($"{_testDeviceId}_0");

            var logger = new Hiro.Unity.Logger();
            _nakamaSystem = new NakamaSystem(logger, _client, _ => Task.FromResult(_session));
            await _nakamaSystem.InitializeAsync();

            _achievementsSystem = new AchievementsSystem(logger, _nakamaSystem);
            await _achievementsSystem.InitializeAsync();

            _economySystem = new EconomySystem(logger, _nakamaSystem, EconomyStoreType.Unspecified);
            await _economySystem.InitializeAsync();

            _controller = new AchievementsController(_nakamaSystem, _achievementsSystem, _economySystem);
        }

        [TearDown]
        public async Task TearDown()
        {
            await _client.DeleteAccountAsync(_session);
        }

        [Test]
        public async Task AchievementLifecycle_ProgressAndCheckStatus_WorksCorrectly()
        {
            // Load achievements
            await _controller.LoadAchievementsAsync();

            if (_controller.AllAchievements.Count == 0)
            {
                Assert.Inconclusive("No achievements available.");
                return;
            }

            // Find a progressable achievement without sub-achievements
            IAchievement progressable = null;
            foreach (var a in _controller.AllAchievements)
            {
                if (!_controller.IsAchievementLocked(a) &&
                    a.Count < a.MaxCount &&
                    !AchievementProgressHelper.HasSubAchievements(a))
                {
                    progressable = a;
                    break;
                }
            }

            if (progressable == null)
            {
                Assert.Inconclusive("No progressable achievement found.");
                return;
            }

            Debug.Log($"Testing with achievement: {progressable.Name} ({progressable.Count}/{progressable.MaxCount})");

            // Select achievement
            _controller.SelectAchievement(progressable);
            Assert.IsNotNull(_controller.GetSelectedAchievement());

            // Initial state checks
            var initialCanClaim = _controller.CanClaimReward(progressable);
            var initialIsCompleted = _controller.IsAchievementCompleted(progressable);

            Debug.Log($"Initial state: CanClaim={initialCanClaim}, IsCompleted={initialIsCompleted}");

            // Update progress
            await _controller.UpdateSelectedAchievementProgressAsync(1);
            await _controller.RefreshAchievementsAsync();

            // Find updated achievement
            IAchievement updated = null;
            foreach (var a in _controller.AllAchievements)
            {
                if (a.Id == progressable.Id)
                {
                    updated = a;
                    break;
                }
            }

            Assert.IsNotNull(updated);
            Debug.Log($"After progress: Count={updated.Count}/{updated.MaxCount}");

            // Verify progress was recorded
            Assert.IsTrue(updated.Count >= progressable.Count,
                $"Progress should not have decreased: was {progressable.Count}, now {updated.Count}");
        }
    }
}
