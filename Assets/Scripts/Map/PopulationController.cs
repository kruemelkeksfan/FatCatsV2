using System;
using System.Collections.Generic;
using UnityEngine;

public class PopulationController : MonoBehaviour
{
	[Serializable]
	public struct PopulationGroup
	{
		public int age;
		public int income;
		public int savings;
		public int count;
		public float satisfaction;

		public PopulationGroup(int age, int income, int savings, int count)
		{
			this.age = age;
			this.income = income;
			this.savings = savings;
			this.count = count;
			satisfaction = 1.0f;
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

	// TODO: Unemployment Compensation: ensure, that nobody works for wages <= Unemployment Compensation so that the Population Group Sorting stays intact

	[SerializeField] private NeedData[] needData = null;
	[SerializeField] private int startingPopulation = 10;
	[SerializeField] private float maxPopulationGainFactor = 0.2f;
	[SerializeField] private int startingMoney = 10;
	private Market market = null;
	private BuildingController buildingController = null;
	private PopulationGroup[] populationGroups = null;
	private Dictionary<int, PopulationGroup> populationByAge = null;
	private Dictionary<int, PopulationGroup> populationByWage = null;
	private int totalPopulation = 0;

	private void Awake()
	{
		market = gameObject.GetComponent<Market>();
		buildingController = gameObject.GetComponent<BuildingController>();

		AddPopulationGroup(0, startingPopulation * startingMoney, startingPopulation);
		totalPopulation = startingPopulation;
	}

	public void UpdatePopulation()
	{
		totalPopulation = 0;
		int populationGain = 0;
		for(int i = 0; i < populationGroups.Length; ++i)
		{
			if(populationGroups[i].count <= 0)
			{
				continue;
			}

			populationGroups[i].savings += populationGroups[i].income * populationGroups[i].count;

			List<Tuple<Good, MarketOffer>> shoppingCart = new List<Tuple<Good, MarketOffer>>();
			float minEssentialNeedSatisfaction = 1.0f;
			float avgNeedSatisfaction = 0.0f;
			int j = 0;
			foreach(NeedData need in needData)
			{
				shoppingCart.Clear();

				int budget = Mathf.FloorToInt(populationGroups[i].savings * need.budgetPercentage);
				int needAmount = Mathf.CeilToInt(need.maxBuyAmount * populationGroups[i].count);

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

				populationGroups[i].savings -= totalPrice;

				// Step 4: Calculate Satisfaction
				float needSatisfaction = 0.0f;
				if(totalAmount < need.minBuyAmount * populationGroups[i].count)
				{
					needSatisfaction = (totalAmount / (need.minBuyAmount * populationGroups[i].count)) * 0.5f;
				}
				else if(totalAmount < (need.maxBuyAmount * populationGroups[i].count))
				{
					needSatisfaction = 0.5f + (totalAmount / (need.maxBuyAmount * populationGroups[i].count)) * 0.25f;
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
			populationGroups[i].satisfaction = avgNeedSatisfaction > minEssentialNeedSatisfaction ? avgNeedSatisfaction : minEssentialNeedSatisfaction;

			// Update Population
			if(populationGroups[i].satisfaction > 0.5f)
			{
				populationGain += Mathf.FloorToInt(((populationGroups[i].satisfaction - 0.5f) * 2.0f) * populationGroups[i].count * maxPopulationGainFactor);
			}
			else
			{
				populationGain -= Mathf.CeilToInt((1.0f - (populationGroups[i].satisfaction * 2.0f)) * populationGroups[i].count * maxPopulationGainFactor);
			}
			totalPopulation += populationGroups[i].count;
		}

		// Secure Minimum Population
		if(totalPopulation + populationGain < startingPopulation)
		{
			populationGain = startingPopulation - totalPopulation;
		}

		// Grow Population
		if(populationGain >= 0)
		{
			populationGroups[populationGroups.Length - 1].count += populationGain;
		}
		// Un-grow Population
		else
		{
			int peopleLeftToDisappear = -populationGain;
			for(int i = populationGroups.Length - 1; i >= 0; --i)
			{
				int despawnAmount = Mathf.Min(populationGroups[i].count, peopleLeftToDisappear);
				populationGroups[i].count -= despawnAmount;
				peopleLeftToDisappear -= despawnAmount;

				if(i < populationGroups.Length - 1)
				{
					buildingController.KillTownWorkers(populationGroups[i].income, despawnAmount);
				}
			}
			if(peopleLeftToDisappear > 0)
			{
				Debug.LogWarning("Could not dissapear enough People, " + peopleLeftToDisappear + " Subjects left to disappear!");
			}
		}
		totalPopulation += populationGain;
	}

	public void AddPopulationGroup(int income, int savings, int count)
	{
		// Initialize new populationGroups-Array if it is not set up yet
		if(populationGroups == null)
		{
			populationGroups = new PopulationGroup[1];
			populationGroups[0] = new PopulationGroup(0, income, savings, count); // TODO: Set Age
			return;
		}

		// Add Population Group to equivalent Population Group in Array, if existent
		for(int i = 0; i < populationGroups.Length; ++i)
		{
			if(populationGroups[i].income == income)
			{
				populationGroups[i].count += count;
				return;
			}
		}

		// If the Population Group does not exist yet, insert it
		// Sort Population Groups descending, so that rich People get to buy Stuff first and starve last
		PopulationGroup[] newPopulationGroups = new PopulationGroup[populationGroups.Length + 1];
		bool inserted = false;
		for(int i = 0; i < newPopulationGroups.Length; ++i)
		{
			if(i < populationGroups.Length && populationGroups[i].income > income)
			{
				newPopulationGroups[i] = populationGroups[i];
			}
			else
			{
				if(!inserted)
				{
					newPopulationGroups[i] = new PopulationGroup(0, income, savings, count); // TODO: Set Age
					inserted = true;
				}
				else
				{
					newPopulationGroups[i] = populationGroups[i - 1];
				}
			}
		}

		populationGroups = newPopulationGroups;
	}

	public int Hire(int income, int count)
	{
		int hiredCount = 0;
		for(int i = populationGroups.Length - 1; i >= 0; --i)
		{
			if(populationGroups[i].income <= 0)
			{
				int hireCount = Mathf.Min(populationGroups[i].count, count - hiredCount);
				populationGroups[i].count -= hireCount;
				hiredCount += hireCount;
			}
			else if(populationGroups[i].income == income)
			{
				populationGroups[i].count += hiredCount;
			}
		}

		AddPopulationGroup(income, 0, hiredCount);

		return count - hiredCount;
	}

	public bool Fire(int income, int count)
	{
		if(count <= 0)
		{
			return true;
		}

		for(int i = 0; i < populationGroups.Length; ++i)
		{
			if(populationGroups[i].income == income && populationGroups[i].count >= count)
			{
				populationGroups[i].count -= count;
				populationGroups[populationGroups.Length - 1].count += count;
				return true;
			}
		}

		return false;
	}

	public Tuple<Dictionary<Tuple<int, int>, int>, Dictionary<int, int>> UpdateJobMarket(LinkedList<Tuple<int, int, int, int>> openPositions)
	{
		Dictionary<Tuple<int, int>, int> hireList = new Dictionary<Tuple<int, int>, int>();		// <Building ID, Job ID>, Count
		Dictionary<int, int> fireList = new Dictionary<int, int>();								// Wage, Count
		LinkedListNode<Tuple<int, int, int, int>> currentOpenPosition = openPositions.First;	// Building ID, Job ID, Number of open Positions, Wage
		int emptyPopulationGroups = 0;
		int i = populationGroups.Length - 1;	// Start with poorest Population Group
		while(currentOpenPosition != null && i >= 0)
		{
			// Break if new Job is payed worse than the currently worst payed Job
			if(currentOpenPosition.Value.Item4 <= populationGroups[i].income)
			{
				break;
			}

			Tuple<int, int> currentOpenPositionId = new Tuple<int, int>(currentOpenPosition.Value.Item1, currentOpenPosition.Value.Item2);

			int hireCount = Mathf.Min(currentOpenPosition.Value.Item3, populationGroups[i].count);
			// Hire List
			if(hireList.ContainsKey(currentOpenPositionId))
			{
				hireList[currentOpenPositionId] += hireCount;
			}
			else
			{
				hireList.Add(currentOpenPositionId, hireCount);
			}
			// Fire List
			if(i < populationGroups.Length - 1)
			{
				if(fireList.ContainsKey(currentOpenPosition.Value.Item4))
				{
					fireList[currentOpenPosition.Value.Item4] += hireCount;
				}
				else
				{
					fireList.Add(currentOpenPosition.Value.Item4, hireCount);
				}
			}

			// Let poor People resign their Jobs ...
			populationGroups[i].count -= hireCount;

			// ... and let them take better Jobs
			bool hiredSuccessfully = false;
			for(int j = 0; j < populationGroups.Length; ++j)
			{
				if(populationGroups[j].income == currentOpenPosition.Value.Item4)
				{
					populationGroups[j].count += hireCount;
					hiredSuccessfully = true;
					break;
				}
			}
			if(!hiredSuccessfully)
			{
				AddPopulationGroup(currentOpenPosition.Value.Item4, 0, hireCount);
			}

			if(hireList[currentOpenPositionId] >= currentOpenPosition.Value.Item3)
			{
				currentOpenPosition = currentOpenPosition.Next;
			}
			if(populationGroups[i].count <= 0)
			{
				++emptyPopulationGroups;
				--i;
			}
		}

		// TODO: Remove after some Playtesting
		if(emptyPopulationGroups > 0)
		{
			Debug.Log("Empty Population Groups: " + emptyPopulationGroups);
		}

		if(emptyPopulationGroups >= 10)
		{
			PopulationGroup[] newPopulationGroups = new PopulationGroup[populationGroups.Length - emptyPopulationGroups];
			int skippedPopulationGroups = 0;
			for(int j = 0; j < newPopulationGroups.Length; ++j)
			{
				if(populationGroups[j].count > 0 || (j + skippedPopulationGroups) >= (populationGroups.Length - 1))	// Never delete last Population Group (we will need it again)
				{
					newPopulationGroups[j] = populationGroups[j + skippedPopulationGroups];
				}
				else
				{
					populationGroups[populationGroups.Length - 1].savings += populationGroups[j + skippedPopulationGroups].savings;	// Transfer Savings to avoid Money Sink
					++skippedPopulationGroups;
				}
			}
			populationGroups = newPopulationGroups;
		}

		return new Tuple<Dictionary<Tuple<int, int>, int>, Dictionary<int, int>>(hireList, fireList);
	}

	public bool ChangeIncome(int oldIncome, int newIncome, int count)
	{
		if(count <= 0)
		{
			return true;
		}

		for(int i = 0; i < populationGroups.Length; ++i)
		{
			if(populationGroups[i].income == oldIncome && populationGroups[i].count >= count)
			{
				populationGroups[i].count -= count;

				for(int j = 0; j < populationGroups.Length; ++j)
				{
					if(populationGroups[j].income == newIncome)
					{
						populationGroups[i].count += count;
						return true;
					}
				}

				AddPopulationGroup(newIncome, 0, count);

				return true;
			}
		}

		return false;
	}

	public int CalculateAverageIncome()
	{
		float totalIncome = 0.0f;
		foreach(PopulationGroup populationGroup in populationGroups)
		{
			if(populationGroup.count <= 0)
			{
				continue;
			}

			totalIncome += populationGroup.income * populationGroup.count;
		}

		return Mathf.RoundToInt(totalIncome / totalPopulation);
	}

	public int CalculateAverageSavings()
	{
		float totalSavings = 0.0f;
		foreach(PopulationGroup populationGroup in populationGroups)
		{
			if(populationGroup.count <= 0)
			{
				continue;
			}

			totalSavings += populationGroup.savings;
		}

		return Mathf.RoundToInt(totalSavings / totalPopulation);
	}

	public PopulationGroup[] GetPopulationGroups()
	{
		return populationGroups;
	}

	public int GetTotalPopulation()
	{
		return totalPopulation;
	}

	public int GetUnemployedPopulation()
	{
		int unemployed = 0;
		for(int i = populationGroups.Length - 1; i >= 0; --i)
		{
			if(populationGroups[i].income <= 0)
			{
				unemployed += populationGroups[i].count;
			}
			else
			{
				break;
			}
		}

		return unemployed;
	}
}
