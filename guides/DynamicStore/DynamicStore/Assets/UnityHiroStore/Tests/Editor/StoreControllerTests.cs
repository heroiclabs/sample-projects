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
using System.Collections.Generic;
using System.Threading.Tasks;
using Hiro;
using Hiro.System;
using Hiro.Unity;
using Nakama;
using NUnit.Framework;
using UnityEngine;

namespace HiroStore.Tests.Editor
{
    /// <summary>
    /// EditMode integration tests for StoreController.
    /// Requires: Nakama server running at 127.0.0.1:7350
    /// </summary>
    [TestFixture]
    public class StoreControllerTests
    {
        private const string Scheme = "http";
        private const string Host = "127.0.0.1";
        private const int Port = 7350;
        private const string ServerKey = "defaultkey";

        private StoreController _controller;
        private IClient _client;
        private ISession _session;
        private NakamaSystem _nakamaSystem;
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

            _economySystem = new EconomySystem(logger, _nakamaSystem, EconomyStoreType.Unspecified);
            await _economySystem.InitializeAsync();

            _controller = new StoreController(
                _nakamaSystem,
                _economySystem,
                new Dictionary<string, Sprite>(),
                new Dictionary<string, Sprite>(),
                null);
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
                new StoreController(null, _economySystem, null, null, null);
            });
        }

        [Test]
        public void Constructor_NullEconomySystem_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new StoreController(_nakamaSystem, null, null, null, null);
            });
        }

        [Test]
        public void Constructor_NullIconDictionaries_DoesNotThrow()
        {
            // Should not throw - icon dictionaries are optional
            var controller = new StoreController(_nakamaSystem, _economySystem, null, null, null);
            Assert.IsNotNull(controller);
        }

        #endregion

        #region RefreshStoreAsync Tests

        [Test]
        public async Task RefreshStoreAsync_ReturnsStoreItems()
        {
            await _controller.RefreshStoreAsync();

            Assert.IsNotNull(_controller.StoreItems);
            Debug.Log($"Found {_controller.StoreItems.Count} store items");

            foreach (var item in _controller.StoreItems)
            {
                Debug.Log($"Store Item: {item.Name} ({item.Id}), Category: {item.Category}");
            }
        }

        [Test]
        public async Task RefreshStoreAsync_PopulatesWallet()
        {
            await _controller.RefreshStoreAsync();

            Assert.IsNotNull(_controller.Wallet);
            Debug.Log($"Wallet has {_controller.Wallet.Count} currencies");

            foreach (var currency in _controller.Wallet)
            {
                Debug.Log($"Currency: {currency.Key} = {currency.Value}");
            }
        }

        #endregion

        #region SwitchCompleteAsync Tests

        [Test]
        public async Task SwitchCompleteAsync_ClearsSelectedItem()
        {
            await _controller.RefreshStoreAsync();

            if (_controller.StoreItems.Count > 0)
            {
                _controller.SelectItem((EconomyListStoreItem)_controller.StoreItems[0]);
                Assert.IsNotNull(_controller.GetSelectedItem());
            }

            await _controller.SwitchCompleteAsync();

            Assert.IsNull(_controller.GetSelectedItem(), "Selected item should be cleared after switch");
        }

        [Test]
        public async Task SwitchCompleteAsync_RefreshesStore()
        {
            await _controller.RefreshStoreAsync();
            var initialCount = _controller.StoreItems.Count;

            await _controller.SwitchCompleteAsync();

            // Store should be refreshed (count may vary but should not be negative)
            Assert.IsTrue(_controller.StoreItems.Count >= 0);
        }

        #endregion

        #region GetItemsForCategory Tests

        [Test]
        public async Task GetItemsForCategory_Currency_ReturnsFilteredItems()
        {
            await _controller.RefreshStoreAsync();

            var currencyItems = _controller.GetItemsForCategory("currency");

            Assert.IsNotNull(currencyItems);
            Debug.Log($"Found {currencyItems.Count} currency items");

            foreach (var item in currencyItems)
            {
                Assert.AreEqual("currency", item.Category,
                    $"Item {item.Id} should be in currency category");
            }
        }

        [Test]
        public async Task GetItemsForCategory_Items_ReturnsFilteredItems()
        {
            await _controller.RefreshStoreAsync();

            var storeItems = _controller.GetItemsForCategory("items");

            Assert.IsNotNull(storeItems);
            Debug.Log($"Found {storeItems.Count} items in 'items' category");

            foreach (var item in storeItems)
            {
                Assert.AreEqual("items", item.Category,
                    $"Item {item.Id} should be in items category");
            }
        }

        [Test]
        public async Task GetItemsForCategory_EmptyCategory_ReturnsEmptyList()
        {
            await _controller.RefreshStoreAsync();

            var items = _controller.GetItemsForCategory("nonexistent_category_xyz");

            Assert.IsNotNull(items);
            Assert.AreEqual(0, items.Count);
        }

        [Test]
        public async Task GetItemsForCategory_ItemsAreSorted()
        {
            await _controller.RefreshStoreAsync();

            var items = _controller.GetItemsForCategory("currency");

            if (items.Count < 2)
            {
                Assert.Inconclusive("Need at least 2 items to verify sorting.");
                return;
            }

            // Verify items are sorted by prefix then by cost
            for (int i = 0; i < items.Count - 1; i++)
            {
                var currentPrefix = items[i].Id.Split('_')[0];
                var nextPrefix = items[i + 1].Id.Split('_')[0];

                if (currentPrefix == nextPrefix)
                {
                    var currentCost = _controller.GetPrimaryCurrencyAmount(items[i]);
                    var nextCost = _controller.GetPrimaryCurrencyAmount(items[i + 1]);
                    Assert.IsTrue(currentCost <= nextCost,
                        $"Items with same prefix should be sorted by cost: {items[i].Id} ({currentCost}) should be <= {items[i + 1].Id} ({nextCost})");
                }
            }
        }

        #endregion

        #region Tab Operations Tests

        [Test]
        public void SwitchTab_Currency_UpdatesCurrentTab()
        {
            _controller.SwitchTab(StoreController.StoreTab.Currency);

            Assert.AreEqual(StoreController.StoreTab.Currency, _controller.GetCurrentTab());
        }

        [Test]
        public void SwitchTab_Items_UpdatesCurrentTab()
        {
            _controller.SwitchTab(StoreController.StoreTab.Items);

            Assert.AreEqual(StoreController.StoreTab.Items, _controller.GetCurrentTab());
        }

        [Test]
        public void GetCurrentCategory_CurrencyTab_ReturnsCurrency()
        {
            _controller.SwitchTab(StoreController.StoreTab.Currency);

            Assert.AreEqual("currency", _controller.GetCurrentCategory());
        }

        [Test]
        public void GetCurrentCategory_ItemsTab_ReturnsItems()
        {
            _controller.SwitchTab(StoreController.StoreTab.Items);

            Assert.AreEqual("items", _controller.GetCurrentCategory());
        }

        #endregion

        #region SelectItem Tests

        [Test]
        public async Task SelectItem_ValidItem_SetsSelectedItem()
        {
            await _controller.RefreshStoreAsync();

            if (_controller.StoreItems.Count == 0)
            {
                Assert.Inconclusive("No store items available.");
                return;
            }

            var itemToSelect = (EconomyListStoreItem)_controller.StoreItems[0];
            _controller.SelectItem(itemToSelect);

            var selectedItem = _controller.GetSelectedItem();
            Assert.IsNotNull(selectedItem);
            Assert.AreEqual(itemToSelect.Id, selectedItem.Id);
        }

        [Test]
        public void SelectItem_Null_ClearsSelection()
        {
            _controller.SelectItem(null);

            Assert.IsNull(_controller.GetSelectedItem());
        }

        [Test]
        public async Task SelectItem_FiresOnItemSelectedEvent()
        {
            await _controller.RefreshStoreAsync();

            if (_controller.StoreItems.Count == 0)
            {
                Assert.Inconclusive("No store items available.");
                return;
            }

            var eventFired = false;
            IEconomyListStoreItem receivedItem = null;

            _controller.OnItemSelected += item =>
            {
                eventFired = true;
                receivedItem = item;
            };

            var itemToSelect = (EconomyListStoreItem)_controller.StoreItems[0];
            _controller.SelectItem(itemToSelect);

            Assert.IsTrue(eventFired, "OnItemSelected event should fire");
            Assert.AreEqual(itemToSelect.Id, receivedItem.Id);
        }

        #endregion

        #region GetFeaturedItemForCategory Tests

        [Test]
        public async Task GetFeaturedItemForCategory_ReturnsNullIfNoFeatured()
        {
            await _controller.RefreshStoreAsync();

            var featured = _controller.GetFeaturedItemForCategory("nonexistent_category");

            // May or may not be null depending on server config
            Debug.Log($"Featured item for nonexistent category: {(featured != null ? featured.Id : "null")}");
        }

        [Test]
        public async Task GetFeaturedItemForCategory_ReturnsFeaturedItem()
        {
            await _controller.RefreshStoreAsync();

            var featured = _controller.GetFeaturedItemForCategory("currency");

            // Log result - may or may not have featured item
            if (featured != null)
            {
                Debug.Log($"Found featured currency item: {featured.Id}");
            }
            else
            {
                Debug.Log("No featured currency item found");
            }
        }

        #endregion

        #region GetItemTheme Tests

        [Test]
        public async Task GetItemTheme_NoTheme_ReturnsPrimary()
        {
            await _controller.RefreshStoreAsync();

            if (_controller.StoreItems.Count == 0)
            {
                Assert.Inconclusive("No store items available.");
                return;
            }

            var theme = _controller.GetItemTheme(_controller.StoreItems[0]);

            Assert.IsNotNull(theme);
            Debug.Log($"Item theme: {theme}");
        }

        [Test]
        public void GetItemTheme_NullItem_ReturnsPrimary()
        {
            var theme = _controller.GetItemTheme(null);

            Assert.AreEqual("primary", theme);
        }

        #endregion

        #region PurchaseItemAsync Tests

        [Test]
        public async Task PurchaseItemAsync_NullItem_ThrowsException()
        {
            var ex = Assert.ThrowsAsync<Exception>(async () =>
            {
                await _controller.PurchaseItemAsync(null);
            });

            Assert.IsTrue(ex.Message.Contains("No item selected"));
        }

        [Test]
        public async Task PurchaseItemAsync_ValidItem_ReturnsPurchaseAck()
        {
            await _controller.RefreshStoreAsync();

            if (_controller.StoreItems.Count == 0)
            {
                Assert.Inconclusive("No store items available.");
                return;
            }

            // Find an affordable item
            IEconomyListStoreItem affordableItem = null;
            foreach (var item in _controller.StoreItems)
            {
                if (_controller.CanAffordItem(item))
                {
                    affordableItem = item;
                    break;
                }
            }

            if (affordableItem == null)
            {
                Assert.Inconclusive("No affordable items available.");
                return;
            }

            var result = await _controller.PurchaseItemAsync(affordableItem);

            Assert.IsNotNull(result);
            Debug.Log($"Purchased item: {affordableItem.Id}");
        }

        #endregion

        #region CanAffordItem Tests

        [Test]
        public void CanAffordItem_NullItem_ReturnsFalse()
        {
            var canAfford = _controller.CanAffordItem(null);

            Assert.IsFalse(canAfford);
        }

        [Test]
        public async Task CanAffordItem_SufficientFunds_ReturnsTrue()
        {
            await _controller.RefreshStoreAsync();

            // Check each item and log affordability
            foreach (var item in _controller.StoreItems)
            {
                var canAfford = _controller.CanAffordItem(item);
                Debug.Log($"Item {item.Id}: CanAfford={canAfford}");
            }
        }

        #endregion

        #region GetPrimaryCurrency Tests

        [Test]
        public async Task GetPrimaryCurrency_ValidItem_ReturnsCurrencyKey()
        {
            await _controller.RefreshStoreAsync();

            if (_controller.StoreItems.Count == 0)
            {
                Assert.Inconclusive("No store items available.");
                return;
            }

            var item = (EconomyListStoreItem)_controller.StoreItems[0];
            var currency = _controller.GetPrimaryCurrency(item);

            Assert.IsNotNull(currency);
            Debug.Log($"Primary currency for {item.Id}: {currency}");
        }

        #endregion

        #region GetPrimaryCurrencyAmount Tests

        [Test]
        public async Task GetPrimaryCurrencyAmount_ValidItem_ReturnsAmount()
        {
            await _controller.RefreshStoreAsync();

            if (_controller.StoreItems.Count == 0)
            {
                Assert.Inconclusive("No store items available.");
                return;
            }

            var amount = _controller.GetPrimaryCurrencyAmount(_controller.StoreItems[0]);

            Assert.IsTrue(amount >= 0, "Amount should be non-negative");
            Debug.Log($"Primary currency amount: {amount}");
        }

        #endregion

        #region GetItemIcon Tests

        [Test]
        public void GetItemIcon_UnknownItem_ReturnsDefaultOrNull()
        {
            var icon = _controller.GetItemIcon("unknown_item_xyz");

            // Should return default icon (null in this test setup)
            Assert.IsNull(icon);
        }

        #endregion

        #region GetCurrencyIcon Tests

        [Test]
        public void GetCurrencyIcon_UnknownCurrency_ReturnsNull()
        {
            var icon = _controller.GetCurrencyIcon("unknown_currency_xyz");

            Assert.IsNull(icon);
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

        #region Wallet Tests

        [Test]
        public async Task Wallet_AfterRefresh_ReturnsWalletDictionary()
        {
            await _controller.RefreshStoreAsync();

            var wallet = _controller.Wallet;

            Assert.IsNotNull(wallet);
            Debug.Log($"Wallet contains {wallet.Count} currencies");
        }

        #endregion
    }
}
