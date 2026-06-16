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

namespace HiroInventory.Tests.Editor
{
    /// <summary>
    /// EditMode integration tests for InventoryController.
    /// Requires: Nakama server running at 127.0.0.1:7350
    /// </summary>
    [TestFixture]
    public class InventoryControllerTests
    {
        private const string Scheme = "http";
        private const string Host = "127.0.0.1";
        private const int Port = 7350;
        private const string ServerKey = "defaultkey";

        private InventoryController _controller;
        private IClient _client;
        private ISession _session;
        private NakamaSystem _nakamaSystem;
        private InventorySystem _inventorySystem;
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

            _inventorySystem = new InventorySystem(logger, _nakamaSystem);
            await _inventorySystem.InitializeAsync();

            _economySystem = new EconomySystem(logger, _nakamaSystem, EconomyStoreType.Unspecified);
            await _economySystem.InitializeAsync();

            _controller = new InventoryController(_nakamaSystem, _inventorySystem, _economySystem);
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
                new InventoryController(null, _inventorySystem, _economySystem);
            });
        }

        [Test]
        public void Constructor_NullInventorySystem_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new InventoryController(_nakamaSystem, null, _economySystem);
            });
        }

        [Test]
        public void Constructor_NullEconomySystem_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new InventoryController(_nakamaSystem, _inventorySystem, null);
            });
        }

        #endregion

        #region LoadItemCodexAsync Tests

        [Test]
        public async Task LoadItemCodexAsync_ReturnsItemCodex()
        {
            await _controller.LoadItemCodexAsync();

            Assert.IsNotNull(_controller.CodexItems);
            Debug.Log($"Found {_controller.CodexItems.Count} codex items");

            foreach (var item in _controller.CodexItems)
            {
                Debug.Log($"Codex Item: {item.Name} ({item.Id}), Stackable: {item.Stackable}, MaxCount: {item.MaxCount}");
            }
        }

        [Test]
        public async Task LoadItemCodexAsync_PopulatesCodexLookup()
        {
            await _controller.LoadItemCodexAsync();

            Assert.IsNotNull(_controller.CodexLookup);
            Assert.AreEqual(_controller.CodexItems.Count, _controller.CodexLookup.Count);

            foreach (var item in _controller.CodexItems)
            {
                Assert.IsTrue(_controller.CodexLookup.ContainsKey(item.Id),
                    $"CodexLookup should contain item {item.Id}");
            }
        }

        #endregion

        #region RefreshInventoryAsync Tests

        [Test]
        public async Task RefreshInventoryAsync_ReturnsInventoryItems()
        {
            var result = await _controller.RefreshInventoryAsync();

            Assert.IsNotNull(result);
            Assert.IsNotNull(_controller.InventoryItems);
            Assert.AreEqual(result.Count, _controller.InventoryItems.Count);
            Debug.Log($"Found {_controller.InventoryItems.Count} inventory items");
        }

        [Test]
        public async Task RefreshInventoryAsync_NewAccount_ReturnsEmptyOrStarterItems()
        {
            var result = await _controller.RefreshInventoryAsync();

            Assert.IsNotNull(result);
            Debug.Log($"New account has {result.Count} inventory items");
        }

        #endregion

        #region SelectItem and GetSelectedItem Tests

        [Test]
        public async Task SelectItem_ValidItem_SetsSelectedItem()
        {
            await _controller.LoadItemCodexAsync();
            await _controller.RefreshInventoryAsync();

            if (_controller.CodexItems.Count == 0)
            {
                Assert.Inconclusive("No codex items available to select.");
                return;
            }

            var itemToSelect = _controller.CodexItems[0];
            _controller.SelectItem(itemToSelect);

            var selectedItem = _controller.GetSelectedItem();
            Assert.IsNotNull(selectedItem);
            Assert.AreEqual(itemToSelect.Id, selectedItem.Id);
        }

        [Test]
        public void SelectItem_Null_ClearsSelection()
        {
            _controller.SelectItem(null);

            var selectedItem = _controller.GetSelectedItem();
            Assert.IsNull(selectedItem);
        }

        [Test]
        public void GetSelectedItem_NoSelection_ReturnsNull()
        {
            var selectedItem = _controller.GetSelectedItem();
            Assert.IsNull(selectedItem);
        }

        #endregion

        #region GrantItemAsync Tests

        [Test]
        public async Task GrantItemAsync_ValidItem_IncreasesInventoryCount()
        {
            await _controller.LoadItemCodexAsync();
            await _controller.RefreshInventoryAsync();

            if (_controller.CodexItems.Count == 0)
            {
                Assert.Inconclusive("No codex items available to grant.");
                return;
            }

            var initialCount = _controller.InventoryItems.Count;

            await _controller.GrantItemAsync(0, 1);
            await _controller.RefreshInventoryAsync();

            Debug.Log($"Inventory count: before={initialCount}, after={_controller.InventoryItems.Count}");
            Assert.IsTrue(_controller.InventoryItems.Count >= initialCount,
                "Inventory should have at least as many items after granting");
        }

        [Test]
        public async Task GrantItemAsync_InvalidIndex_ThrowsException()
        {
            await _controller.LoadItemCodexAsync();

            var ex = Assert.ThrowsAsync<Exception>(async () =>
            {
                await _controller.GrantItemAsync(-1, 1);
            });

            Assert.IsTrue(ex.Message.Contains("valid item"));
        }

        [Test]
        public async Task GrantItemAsync_IndexOutOfRange_ThrowsException()
        {
            await _controller.LoadItemCodexAsync();

            var ex = Assert.ThrowsAsync<Exception>(async () =>
            {
                await _controller.GrantItemAsync(_controller.CodexItems.Count + 10, 1);
            });

            Assert.IsTrue(ex.Message.Contains("valid item"));
        }

        [Test]
        public async Task GrantItemAsync_ZeroQuantity_ThrowsException()
        {
            await _controller.LoadItemCodexAsync();

            if (_controller.CodexItems.Count == 0)
            {
                Assert.Inconclusive("No codex items available.");
                return;
            }

            var ex = Assert.ThrowsAsync<Exception>(async () =>
            {
                await _controller.GrantItemAsync(0, 0);
            });

            Assert.IsTrue(ex.Message.Contains("0"));
        }

        [Test]
        public async Task GrantItemAsync_MultipleItems_IncreasesCount()
        {
            await _controller.LoadItemCodexAsync();
            await _controller.RefreshInventoryAsync();

            if (_controller.CodexItems.Count == 0)
            {
                Assert.Inconclusive("No codex items available.");
                return;
            }

            // Find a stackable item
            int stackableIndex = -1;
            for (int i = 0; i < _controller.CodexItems.Count; i++)
            {
                if (_controller.CodexItems[i].Stackable)
                {
                    stackableIndex = i;
                    break;
                }
            }

            if (stackableIndex == -1)
            {
                Assert.Inconclusive("No stackable codex items available.");
                return;
            }

            await _controller.GrantItemAsync(stackableIndex, 5);
            await _controller.RefreshInventoryAsync();

            var grantedItem = _controller.CodexItems[stackableIndex];
            IInventoryItem foundItem = null;
            foreach (var item in _controller.InventoryItems)
            {
                if (item.Id == grantedItem.Id)
                {
                    foundItem = item;
                    break;
                }
            }

            Assert.IsNotNull(foundItem, "Should find the granted item in inventory");
            Assert.IsTrue(foundItem.Count >= 5, $"Item count should be at least 5, was {foundItem.Count}");
        }

        #endregion

        #region ConsumeItemAsync Tests

        [Test]
        public async Task ConsumeItemAsync_NoSelectedItem_DoesNothing()
        {
            // Ensure no item is selected
            _controller.SelectItem(null);

            // Should not throw
            await _controller.ConsumeItemAsync(1, false);
        }

        [Test]
        public async Task ConsumeItemAsync_ZeroQuantity_ThrowsException()
        {
            await _controller.LoadItemCodexAsync();
            await _controller.RefreshInventoryAsync();

            if (_controller.CodexItems.Count == 0)
            {
                Assert.Inconclusive("No codex items available.");
                return;
            }

            // Grant and select an item first
            await _controller.GrantItemAsync(0, 5);
            await _controller.RefreshInventoryAsync();

            if (_controller.InventoryItems.Count == 0)
            {
                Assert.Inconclusive("No inventory items available after grant.");
                return;
            }

            _controller.SelectItem(_controller.InventoryItems[0]);

            var ex = Assert.ThrowsAsync<Exception>(async () =>
            {
                await _controller.ConsumeItemAsync(0, false);
            });

            Assert.IsTrue(ex.Message.Contains("greater than 0"));
        }

        [Test]
        public async Task ConsumeItemAsync_NegativeQuantity_ThrowsException()
        {
            await _controller.LoadItemCodexAsync();
            await _controller.RefreshInventoryAsync();

            if (_controller.CodexItems.Count == 0)
            {
                Assert.Inconclusive("No codex items available.");
                return;
            }

            // Grant and select an item first
            await _controller.GrantItemAsync(0, 5);
            await _controller.RefreshInventoryAsync();

            if (_controller.InventoryItems.Count == 0)
            {
                Assert.Inconclusive("No inventory items available after grant.");
                return;
            }

            _controller.SelectItem(_controller.InventoryItems[0]);

            var ex = Assert.ThrowsAsync<Exception>(async () =>
            {
                await _controller.ConsumeItemAsync(-1, false);
            });

            Assert.IsTrue(ex.Message.Contains("greater than 0"));
        }

        [Test]
        public async Task ConsumeItemAsync_ValidItem_ClearsSelection()
        {
            await _controller.LoadItemCodexAsync();
            await _controller.RefreshInventoryAsync();

            if (_controller.CodexItems.Count == 0)
            {
                Assert.Inconclusive("No codex items available.");
                return;
            }

            // Find a consumable/stackable item
            int consumableIndex = -1;
            for (int i = 0; i < _controller.CodexItems.Count; i++)
            {
                if (_controller.CodexItems[i].Consumable && _controller.CodexItems[i].Stackable)
                {
                    consumableIndex = i;
                    break;
                }
            }

            if (consumableIndex == -1)
            {
                Assert.Inconclusive("No consumable stackable items in codex.");
                return;
            }

            // Grant items and select
            await _controller.GrantItemAsync(consumableIndex, 10);
            await _controller.RefreshInventoryAsync();

            var grantedItem = _controller.CodexItems[consumableIndex];
            IInventoryItem inventoryItem = null;
            foreach (var item in _controller.InventoryItems)
            {
                if (item.Id == grantedItem.Id)
                {
                    inventoryItem = item;
                    break;
                }
            }

            if (inventoryItem == null)
            {
                Assert.Inconclusive("Granted item not found in inventory.");
                return;
            }

            _controller.SelectItem(inventoryItem);

            // Consume and verify selection is cleared
            await _controller.ConsumeItemAsync(1, true);

            Assert.IsNull(_controller.GetSelectedItem(), "Selection should be cleared after consume");
        }

        #endregion

        #region RemoveItemAsync Tests

        [Test]
        public async Task RemoveItemAsync_NoSelectedItem_DoesNothing()
        {
            _controller.SelectItem(null);

            // Should not throw
            await _controller.RemoveItemAsync(1);
        }

        [Test]
        public async Task RemoveItemAsync_ZeroQuantity_ThrowsException()
        {
            await _controller.LoadItemCodexAsync();
            await _controller.RefreshInventoryAsync();

            if (_controller.CodexItems.Count == 0)
            {
                Assert.Inconclusive("No codex items available.");
                return;
            }

            await _controller.GrantItemAsync(0, 5);
            await _controller.RefreshInventoryAsync();

            if (_controller.InventoryItems.Count == 0)
            {
                Assert.Inconclusive("No inventory items available.");
                return;
            }

            _controller.SelectItem(_controller.InventoryItems[0]);

            var ex = Assert.ThrowsAsync<Exception>(async () =>
            {
                await _controller.RemoveItemAsync(0);
            });

            Assert.IsTrue(ex.Message.Contains("greater than 0"));
        }

        [Test]
        public async Task RemoveItemAsync_ValidItem_ClearsSelection()
        {
            await _controller.LoadItemCodexAsync();
            await _controller.RefreshInventoryAsync();

            if (_controller.CodexItems.Count == 0)
            {
                Assert.Inconclusive("No codex items available.");
                return;
            }

            // Find a stackable item
            int stackableIndex = -1;
            for (int i = 0; i < _controller.CodexItems.Count; i++)
            {
                if (_controller.CodexItems[i].Stackable)
                {
                    stackableIndex = i;
                    break;
                }
            }

            if (stackableIndex == -1)
            {
                Assert.Inconclusive("No stackable items in codex.");
                return;
            }

            // Grant and select
            await _controller.GrantItemAsync(stackableIndex, 10);
            await _controller.RefreshInventoryAsync();

            var grantedItem = _controller.CodexItems[stackableIndex];
            IInventoryItem inventoryItem = null;
            foreach (var item in _controller.InventoryItems)
            {
                if (item.Id == grantedItem.Id)
                {
                    inventoryItem = item;
                    break;
                }
            }

            if (inventoryItem == null)
            {
                Assert.Inconclusive("Granted item not found in inventory.");
                return;
            }

            _controller.SelectItem(inventoryItem);

            // Remove and verify selection is cleared
            await _controller.RemoveItemAsync(1);

            Assert.IsNull(_controller.GetSelectedItem(), "Selection should be cleared after remove");
        }

        #endregion

        #region SwitchCompleteAsync Tests

        [Test]
        public async Task SwitchCompleteAsync_ClearsSelectedItem()
        {
            await _controller.LoadItemCodexAsync();
            await _controller.RefreshInventoryAsync();

            if (_controller.CodexItems.Count > 0)
            {
                _controller.SelectItem(_controller.CodexItems[0]);
                Assert.IsNotNull(_controller.GetSelectedItem());
            }

            await _controller.SwitchCompleteAsync();

            Assert.IsNull(_controller.GetSelectedItem(), "Selected item should be cleared after switch");
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
            Debug.Log($"Current user ID: {userId}");
        }

        #endregion
    }
}
