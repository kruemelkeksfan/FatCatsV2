using System;
using System.Collections.Generic;
using UnityEngine;
using SQLite;

public class PopulationController : MonoBehaviour
{
	public class PopulationGroup
	{
		[PrimaryKey, AutoIncrement]
		public int Id
		{
			get; set;
		}

		public int Birthyear
		{
			get; set;
		}
		public int Income
		{
			get; set;
		}
		public int Savings
		{
			get; set;
		}
		public int Count
		{
			get; set;
		}
		public float Satisfaction
		{
			get; set;
		}

		public PopulationGroup()
		{
			Id = 0;
			Birthyear = 0;
			Income = 0;
			Savings = 0;
			Count = 0;
			Satisfaction = 0.0f;
		}

		public PopulationGroup(int birthyear, int income, int savings, int count)
		{
			Id = 0;
			Birthyear = birthyear;
			Income = income;
			Savings = savings;
			Count = count;
			Satisfaction = 0.0f;
		}
	}

	[Serializable]
	private struct NeedData
	{
		public NeedCategory goodCategory;
		public float budgetPercentage;
		public float minBuyAmount;
		public float maxBuyAmount;
		public bool essential;

		public NeedData(NeedCategory goodCategory, float budgetPercentage, float minBuyAmount, float maxBuyAmount, bool essential)
		{
			this.budgetPercentage = budgetPercentage;
			this.minBuyAmount = minBuyAmount;
			this.maxBuyAmount = maxBuyAmount;
			this.goodCategory = goodCategory;
			this.essential = essential;
		}
	}

	// TODO: Indexing based on Age or Wage
	// TODO: Wage Groups

	// TODO: Unemployment Compensation: probably best approach would be to leave income as is and just award unemployment compensation at the start of each Town update tick
	// downside: people would not compare their job income to unemployment compensation when deciding for a job
	// potential solution: also award (partial) compensation to people who work for less than the unemployment compensation
	// if you want to implement it via income anyways, please check all Usages of "Income" in the whole project - comparisons like "Income > 0" are made in many places

	[SerializeField] private NeedData[] needData = null;
	[SerializeField] private int startingPopulation = 10;
	[SerializeField] private float maxPopulationGainFactor = 0.2f;
	[SerializeField] private int startingMoney = 10;
	private TimeController timeController = null;
	private Market market = null;
	private BuildingController buildingController = null;
	private SQLiteConnection database = null;
	private int totalPopulation = 0;

	private void Start()
	{
		timeController = TimeController.GetInstance();
		market = gameObject.GetComponent<Market>();
		buildingController = gameObject.GetComponent<BuildingController>();

		// TODO: Save to File when Savegames are being implemented
		// TODO: Check Database Size
		// 1. Create a connection to the database.
		// The special ":memory:" in-memory database and
		// URIs like "file:///somefile" are also supported
		database = new SQLiteConnection(":memory:");
		// 2. Once you have defined your entity, you can automatically
		// generate tables in your database by calling CreateTable
		database.CreateTable<PopulationGroup>();
		// TODO: Ensure regular Vacuum
		// 4.b You can also make queries at a low-level using the Query method
		// var players = db.Query<Player>("SELECT * FROM Player WHERE Id = ?", 1);
		// foreach (Player player in players)
		// {
		//   Debug.Log($"Player with ID 1 is called {player.Name}");
		// }
		// 5. You can perform low-level updates to the database using the Execute
		// method, for example for running PRAGMAs or VACUUM
		// db.Execute("VACUUM");

		AddPopulationGroup(0, startingPopulation * startingMoney, startingPopulation, -20);
		totalPopulation = startingPopulation;
	}

	public void UpdatePopulation()
	{
		totalPopulation = 0;
		int populationGain = 0;
		// 4.a The most straightforward way to query for data
		// is using the Table method. This can take predicates
		// for constraining via WHERE clauses and/or adding ORDER BY clauses
		TableQuery<PopulationGroup> populationQuery = database.Table<PopulationGroup>().OrderByDescending<int>(populationGroup => populationGroup.Income);
		foreach(PopulationGroup populationGroup in populationQuery)
		{
			// TODO: Check if going from richest to poorest?
			if(populationGroup.Count <= 0)
			{
				continue;
			}

			populationGroup.Savings += populationGroup.Income * populationGroup.Count;

			List<Tuple<Good, MarketOffer>> shoppingCart = new List<Tuple<Good, MarketOffer>>();
			float minEssentialNeedSatisfaction = 1.0f;
			float avgNeedSatisfaction = 0.0f;
			int j = 0;
			foreach(NeedData need in needData)
			{
				shoppingCart.Clear();

				int budget = Mathf.FloorToInt(populationGroup.Savings * need.budgetPercentage);
				int needAmount = Mathf.CeilToInt(need.maxBuyAmount * populationGroup.Count);

				// Step 1: Buy Cheapest until maximum Demand is satisfied or Budget is exhausted
				LinkedList<Tuple<Good, MarketOffer>> offers = new LinkedList<Tuple<Good, MarketOffer>>(market.GetSortedOffers(need.goodCategory, Market.CompareOfferPrice));
				while(offers.Count > 0 && needAmount > 0)
				{
					Tuple<Good, MarketOffer> currentOffer = offers.First.Value;

					while(offers.Count > 0 && currentOffer.Item2.count <= 0)
					{
						offers.RemoveFirst();
						currentOffer = offers.First?.Value;
					}
					if(currentOffer == null)
					{
						break;
					}

					int buyAmount = Math.Min(Math.Min(currentOffer.Item2.count, needAmount), budget / currentOffer.Item2.price);
					if(buyAmount <= 0)
					{
						break;
					}

					shoppingCart.Add(new Tuple<Good, MarketOffer>(currentOffer.Item1, new MarketOffer(currentOffer.Item2.price, buyAmount)));
					offers.First.Value = new Tuple<Good, MarketOffer>(currentOffer.Item1, new MarketOffer(currentOffer.Item2.price, currentOffer.Item2.count - buyAmount));

					budget -= currentOffer.Item2.price * buyAmount;
					needAmount -= buyAmount;
				}

				// Step 2: Switch Items for better Quality-Price-Ratio until Budget is exhausted
				// We have 2 Lists: shoppingCart contains the cheap Items we "reserved" in Step 1, offers contains all still available Market Offers
				// Both Lists are sorted by Quality/Price-Ratio
				// We traverse both Lists, the shoppingCart from lousiest to best Item and offers from best to lousiest Items
				// If the current Item in offers is better than the one in shoppingCart, the Item Stacks are swapped
				if(shoppingCart.Count > 0)
				{
					List<Tuple<Good, MarketOffer>> leftoverOffers = new List<Tuple<Good, MarketOffer>>(offers);
					shoppingCart.Sort(Market.CompareOfferPerceivedQualityPriceRatio);
					leftoverOffers.Sort(Market.CompareOfferPerceivedQualityPriceRatio);
					int shoppingCartIndex = shoppingCart.Count - 1;
					int betterOfferIndex = 0;
					while(shoppingCartIndex >= 0 && betterOfferIndex < leftoverOffers.Count)
					{
						// Skip Offer if there is no Stock left
						if(leftoverOffers[betterOfferIndex].Item2.count <= 0)
						{
							++betterOfferIndex;
							continue;
						}

						// Break if already bought Item has a better Quality-Ratio than the better Offer Candidate
						if((shoppingCart[shoppingCartIndex].Item1.perceivedQuality / shoppingCart[shoppingCartIndex].Item2.price)
							>= (leftoverOffers[betterOfferIndex].Item1.perceivedQuality / leftoverOffers[betterOfferIndex].Item2.price))
						{
							break;
						}

						int swapAmount = 0;
						int priceDifference = leftoverOffers[betterOfferIndex].Item2.price - shoppingCart[shoppingCartIndex].Item2.price;
						if(priceDifference > 0)
						{
							swapAmount = Math.Min(Math.Min(leftoverOffers[betterOfferIndex].Item2.count, shoppingCart[shoppingCartIndex].Item2.count), budget / priceDifference);
						}
						else
						{
							swapAmount = Math.Min(leftoverOffers[betterOfferIndex].Item2.count, shoppingCart[shoppingCartIndex].Item2.count);
						}

						if(swapAmount > 0)
						{
							// shoppingCart-Stack still contains Stuff after the Swap: reduce Stack Size by swapAmount, create a new Stack with swapAmount Items from offers-Stack
							if(shoppingCart[shoppingCartIndex].Item2.count - swapAmount > 0)
							{
								shoppingCart[shoppingCartIndex] = new Tuple<Good, MarketOffer>(shoppingCart[shoppingCartIndex].Item1, new MarketOffer(shoppingCart[shoppingCartIndex].Item2.price, shoppingCart[shoppingCartIndex].Item2.count - swapAmount));
								shoppingCart.Add(new Tuple<Good, MarketOffer>(leftoverOffers[betterOfferIndex].Item1, new MarketOffer(leftoverOffers[betterOfferIndex].Item2.price, swapAmount)));
							}
							// shoppingCart-Stack is empty after the Swap: replace Stack with Stack from offers of identical Size, go to next shoppingCart-Stack
							else
							{
								shoppingCart[shoppingCartIndex] = new Tuple<Good, MarketOffer>(leftoverOffers[betterOfferIndex].Item1, new MarketOffer(leftoverOffers[betterOfferIndex].Item2.price, swapAmount));
								--shoppingCartIndex;
							}

							// offers-Stack still contains Stuff after the Swap: reduce Stack Size by swapAmount
							if(leftoverOffers[betterOfferIndex].Item2.count - swapAmount > 0)
							{
								leftoverOffers[betterOfferIndex] = new Tuple<Good, MarketOffer>(leftoverOffers[betterOfferIndex].Item1, new MarketOffer(leftoverOffers[betterOfferIndex].Item2.price, leftoverOffers[betterOfferIndex].Item2.count - swapAmount));
							}
							// offers-Stack is empty after the Swap: go to next offers-Stack
							else
							{
								++betterOfferIndex;
							}
						}
						else
						{
							++betterOfferIndex;
						}
					}
				}

				// Step 3: Buy and calculate Satisfaction
				int totalAmount = 0;
				int totalPrice = 0;
				float avgPerceivedQuality = 0.0f;
				foreach(Tuple<Good, MarketOffer> offer in shoppingCart)
				{
					market.Buy(offer.Item1, offer.Item2.count);

					totalAmount += offer.Item2.count;
					totalPrice += offer.Item2.price * offer.Item2.count;
					avgPerceivedQuality += offer.Item1.perceivedQuality * offer.Item2.count;
				}
				avgPerceivedQuality /= totalAmount;

				populationGroup.Savings -= totalPrice;

				// Step 4: Calculate Satisfaction
				float needSatisfaction = 0.0f;
				if(totalAmount < need.minBuyAmount * populationGroup.Count)
				{
					needSatisfaction = (totalAmount / (need.minBuyAmount * populationGroup.Count)) * 0.5f;
				}
				else if(totalAmount < (need.maxBuyAmount * populationGroup.Count))
				{
					needSatisfaction = 0.5f + (totalAmount / (need.maxBuyAmount * populationGroup.Count)) * 0.25f;
				}
				else
				{
					needSatisfaction = Mathf.Clamp01(0.75f + avgPerceivedQuality * 0.25f);
				}
				if(need.essential && needSatisfaction < minEssentialNeedSatisfaction)
				{
					minEssentialNeedSatisfaction = needSatisfaction;
				}
				avgNeedSatisfaction += needSatisfaction;

				++j;
			}
			avgNeedSatisfaction /= needData.Length;
			populationGroup.Satisfaction = avgNeedSatisfaction > minEssentialNeedSatisfaction ? avgNeedSatisfaction : minEssentialNeedSatisfaction;

			// Update Population
			if(populationGroup.Satisfaction > 0.5f)
			{
				populationGain += Mathf.FloorToInt(((populationGroup.Satisfaction - 0.5f) * 2.0f) * populationGroup.Count * maxPopulationGainFactor);
			}
			else
			{
				populationGain -= Mathf.CeilToInt((1.0f - (populationGroup.Satisfaction * 2.0f)) * populationGroup.Count * maxPopulationGainFactor);
			}
			totalPopulation += populationGroup.Count;

			database.Update(populationGroup);
		}

		// Secure Minimum Population
		if(totalPopulation + populationGain < startingPopulation)
		{
			populationGain = startingPopulation - totalPopulation;
		}

		// Grow Population
		if(populationGain >= 0)
		{
			AddPopulationGroup(0, 0, populationGain);
		}
		// Un-grow Population
		else
		{
			int peopleLeftToDisappear = -populationGain;
			populationQuery = database.Table<PopulationGroup>().OrderBy<float>(populationGroup => populationGroup.Satisfaction);
			foreach(PopulationGroup populationGroup in populationQuery)
			{
				int despawnAmount = Mathf.Min(populationGroup.Count, peopleLeftToDisappear);
				populationGroup.Count -= despawnAmount;
				peopleLeftToDisappear -= despawnAmount;

				if(populationGroup.Income > 0)
				{
					buildingController.KillTownWorkers(populationGroup.Income, despawnAmount);
				}

				database.Update(populationGroup);
			}
			if(peopleLeftToDisappear > 0)
			{
				Debug.LogWarning("Could not dissapear enough People, " + peopleLeftToDisappear + " Subjects left to disappear!");
			}
		}
		totalPopulation += populationGain;

		// Delete extinct Population Groups
		int heritage = 0;
		populationQuery = database.Table<PopulationGroup>().Where(populationGroup => populationGroup.Count == 0);
		foreach(PopulationGroup populationGroup in populationQuery)
		{
			heritage += populationGroup.Savings;
			database.Delete(populationGroup);
		}
		// Der Teufel scheißt auf den größten Haufen
		// Give all Money of extinct Population Groups to richest Population Groups
		PopulationGroup richestPopulationGroup = database.Table<PopulationGroup>().OrderByDescending<int>(populationGroup => populationGroup.Income).FirstOrDefault();
		richestPopulationGroup.Savings += heritage;
		database.Update(richestPopulationGroup);
	}

	public void AddPopulationGroup(int income, int savings, int count, int birthyear = int.MinValue)
	{
		// PopulationGroup populationGroup = new PopulationGroup(1995, 0, 0, 1);
		// database.Insert(populationGroup);

		if(birthyear <= int.MinValue)
		{
			birthyear = timeController.GetCurrentYear();
		}

		// Add Population Group to equivalent Population Group, if existent
		PopulationGroup populationGroup = database.Table<PopulationGroup>().Where(populationGroup => (populationGroup.Birthyear == birthyear && populationGroup.Income == income)).FirstOrDefault();
		if(populationGroup != null)
		{
			populationGroup.Count += count;
			populationGroup.Savings += savings;
			database.Update(populationGroup);
			return;
		}

		// If the Population Group does not exist yet, insert it
		// Sort Population Groups descending, so that rich People get to buy Stuff first and starve last
		populationGroup = new PopulationGroup(birthyear, income, savings, count);
		database.Insert(populationGroup);
	}

	public bool ChangeIncome(int oldIncome, int newIncome, int count)
	{
		if(count <= 0)
		{
			return true;
		}

		int peopleLeftToChange = count;
		// Old People will change their Jobs first, for better or worse
		TableQuery<PopulationGroup> populationQuery = database.Table<PopulationGroup>().Where(populationGroup => populationGroup.Income == oldIncome).OrderBy<int>(populationGroup => populationGroup.Birthyear);
		foreach(PopulationGroup populationGroup in populationQuery)
		{
			int changeAmount = Mathf.Min(peopleLeftToChange, populationGroup.Count);
			int transferredMoney = Mathf.FloorToInt(populationGroup.Savings * (changeAmount / populationGroup.Count));

			populationGroup.Count -= changeAmount;
			populationGroup.Savings -= transferredMoney;
			database.Update(populationGroup);

			AddPopulationGroup(newIncome, transferredMoney, changeAmount, populationGroup.Birthyear);
			
			peopleLeftToChange -= changeAmount;
			if(peopleLeftToChange <= 0)
			{
				return true;
			}
		}

		return false;
	}

	// Analyzes the Towns Job Market, determines the best Jobs for People, assigns them to the right PopulationGroups and returns Lists to update the Building Data
	public Tuple<Dictionary<Building, int>, Dictionary<int, int>> UpdateJobMarket(LinkedList<Tuple<Building, int>> openPositions)
	{
		Dictionary<Building, int> hireList = new Dictionary<Building, int>();           // Building, Count
		Dictionary<int, int> fireList = new Dictionary<int, int>();                     // Wage, Count

		if(openPositions.Count <= 0)
		{
			new Tuple<Dictionary<Building, int>, Dictionary<int, int>>(hireList, fireList);
		}

		LinkedListNode<Tuple<Building, int>> currentOpenPosition = openPositions.First; // Building, Number of open Positions
		// Order by Income primarily and by Age secondarily
		// We need this to give young People better Job Opportunities and simulate Age Discrimination
		PopulationGroup[] populationGroups = database.Table<PopulationGroup>().OrderBy<int>(populationGroup => populationGroup.Income).ThenByDescending<int>(populationGroup => populationGroup.Birthyear).ToArray();
		int i = 0;
		while(currentOpenPosition != null && i < populationGroups.Length)
		{
			// Break if new Job is payed worse than the currently worst payed Job
			if(currentOpenPosition.Value.Item1.wage <= populationGroups[i].Income)
			{
				break;
			}

			Building currentOpenPositionBuilding = currentOpenPosition.Value.Item1;
			int hireCount = Mathf.Min(currentOpenPosition.Value.Item2, populationGroups[i].Count);

			// Hire List
			if(hireList.ContainsKey(currentOpenPositionBuilding))
			{
				hireList[currentOpenPositionBuilding] += hireCount;
			}
			else
			{
				hireList.Add(currentOpenPositionBuilding, hireCount);
			}
			// Fire List
			if(populationGroups[i].Income > 0) // If the current PopulationGroup already has work
			{
				if(fireList.ContainsKey(populationGroups[i].Income))
				{
					fireList[populationGroups[i].Income] += hireCount;
				}
				else
				{
					fireList.Add(populationGroups[i].Income, hireCount);
				}
			}

			// Let poor People resign their Jobs ...
			populationGroups[i].Count -= hireCount;

			// ... and let them take better Jobs
			if(populationGroups[i].Count > 0)
			{
				AddPopulationGroup(currentOpenPositionBuilding.wage, 0, hireCount, populationGroups[i].Birthyear);
			}
			else
			{
				AddPopulationGroup(currentOpenPositionBuilding.wage, populationGroups[i].Savings, hireCount, populationGroups[i].Birthyear);
				populationGroups[i].Savings = 0;
			}

			if(hireList[currentOpenPositionBuilding] >= currentOpenPosition.Value.Item2)
			{
				currentOpenPosition = currentOpenPosition.Next;
				if(currentOpenPosition == null)
				{
					database.Update(populationGroups[i]);
				}
			}
			if(populationGroups[i].Count <= 0)
			{
				database.Update(populationGroups[i]);
				++i;
			}
		}

		return new Tuple<Dictionary<Building, int>, Dictionary<int, int>>(hireList, fireList);
	}

	public int CalculateAverageIncome()
	{
		float totalIncome = 0.0f;
		TableQuery<PopulationGroup> populationQuery = database.Table<PopulationGroup>();
		foreach(PopulationGroup populationGroup in populationQuery)
		{
			if(populationGroup.Count <= 0)
			{
				continue;
			}

			Debug.Log(populationGroup.Income + " " + populationGroup.Count);
			totalIncome += populationGroup.Income * populationGroup.Count;
		}

		return Mathf.RoundToInt(totalIncome / totalPopulation);
	}

	public int CalculateAverageSavings()
	{
		float totalSavings = 0.0f;
		TableQuery<PopulationGroup> populationQuery = database.Table<PopulationGroup>();
		foreach(PopulationGroup populationGroup in populationQuery)
		{
			if(populationGroup.Count <= 0)
			{
				continue;
			}

			totalSavings += populationGroup.Savings;
		}

		return Mathf.RoundToInt(totalSavings / totalPopulation);
	}

	public int GetTotalPopulation()
	{
		return totalPopulation;
	}

	public int GetUnemployedPopulation()
	{
		int unemployed = 0;
		TableQuery<PopulationGroup> populationQuery = database.Table<PopulationGroup>().Where(populationGroup => populationGroup.Income <= 0);
		foreach(PopulationGroup populationGroup in populationQuery)
		{
			if(populationGroup.Income <= 0)
			{
				unemployed += populationGroup.Count;
			}
			else
			{
				break;
			}
		}

		return unemployed;
	}
}
