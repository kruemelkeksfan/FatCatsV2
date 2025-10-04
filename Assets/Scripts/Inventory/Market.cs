using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class MarketOffer
{
	public int price;
	public int count;
	public int lastAmount;

	public MarketOffer(int price, int count)
	{
		this.price = price;
		this.count = count;

		lastAmount = count;
	}
}

public class Market : PanelObject
{
	public static int CompareOfferPrice(Tuple<Good, MarketOffer> lho, Tuple<Good, MarketOffer> rho)
	{
		return lho.Item2.price - rho.Item2.price;
	}

	public static int CompareOfferPerceivedQualityPriceRatio(Tuple<Good, MarketOffer> lho, Tuple<Good, MarketOffer> rho)
	{
		float sortResult = (rho.Item1.perceivedQuality / rho.Item2.price) - (lho.Item1.perceivedQuality / lho.Item2.price);
		return (sortResult <= 0.0f) ? Mathf.FloorToInt(sortResult) : Mathf.CeilToInt(sortResult);
	}

	[SerializeField] private RectTransform marketEntryPrefab = null;
	private Dictionary<Good, MarketOffer> offers = null;
	private PopulationController populationController = null;
	private string townName = "Unknown Town";

	private void Awake()
	{
		offers = new Dictionary<Good, MarketOffer>();
	}

	protected override void Start()
	{
		base.Start();

		populationController = gameObject.GetComponent<PopulationController>();

		townName = gameObject.GetComponent<Town>().GetTownName();
	}

	public override void UpdatePanel(RectTransform panel)
	{
		base.UpdatePanel(panel);

		Inventory playerInventory = EnsurePlayerPresence();
		if(playerInventory == null)
		{
			return;
		}

		panel.GetChild(0).GetChild(0).GetComponent<TMP_Text>().text = "Marketplace - " + townName;

		RectTransform townStatPanel = (RectTransform)panel.GetChild(1);

		townStatPanel.GetChild(1).GetComponent<TMP_Text>().text = populationController.GetTotalPopulation().ToString();
		townStatPanel.GetChild(3).GetComponent<TMP_Text>().text = populationController.CalculateAverageIncome().ToString();
		townStatPanel.GetChild(5).GetComponent<TMP_Text>().text = populationController.CalculateAverageSavings().ToString();

		RectTransform marketContentParent = (RectTransform)panel.GetChild(2).GetChild(0).GetChild(0);

		// First use HashSet for Uniqueness of Values
		HashSet<Good> marketItems = new HashSet<Good>();
		Dictionary<Good, MarketOffer>.KeyCollection offerGoods = offers.Keys;
		foreach(Good offerGood in offerGoods)
		{
			marketItems.Add(offerGood);
		}
		List<Good> playerInventoryItems = playerInventory.GetSortedInventoryKeys();
		foreach(Good playerInventoryItem in playerInventoryItems)
		{
			marketItems.Add(playerInventoryItem);
		}
		// Later use List for Sorting
		List<Good> sortedMarketItems = new List<Good>(marketItems);
		sortedMarketItems.Sort(Inventory.CompareGoods);

		while(marketContentParent.childCount > 0)
		{
			Transform child = marketContentParent.GetChild(0);
			child.SetParent(null, false);
			GameObject.Destroy(child.gameObject);
		}

		marketContentParent.sizeDelta = new Vector2(marketContentParent.sizeDelta.x, 0);
		int i = 1;
		foreach(Good good in sortedMarketItems)
		{
			Good localGood = good;

			RectTransform marketEntry = GameObject.Instantiate<RectTransform>(marketEntryPrefab, marketContentParent);
			marketEntry.anchoredPosition = new Vector2(marketEntry.anchoredPosition.x, -marketEntry.sizeDelta.y * i);
			marketContentParent.sizeDelta += new Vector2(0.0f, marketEntry.sizeDelta.y * i);

			marketEntry.GetChild(0).GetComponent<TMP_Text>().text = good.goodData.goodName;
			marketEntry.GetChild(1).GetComponent<TMP_Text>().text = Mathf.RoundToInt(good.perceivedQuality * 100.0f) + "%";

			TMP_InputField priceField = marketEntry.GetChild(2).GetComponent<TMP_InputField>();
			if(good.owner == playerInventory)
			{
				if(offers.ContainsKey(good))
				{
					priceField.text = offers[good].price.ToString();
				}

				priceField.onEndEdit.RemoveAllListeners();
				priceField.onEndEdit.AddListener(delegate
				{
					if(offers.ContainsKey(localGood))
					{
						int price = priceField.text != string.Empty ? Mathf.Max(Int32.Parse(priceField.text), 0) : 0;
						offers[localGood].price = price;

						panelManager.QueuePanelUpdate(this);
					}
				});

				priceField.gameObject.SetActive(true);
				marketEntry.GetChild(3).gameObject.SetActive(false);
			}
			else
			{
				TMP_Text priceText = marketEntry.GetChild(3).GetComponent<TMP_Text>();
				priceText.text = offers[good].price.ToString();

				priceField.gameObject.SetActive(false);
				priceText.gameObject.SetActive(true);
			}

			marketEntry.GetChild(5).GetComponent<TMP_Text>().text = playerInventory.GetInventoryAmount(good).ToString();

			TMP_InputField amountField = marketEntry.GetChild(7).GetComponent<TMP_InputField>();
			amountField.text = offers.ContainsKey(good) ? offers[good].lastAmount.ToString() : "1";
			amountField.onEndEdit.RemoveAllListeners();
			amountField.onEndEdit.AddListener(delegate
			{
				if(offers.ContainsKey(localGood))
				{
					offers[localGood].lastAmount = (amountField.text != string.Empty) ? Int32.Parse(amountField.text) : 1;
				}
			});

			if(offers.ContainsKey(good) && offers[good].count > 0)
			{
				Button buyButton = marketEntry.GetChild(6).GetComponent<Button>();
				buyButton.onClick.RemoveAllListeners();
				buyButton.onClick.AddListener(delegate
				{
					int amount = amountField.text != string.Empty ? Int32.Parse(amountField.text) : 0;
					if(localGood.owner != playerInventory)
					{
						Buy(localGood, Mathf.Clamp(amount, 0, Mathf.Min(offers[localGood].count, Mathf.FloorToInt(playerInventory.GetMoney() / offers[localGood].price))));
					}
					else
					{
						CancelSale(localGood, Mathf.Clamp(amount, 0, offers[localGood].count));
					}
				});
				buyButton.gameObject.SetActive(true);
			}
			else
			{
				marketEntry.GetChild(6).gameObject.SetActive(false);
			}

			if(playerInventory.GetInventoryAmount(good) > 0)
			{
				Button sellButton = marketEntry.GetChild(8).GetComponent<Button>();
				sellButton.onClick.RemoveAllListeners();
				sellButton.onClick.AddListener(delegate
				{
					PutUpForSale(localGood,
						amountField.text != string.Empty ? Mathf.Clamp(Int32.Parse(amountField.text), 0, playerInventory.GetInventoryAmount(localGood)) : 0,
						priceField.text != string.Empty ? Mathf.Max(Int32.Parse(priceField.text), 0) : 0);
				});
				sellButton.gameObject.SetActive(true);
			}
			else
			{
				marketEntry.GetChild(8).gameObject.SetActive(false);
			}

			marketEntry.GetChild(9).GetComponent<TMP_Text>().text = (offers.ContainsKey(good) ? offers[good].count : 0).ToString();

			if(i % 2 != 0)
			{
				marketEntry.GetComponent<Image>().enabled = false;
			}

			++i;
		}
	}

	public bool PutUpForSale(Good good, int amount, int price)
	{
		if(good.owner.ReserveGood(good, amount))
		{
			good.owner.SetLastMarket(this);

			if(!offers.ContainsKey(good))
			{
				offers.Add(good, new MarketOffer(price, amount));
			}
			else
			{
				MarketOffer offer = offers[good];
				offer.count += amount;
				offers[good] = offer;
			}

			panelManager.QueuePanelUpdate(this);

			return true;
		}

		return false;
	}

	public bool CancelSale(Good good, int amount)
	{
		MarketOffer offer;
		if(offers.ContainsKey(good))
		{
			offer = offers[good];
		}
		else
		{
			return true;
		}

		if(amount <= offer.count && good.owner.UnreserveGood(good, amount))
		{
			if(amount == offer.count)
			{
				offers.Remove(good);
			}
			else
			{
				offer.count -= amount;
			}

			panelManager.QueuePanelUpdate(this);

			return true;
		}

		Debug.LogWarning("Cancel Sale of " + amount + " " + good.goodData.goodName + " is not possible!");
		return false;
	}

	public bool Buy(Good good, int amount, Inventory buyer = null)
	{
		if(offers.ContainsKey(good) && offers[good].count >= amount
			&& (buyer == null || buyer.ChangeMoney(-offers[good].price * amount))
			&& good.owner.UnreserveGood(good, amount) && good.owner.WithdrawGood(good, amount, false))
		{
			good.owner.ChangeMoney(offers[good].price * amount);

			MarketOffer offer = offers[good];
			offer.count -= amount;
			if(offer.count > 0)
			{
				offers[good] = offer;
			}
			else
			{
				offers.Remove(good);
			}

			if(buyer != null)
			{
				Good transferredGood = new Good(good.goodData, good.quality, good.perceivedQuality, buyer);
				buyer.DepositGood(transferredGood, amount);
			}

			panelManager.QueuePanelUpdate(this);

			return true;
		}

		Debug.LogWarning("Buying of " + amount + " " + good.goodData.goodName + " by " + buyer + " failed!");
		return false;
	}

	public bool PlayerExit(Inventory playerInventory)
	{
		List<Tuple<Good, MarketOffer>> withdrawnOffers = new List<Tuple<Good, MarketOffer>>();
		foreach(KeyValuePair<Good, MarketOffer> offer in offers)
		{
			if(offer.Key.owner == playerInventory)
			{
				if(playerInventory.UnreserveGood(offer.Key, offer.Value.count))
				{
					withdrawnOffers.Add(new Tuple<Good, MarketOffer>(offer.Key, offer.Value));
				}
				else
				{
					foreach(Tuple<Good, MarketOffer> withdrawnOffer in withdrawnOffers)
					{
						playerInventory.ReserveGood(withdrawnOffer.Item1, withdrawnOffer.Item2.count);
					}

					return false;
				}
			}
		}
		foreach(Tuple<Good, MarketOffer> withdrawnOffer in withdrawnOffers)
		{
			offers.Remove(withdrawnOffer.Item1);
		}

		panelManager.QueuePanelUpdate(this);

		return true;
	}

	public List<Tuple<Good, MarketOffer>> GetSortedOffers(NeedCategory category, Comparison<Tuple<Good, MarketOffer>> sortFunction)
	{
		List<Tuple<Good, MarketOffer>> sortedOffers = new List<Tuple<Good, MarketOffer>>();
		foreach(KeyValuePair<Good, MarketOffer> offer in offers)
		{
			if(offer.Key.goodData.needCategory == category)
			{
				sortedOffers.Add(new Tuple<Good, MarketOffer>(offer.Key, offer.Value));
			}
		}
		sortedOffers.Sort(sortFunction);

		return sortedOffers;
	}

	public List<Tuple<Good, MarketOffer>> GetSortedOffers(string goodName, Comparison<Tuple<Good, MarketOffer>> sortFunction)
	{
		List<Tuple<Good, MarketOffer>> sortedOffers = new List<Tuple<Good, MarketOffer>>();
		foreach(KeyValuePair<Good, MarketOffer> offer in offers)
		{
			if(offer.Key.goodData.goodName == goodName)
			{
				sortedOffers.Add(new Tuple<Good, MarketOffer>(offer.Key, offer.Value));
			}
		}
		sortedOffers.Sort(sortFunction);

		return sortedOffers;
	}
}
