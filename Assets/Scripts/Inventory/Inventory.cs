using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Inventory : PanelObject
{
	public enum SortType { PerceivedQualityAscending, PerceivedQualityDescending };

	public class AutoTrade
	{
		public string goodName;
		public bool buy;
		public int maxAmount;
		public int buyPrice;
		public bool sell;
		public int minAmount;
		public int sellPrice;

		public AutoTrade(string goodName, bool buy, int maxAmount, int buyPrice, bool sell, int minAmount, int sellPrice)
		{
			this.goodName = goodName;
			this.buy = buy;
			this.maxAmount = maxAmount;
			this.buyPrice = buyPrice;
			this.sell = sell;
			this.minAmount = minAmount;
			this.sellPrice = sellPrice;
		}
	}

	[Tooltip("How much this Inventory can hold by default, e.g. a naked Player.")]
	[SerializeField] private int baseBulkCapacity = 10;
	[Tooltip("How much Items decay per Day when they exceed this Inventories Capacity.")]
	[SerializeField] private float overCapacityDecayPerDay = 0.05f;
	[SerializeField] private RectTransform inventoryEntryPrefab = null;
	[SerializeField] private RectTransform autoTradeEntryPrefab = null;
	private InfoController infoController = null;
	private BuildingController buildingController = null;
	private int money = 0;
	private Dictionary<Good, int> inventoryItems = null;
	private List<Good> sortedInventoryKeys = null;
	private Dictionary<Good, int> reservedInventoryItems = null;
	private Dictionary<string, int> storedAmounts = null;
	private int bulk = 0;
	private int bulkCapacity = 0;
	private Player player = null;
	private bool localPlayerInventory = false;
	private bool localPlayerOwned = false;
	private string townName = null;
	private TMP_Text moneyText = null;
	private bool overCapacityYesterday = false;
	private Dictionary<string, AutoTrade> autoTrades = null;
	private List<string> sortedAutoTrades = null;
	private Market lastMarket = null;

	public static int CompareGoods(Good lho, Good rho)
	{
		if(lho.goodData.needCategory != rho.goodData.needCategory)
		{
			return lho.goodData.needCategory - rho.goodData.needCategory;
		}

		if(lho.goodData.goodName != rho.goodData.goodName)
		{
			return lho.goodData.goodName.CompareTo(rho.goodData.goodName);
		}

		return Mathf.RoundToInt(rho.perceivedQuality * 100.0f) - Mathf.RoundToInt(lho.perceivedQuality * 100.0f);
	}

	private void Awake()
	{
		inventoryItems = new Dictionary<Good, int>();
		sortedInventoryKeys = new List<Good>();
		reservedInventoryItems = new Dictionary<Good, int>();
		storedAmounts = new Dictionary<string, int>();
		bulkCapacity = baseBulkCapacity;
	}

	protected override void Start()
	{
		base.Start();

		TimeController timeController = TimeController.GetInstance();
		timeController.AddDailyUpdateListener(GoodDecay, TimeController.PriorityCategory.GoodDecay);
		timeController.AddDailyUpdateListener(AutoTradeSell, TimeController.PriorityCategory.AutoTradeSell);
		timeController.AddDailyUpdateListener(AutoTradeBuy, TimeController.PriorityCategory.AutoTradeBuy);

		infoController = InfoController.GetInstance();
		buildingController = GetComponentInParent<BuildingController>();
		lastMarket = GetComponentInParent<Market>();
	}

	public void GoodDecay(double time)
	{
		// Daily Decay of Goods
		Good[] inventoryKeys = sortedInventoryKeys.ToArray();
		foreach(Good good in inventoryKeys)
		{
			if(good.goodData.decayPerDay >= Mathf.Epsilon)
			{
				DecayGood(good, good.goodData.decayPerDay);
				panelManager.QueuePanelUpdate(this);
			}
		}

		// Over Capacity Penalty
		// Extra Loop, because the Good List did change on Decay
		if(bulk > bulkCapacity && overCapacityDecayPerDay >= Mathf.Epsilon)
		{
			float currentOvercapacityDecay = GetOvercapacityDecay();
			inventoryKeys = sortedInventoryKeys.ToArray();
			foreach(Good good in inventoryKeys)
			{
				DecayGood(good, currentOvercapacityDecay);
			}

			if(localPlayerOwned)
			{
				if(!overCapacityYesterday)
				{
					string location = gameObject.GetComponentInParent<Town>()?.GetTownName();
					infoController.AddMessage("Your Inventory" + ((location != null) ? (" in " + location) : "") + " is overflowing and Goods are decaying!", true, false);
				}

				overCapacityYesterday = true;

				panelManager.QueuePanelUpdate(this);
			}
		}
		else
		{
			overCapacityYesterday = false;
		}
	}

	public void AutoTradeSell(double time)
	{
		if(autoTrades != null && buildingController.IsWarehouseAdministered(player))
		{
			// Cancel all Sales
			List<Good> reservedInventoryItemKeys = new List<Good>(reservedInventoryItems.Keys);
			foreach(Good reservedInventoryItemKey in reservedInventoryItemKeys)
			{
				lastMarket.CancelSale(reservedInventoryItemKey, reservedInventoryItems[reservedInventoryItemKey]);
			}

			storedAmounts.Clear();
			foreach(AutoTrade autoTrade in autoTrades.Values)
			{
				List<Tuple<Good, int>> inventoryGoods = GetStoredGoods(autoTrade.goodName, SortType.PerceivedQualityDescending);
				int storeAmount = 0;
				foreach(Tuple<Good, int> inventoryGoodEntry in inventoryGoods)
				{
					// Count Items
					storeAmount += inventoryGoodEntry.Item2;

					// Sell Stuff
					if(autoTrade.sell && storeAmount > autoTrade.minAmount)
					{
						int sellAmount = storeAmount - autoTrade.minAmount;
						storeAmount = autoTrade.minAmount;

						lastMarket.PutUpForSale(inventoryGoodEntry.Item1, sellAmount, autoTrade.sellPrice);
					}
				}

				storedAmounts[autoTrade.goodName] = storeAmount;
			}
		}
	}

	public void AutoTradeBuy(double time)
	{
		if(autoTrades != null && buildingController.IsWarehouseAdministered(player))
		{
			foreach(AutoTrade autoTrade in autoTrades.Values)
			{
				// Buy Stuff
				int storeAmount = storedAmounts[autoTrade.goodName];
				if(autoTrade.buy && storeAmount < autoTrade.maxAmount)
				{
					List<Tuple<Good, MarketOffer>> offers = lastMarket.GetSortedOffers(autoTrade.goodName, Market.CompareOfferPrice);
					foreach(Tuple<Good, MarketOffer> offer in offers)
					{
						int buyAmount = Mathf.Min(autoTrade.maxAmount - storeAmount, offer.Item2.count);
						if(lastMarket.Buy(offer.Item1, buyAmount, this))
						{
							storeAmount += buyAmount;
							if(storeAmount >= autoTrade.maxAmount)
							{
								break;
							}
						}
						else if(localPlayerOwned)
						{
							string location = gameObject.GetComponentInParent<Town>()?.GetTownName();
							infoController.AddMessage("Unable to buy " + autoTrade.goodName + ((location != null) ? (" in " + location) : ""), true, false);
						}
					}
				}
			}
		}
	}

	public override void UpdatePanel(RectTransform panel)
	{
		base.UpdatePanel(panel);

		if(townName != null)
		{
			if(!EnsurePlayerPresence())
			{
				return;
			}

			panel.GetChild(0).GetChild(0).GetComponent<TMP_Text>().text = "Inventory - " + townName;
		}

		RectTransform topBar = (RectTransform)panel.GetChild(1);

		if(bulk <= bulkCapacity)
		{
			topBar.GetChild(1).GetComponent<TMP_Text>().text = bulk + "/" + bulkCapacity;
		}
		else
		{
			topBar.GetChild(1).GetComponent<TMP_Text>().text = "<color=red>" + bulk + "/" + bulkCapacity + "</color>";
		}
		topBar.GetChild(3).GetComponent<TMP_Text>().text = money + "G";
		topBar.GetChild(10).GetComponent<TMP_Text>().text = (GetOvercapacityDecay() * 100.0f).ToString("F1") + "%";

		Button moneyTransferButton = topBar.GetChild(7).GetComponent<Button>();
		Inventory alternativeInventory;
		if(localPlayerInventory)
		{
			alternativeInventory = transform.parent.GetComponentInChildren<BuildingController>()?.GetWarehouseInventory(player.GetPlayerName());
			moneyTransferButton.GetComponentInChildren<TMP_Text>().text = "Deposit";
		}
		else
		{
			alternativeInventory = transform.parent.parent.GetComponentInChildren<Player>()?.GetInventory();
			moneyTransferButton.GetComponentInChildren<TMP_Text>().text = "Withdraw";
		}
		if(alternativeInventory != null)
		{
			TMP_InputField amountField = topBar.GetChild(4).GetComponent<TMP_InputField>();

			Button allButton = topBar.GetChild(6).GetComponent<Button>();
			allButton.onClick.RemoveAllListeners();
			allButton.onClick.AddListener(delegate
			{
				amountField.text = money.ToString();
			});

			moneyTransferButton.gameObject.SetActive(true);

			moneyTransferButton.onClick.RemoveAllListeners();
			moneyTransferButton.onClick.AddListener(delegate
			{
				TransferMoney(alternativeInventory, Mathf.Clamp(Int32.Parse(amountField.text), 0, money));
			});

			amountField.gameObject.SetActive(true);
			topBar.GetChild(5).gameObject.SetActive(true);
			allButton.gameObject.SetActive(true);
			moneyTransferButton.gameObject.SetActive(true);
		}
		else
		{
			topBar.GetChild(4).gameObject.SetActive(false);
			topBar.GetChild(5).gameObject.SetActive(false);
			topBar.GetChild(6).gameObject.SetActive(false);
			moneyTransferButton.gameObject.SetActive(false);
		}

		Market market = transform.parent.GetComponentInChildren<Market>();  // Get Component from Sibling
		if(market != null)
		{
			Button marketButton = topBar.GetChild(8).GetComponent<Button>();
			marketButton.gameObject.SetActive(true);

			marketButton.onClick.RemoveAllListeners();
			marketButton.onClick.AddListener(delegate
			{
				panelManager.OpenPanel(market);
			});
		}
		else
		{
			topBar.GetChild(8).gameObject.SetActive(false);
		}

		RectTransform inventoryContentParent = (RectTransform)panel.GetChild(3).GetChild(0).GetChild(0);

		// Destroy preemptively and repopulate, because there are 2 different Types of Entries which can't be distinguished easily
		while(inventoryContentParent.childCount > 0)
		{
			Transform child = inventoryContentParent.GetChild(0);
			child.SetParent(null, false);
			GameObject.Destroy(child.gameObject);
		}

		inventoryContentParent.sizeDelta = new Vector2(inventoryContentParent.sizeDelta.x, 0.0f);
		int i = 0;
		float totalHeight = 0.0f;
		foreach(Good good in sortedInventoryKeys)
		{
			RectTransform inventoryEntry = GameObject.Instantiate<RectTransform>(inventoryEntryPrefab, inventoryContentParent);
			inventoryEntry.anchoredPosition = new Vector2(inventoryEntry.anchoredPosition.x, -totalHeight);

			inventoryEntry.GetChild(0).GetComponent<TMP_Text>().text = good.goodData.goodName;
			inventoryEntry.GetChild(1).GetComponent<TMP_Text>().text = Mathf.RoundToInt(good.perceivedQuality * 100.0f) + "%";
			inventoryEntry.GetChild(2).GetComponent<TMP_Text>().text = inventoryItems[good].ToString();
			inventoryEntry.GetChild(3).GetComponent<TMP_Text>().text = (good.goodData.bulk * inventoryItems[good]).ToString();

			TMP_InputField amountField = inventoryEntry.GetChild(4).GetComponent<TMP_InputField>();

			Button allButton = inventoryEntry.GetChild(5).GetComponent<Button>();
			allButton.onClick.RemoveAllListeners();
			allButton.onClick.AddListener(delegate
			{
				amountField.text = inventoryItems[good].ToString();
			});

			if(alternativeInventory != null)
			{
				Button transferButton = inventoryEntry.GetChild(6).GetComponent<Button>();
				transferButton.onClick.RemoveAllListeners();
				transferButton.onClick.AddListener(delegate
				{
					int amount = Mathf.Clamp(Int32.Parse(amountField.text), 0, inventoryItems[good]);
					if(WithdrawGood(good, amount, true))
					{
						alternativeInventory.DepositGood(good, amount);
					}
				});
				transferButton.gameObject.SetActive(true);
			}
			else
			{
				inventoryEntry.GetChild(6).gameObject.SetActive(false);
			}

			Button dumpButton = inventoryEntry.GetChild(7).GetComponent<Button>();
			dumpButton.onClick.RemoveAllListeners();
			dumpButton.onClick.AddListener(delegate
			{
				WithdrawGood(good, Mathf.Clamp(Int32.Parse(amountField.text), 0, inventoryItems[good]), true);
			});

			if(i % 2 == 0)
			{
				inventoryEntry.GetComponent<Image>().enabled = false;
			}

			++i;
			totalHeight += inventoryEntry.sizeDelta.y;
		}

		if(autoTrades != null && buildingController.IsWarehouseAdministered(player))
		{
			// Spacing
			++i;
			totalHeight += 16.0f;

			foreach(string autoTradeName in sortedAutoTrades)
			{
				string localAutoTradeName = autoTradeName;
				AutoTrade localAutoTrade = autoTrades[autoTradeName];

				RectTransform autoTradeEntry = GameObject.Instantiate<RectTransform>(autoTradeEntryPrefab, inventoryContentParent);
				autoTradeEntry.anchoredPosition = new Vector2(autoTradeEntry.anchoredPosition.x, -totalHeight);

				autoTradeEntry.GetChild(1).GetComponent<TMP_Text>().text = localAutoTradeName;

				Button buyButton = autoTradeEntry.GetChild(2).GetComponent<Button>();
				buyButton.onClick.RemoveAllListeners();
				buyButton.onClick.AddListener(delegate
				{
					localAutoTrade.buy = !localAutoTrade.buy;
					panelManager.QueuePanelUpdate(this);
				});

				if(localAutoTrade.buy)
				{
					TMP_InputField maxAmountField = autoTradeEntry.GetChild(4).GetComponent<TMP_InputField>();
					maxAmountField.text = localAutoTrade.maxAmount.ToString();
					maxAmountField.onEndEdit.RemoveAllListeners();
					maxAmountField.onEndEdit.AddListener(delegate
					{
						localAutoTrade.maxAmount = Mathf.Max(Int32.Parse(maxAmountField.text), 0);
					});

					TMP_InputField buyPriceField = autoTradeEntry.GetChild(6).GetComponent<TMP_InputField>();
					buyPriceField.text = localAutoTrade.buyPrice.ToString();
					buyPriceField.onEndEdit.RemoveAllListeners();
					buyPriceField.onEndEdit.AddListener(delegate
					{
						localAutoTrade.buyPrice = Mathf.Max(Int32.Parse(buyPriceField.text), 0);
					});

					maxAmountField.gameObject.SetActive(true);
					buyPriceField.gameObject.SetActive(true);

					autoTradeEntry.GetChild(3).gameObject.SetActive(true);
					autoTradeEntry.GetChild(5).gameObject.SetActive(true);
					autoTradeEntry.GetChild(7).gameObject.SetActive(true);
				}
				else
				{
					autoTradeEntry.GetChild(3).gameObject.SetActive(false);
					autoTradeEntry.GetChild(4).gameObject.SetActive(false);
					autoTradeEntry.GetChild(5).gameObject.SetActive(false);
					autoTradeEntry.GetChild(6).gameObject.SetActive(false);
					autoTradeEntry.GetChild(7).gameObject.SetActive(false);
				}

				Button sellButton = autoTradeEntry.GetChild(8).GetComponent<Button>();
				sellButton.onClick.RemoveAllListeners();
				sellButton.onClick.AddListener(delegate
				{
					localAutoTrade.sell = !localAutoTrade.sell;
					panelManager.QueuePanelUpdate(this);
				});

				if(localAutoTrade.sell)
				{
					TMP_InputField minAmountField = autoTradeEntry.GetChild(10).GetComponent<TMP_InputField>();
					minAmountField.text = localAutoTrade.minAmount.ToString();
					minAmountField.onEndEdit.RemoveAllListeners();
					minAmountField.onEndEdit.AddListener(delegate
					{
						localAutoTrade.minAmount = Mathf.Max(Int32.Parse(minAmountField.text), 0);
					});

					TMP_InputField sellPriceField = autoTradeEntry.GetChild(12).GetComponent<TMP_InputField>();
					sellPriceField.text = localAutoTrade.sellPrice.ToString();
					sellPriceField.onEndEdit.RemoveAllListeners();
					sellPriceField.onEndEdit.AddListener(delegate
					{
						localAutoTrade.sellPrice = Mathf.Max(Int32.Parse(sellPriceField.text), 0);
					});

					minAmountField.gameObject.SetActive(true);
					sellPriceField.gameObject.SetActive(true);

					autoTradeEntry.GetChild(9).gameObject.SetActive(true);
					autoTradeEntry.GetChild(11).gameObject.SetActive(true);
					autoTradeEntry.GetChild(13).gameObject.SetActive(true);
				}
				else
				{
					autoTradeEntry.GetChild(9).gameObject.SetActive(false);
					autoTradeEntry.GetChild(10).gameObject.SetActive(false);
					autoTradeEntry.GetChild(11).gameObject.SetActive(false);
					autoTradeEntry.GetChild(12).gameObject.SetActive(false);
					autoTradeEntry.GetChild(13).gameObject.SetActive(false);
				}

				if(i % 2 == 0)
				{
					autoTradeEntry.GetComponent<Image>().enabled = false;
				}

				++i;
				totalHeight += autoTradeEntry.sizeDelta.y;
			}
		}

		inventoryContentParent.sizeDelta = new Vector2(inventoryContentParent.sizeDelta.x, totalHeight);
	}

	public bool DepositGood(Good good, int amount)
	{
		if(amount <= 0)
		{
			return true;
		}

		good.owner = this;

		if(!inventoryItems.ContainsKey(good))
		{
			bool inserted = false;
			// Stack Goods with similar Quality
			foreach(Good inventoryGood in sortedInventoryKeys)
			{
				if(inventoryGood.goodData.goodName == good.goodData.goodName && Math.Abs(inventoryGood.quality - good.quality) < 0.1f)
				{
					inventoryGood.quality = (inventoryGood.quality * inventoryItems[inventoryGood] + good.quality * amount) / (inventoryItems[inventoryGood] + amount);
					inventoryGood.perceivedQuality = (inventoryGood.perceivedQuality * inventoryItems[inventoryGood] + good.perceivedQuality * amount) / (inventoryItems[inventoryGood] + amount);
					inventoryItems[inventoryGood] += amount;

					inserted = true;
					break;
				}
			}

			if(!inserted)
			{
				inventoryItems[good] = amount;
				sortedInventoryKeys.Add(good);
				sortedInventoryKeys.Sort(CompareGoods);
			}
		}
		else
		{
			inventoryItems[good] += amount;
		}
		bulk += good.goodData.bulk * amount;

		panelManager.QueuePanelUpdate(this);
		if(lastMarket != null)
		{
			panelManager.QueuePanelUpdate(lastMarket);
		}

		return true;
	}

	public bool WithdrawGood(Good good, int amount, bool recalculatePathCost)
	{
		if(inventoryItems.ContainsKey(good) && (inventoryItems[good] - ((reservedInventoryItems.ContainsKey(good)) ? reservedInventoryItems[good] : 0)) >= amount)
		{
			if(inventoryItems[good] - amount > 0)
			{
				inventoryItems[good] -= amount;
			}
			else
			{
				inventoryItems.Remove(good);
				sortedInventoryKeys.Remove(good);
			}

			bulk -= good.goodData.bulk * amount;
			if(bulk < 0)
			{
				bulk = 0;
			}

			panelManager.QueuePanelUpdate(this);
			if(lastMarket != null)
			{
				panelManager.QueuePanelUpdate(lastMarket);
			}

			if(townName == null && recalculatePathCost)
			{
				player.RestartPathIfCheaper();
			}

			return true;
		}

		return false;
	}

	public int WithdrawGoodPartially(Good good, int amount, bool recalculatePathCost)
	{
		if(inventoryItems.ContainsKey(good))
		{
			int partialAmount = Mathf.Min(amount, (inventoryItems[good] - ((reservedInventoryItems.ContainsKey(good)) ? reservedInventoryItems[good] : 0)));
			if(WithdrawGood(good, partialAmount, recalculatePathCost))
			{
				return partialAmount;
			}
		}

		return 0;
	}

	public List<Tuple<Good, int>> WithdrawGoodUnchecked(string goodName, int amount, bool retrieveMaxQuality, bool recalculatePathCost)
	{
		List<Tuple<Good, int>> withdrawnGoods = new List<Tuple<Good, int>>();
		int withdrawnAmount = 0;
		int i = retrieveMaxQuality ? 0 : sortedInventoryKeys.Count - 1;
		while(i >= 0 && i < sortedInventoryKeys.Count && withdrawnAmount < amount)
		{
			if(sortedInventoryKeys[i].goodData.goodName == goodName)
			{
				Good sortedInventoryKey = sortedInventoryKeys[i];
				int goodAmount = WithdrawGoodPartially(sortedInventoryKey, amount - withdrawnAmount, false);
				withdrawnGoods.Add(new Tuple<Good, int>(sortedInventoryKey, goodAmount));
				withdrawnAmount += goodAmount;
			}

			i += retrieveMaxQuality ? 1 : -1;
		}

		if(townName == null && recalculatePathCost)
		{
			player.RestartPathIfCheaper();
		}

		return new List<Tuple<Good, int>>(withdrawnGoods);
	}

	public bool ReserveGood(Good good, int amount)
	{
		bool alreadyReserved = reservedInventoryItems.ContainsKey(good);
		if(inventoryItems.ContainsKey(good) && (inventoryItems[good] - (alreadyReserved ? reservedInventoryItems[good] : 0)) >= amount)
		{
			if(!alreadyReserved)
			{
				reservedInventoryItems.Add(good, amount);
			}
			else
			{
				reservedInventoryItems[good] += amount;
			}

			return true;
		}

		return false;
	}

	public bool UnreserveGood(Good good, int amount)
	{
		if(inventoryItems.ContainsKey(good) && reservedInventoryItems.ContainsKey(good) && reservedInventoryItems[good] >= amount)
		{
			if(reservedInventoryItems[good] > amount)
			{
				reservedInventoryItems[good] -= amount;
			}
			else
			{
				reservedInventoryItems.Remove(good);
			}

			return true;
		}

		Debug.LogWarning("Unreserving " + amount + " " + good.goodData.goodName + " in " + this + " failed!");
		return false;
	}

	private float GetOvercapacityDecay()
	{
		if(bulk > 0 && bulk > bulkCapacity)
		{
			return (1.0f - ((float)bulkCapacity / (float)bulk)) * overCapacityDecayPerDay;
		}
		else
		{
			return 0.0f;
		}
	}

	private bool DecayGood(Good good, float decay)
	{
		if(inventoryItems.ContainsKey(good))
		{
			good.quality -= decay;
			good.perceivedQuality -= decay;

			// Let Good disappear if Quality <= 0%
			if(good.quality <= 0.0f)
			{
				if(lastMarket != null && reservedInventoryItems.ContainsKey(good) && reservedInventoryItems[good] > 0)
				{
					lastMarket.CancelSale(good, reservedInventoryItems[good]);
				}

				if(!WithdrawGood(good, inventoryItems[good], true))
				{
					Debug.LogWarning("Unable to destroy " + good.goodData.goodName + " from " + gameObject + " for Decay!");
					return false;
				}
			}

			return true;
		}

		return false;
	}

	public bool ChangeMoney(int amount)
	{
		if(money + amount >= 0)
		{
			// Change Money
			money += amount;

			// Show Money
			if(localPlayerInventory)
			{
				if(moneyText == null)
				{
					if(panelManager == null)
					{
						panelManager = PanelManager.GetInstance();
					}
					moneyText = panelManager.GetCharacterPanel().GetChild(1).GetComponent<TMP_Text>();
				}
				moneyText.text = money + "G";
			}
			if(localPlayerOwned)
			{
				panelManager.QueuePanelUpdate(this);
				if(lastMarket != null)
				{
					panelManager.QueuePanelUpdate(lastMarket);
				}
			}

			return true;
		}

		return false;
	}

	public bool TransferMoney(Inventory recipient, int amount)
	{
		if(ChangeMoney(-amount))
		{
			recipient.ChangeMoney(amount);
			return true;
		}

		return false;
	}

	public void ChangeBulkCapacity(int amount)
	{
		bulkCapacity += amount;
		panelManager.QueuePanelUpdate(this);
	}

	public bool IsLocalPlayerInventory()
	{
		return localPlayerInventory;
	}

	public int GetBulk()
	{
		return bulk;
	}

	public int GetBulkCapacity()
	{
		return bulkCapacity;
	}

	public int GetMoney()
	{
		return money;
	}

	public List<Good> GetSortedInventoryKeys()
	{
		return sortedInventoryKeys;
	}

	public int GetInventoryAmount(Good good)
	{
		if(inventoryItems.ContainsKey(good))
		{
			return inventoryItems[good] - (reservedInventoryItems.ContainsKey(good) ? reservedInventoryItems[good] : 0);
		}

		return 0;
	}

	public int GetInventoryAmount(string goodName)
	{
		int totalAmount = 0;
		foreach(KeyValuePair<Good, int> goodEntry in inventoryItems)
		{
			if(goodEntry.Key.goodData.goodName == goodName)
			{
				totalAmount += goodEntry.Value - (reservedInventoryItems.ContainsKey(goodEntry.Key) ? reservedInventoryItems[goodEntry.Key] : 0);
			}
		}

		return totalAmount;
	}

	public List<Tuple<Good, int>> GetStoredGoods(string goodName, SortType sortType)
	{
		List<Tuple<Good, int>> goodList = new List<Tuple<Good, int>>();

		foreach(KeyValuePair<Good, int> inventoryEntry in inventoryItems)
		{
			if(inventoryEntry.Key.goodData.goodName == goodName)
			{
				goodList.Add(new Tuple<Good, int>(inventoryEntry.Key, inventoryEntry.Value - (reservedInventoryItems.ContainsKey(inventoryEntry.Key) ? reservedInventoryItems[inventoryEntry.Key] : 0)));
			}
		}

		if(sortType == SortType.PerceivedQualityAscending)
		{
			goodList.Sort(delegate (Tuple<Good, int> lho, Tuple<Good, int> rho)
			{
				float sortResult = lho.Item1.perceivedQuality - rho.Item1.perceivedQuality;
				return (sortResult <= 0.0f) ? Mathf.FloorToInt(sortResult) : Mathf.CeilToInt(sortResult);
			});
		}
		else if(sortType == SortType.PerceivedQualityDescending)
		{
			goodList.Sort(delegate (Tuple<Good, int> lho, Tuple<Good, int> rho)
			{
				float sortResult = rho.Item1.perceivedQuality - lho.Item1.perceivedQuality;
				return (sortResult <= 0.0f) ? Mathf.FloorToInt(sortResult) : Mathf.CeilToInt(sortResult);
			});
		}

		return goodList;
	}

	public Player GetPlayer()
	{
		return player;
	}

	public void SetPlayer(Player player, string townName)
	{
		this.player = player;
		this.townName = townName;

		localPlayerInventory = player.IsLocalPlayer() && townName == null;
		localPlayerOwned = player.IsLocalPlayer();

		if(townName != null)
		{
			GoodData[] goodDataArray = GoodManager.GetInstance().GetGoodData();
			autoTrades = new Dictionary<string, AutoTrade>(goodDataArray.Length);
			sortedAutoTrades = new List<string>(goodDataArray.Length);
			foreach(GoodData goodData in goodDataArray)
			{
				autoTrades.Add(goodData.goodName, new AutoTrade(goodData.goodName, false, 0, 1, false, 0, 1));
				sortedAutoTrades.Add(goodData.goodName);
			}
		}
	}

	// This works, because all Inventories can only sell at one Market at a Time:
	// Warehouses do not move and Player Sales are cancelled automatically when leaving
	public void SetLastMarket(Market market)
	{
		lastMarket = market;
	}
}
