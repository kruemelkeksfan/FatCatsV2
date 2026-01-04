using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildingController : PanelObject
{
	private static Player localPlayer = null;

	[SerializeField] private int warehouseBulkPerSize = 2000;
	[SerializeField] private float buildingDegradationFactor = 1.0f;
	[SerializeField] private RectTransform buildingEntryPrefab = null;
	[SerializeField] private RectTransform constructionSiteEntryPrefab = null;
	[SerializeField] private Inventory warehouseInventoryPrefab = null;
	[SerializeField] private Color selectionColor = new Color();
	[SerializeField] private int wageGroupCount = 5;
	private new Transform transform = null;
	private GoodManager goodManager = null;
	private InfoController infoController = null;
	private PopulationController populationController = null;
	private List<Building> buildings = null;
	private Dictionary<Building, ConstructionSite> constructionSites = null;
	private string townName = "Unknown Town";
	private Dictionary<string, Inventory> warehouseInventories = null;
	private Building currentBuilding = null;
	private int currentDestructionCount = 0;
	private int currentWageGroupSetting = 0;

	private static int CompareBuildings(Building lho, Building rho)
	{
		if(lho.owner != rho.owner)
		{
			if(lho.owner == localPlayer)
			{
				return -1;
			}
			else if(rho.owner == localPlayer)
			{
				return 1;
			}

			if(lho.owner == null)
			{
				return -1;
			}
			else if(rho.owner == null)
			{
				return 1;
			}

			return lho.owner.GetPlayerName().CompareTo(rho.owner.GetPlayerName());
		}

		if(lho.underConstruction != rho.underConstruction)
		{
			return lho.underConstruction ? 1 : -1;
		}

		if(lho.buildingData.id != rho.buildingData.id)
		{
			return lho.buildingData.id - rho.buildingData.id;
		}

		if(lho.size != rho.size)
		{
			return rho.size - lho.size;
		}

		return Mathf.RoundToInt(rho.quality * 100.0f) - Mathf.RoundToInt(lho.quality * 100.0f);
	}

	private void Awake()
	{
		transform = gameObject.GetComponent<Transform>();
		populationController = gameObject.GetComponent<PopulationController>();

		buildings = new List<Building>();
		constructionSites = new Dictionary<Building, ConstructionSite>();
		warehouseInventories = new Dictionary<string, Inventory>();
	}

	protected override void Start()
	{
		base.Start();

		goodManager = GoodManager.GetInstance();
		townName = gameObject.GetComponent<Town>().GetTownName();
		infoController = InfoController.GetInstance();

		TimeController timeController = TimeController.GetInstance();
		timeController.AddDailyUpdateListener(UpdateBuildings, TimeController.PriorityCategory.Buildings);
		timeController.AddDailyUpdateListener(UpdateWorkers, TimeController.PriorityCategory.Workers);
	}

	public void UpdateBuildings(double time)
	{
		// TODO: Implement Seasons for Yield Differences (Growth Modifier is determined and retrieved from Time Controller)

		List<Building> buildingsToDestroy = new List<Building>();
		for(int i = 0; i < buildings.Count; ++i)
		{
			// TODO: Connect Town Buildings to Town Warehouse
			if(buildings[i].connectedInventory == null)
			{
				continue;
			}

			// Check wanted Workers instead of real Workers, because it is faster than checking playerWorkers.Count
			if(!buildings[i].underConstruction && buildings[i].wantedWorkers > 0)
			{
				// Resource Checking
				int minProductionBatches = buildings[i].GetCurrentWorkerCount();
				foreach(Tuple<string, int> resourceInput in buildings[i].currentResourceInputs)
				{
					int possibleProductionBatches = buildings[i].connectedInventory.GetInventoryAmount(resourceInput.Item1) / resourceInput.Item2;
					if(possibleProductionBatches < minProductionBatches)
					{
						minProductionBatches = possibleProductionBatches;
						infoController.AddMessage("Not enough " + resourceInput.Item1 + " in " + townName + "!", true, true);
					}
				}

				// Resource Consumption and Resource Quality Calculation
				float resourceQualitySum = 0.0f;
				int totalResourceAmount = 0;
				foreach(Tuple<string, int> resourceInput in buildings[i].currentResourceInputs)
				{
					List<Tuple<Good, int>> withdrawnGoods = buildings[i].connectedInventory.WithdrawGoodUnchecked(resourceInput.Item1, minProductionBatches * resourceInput.Item2, true, false);
					foreach(Tuple<Good, int> withdrawnGood in withdrawnGoods)
					{
						resourceQualitySum += withdrawnGood.Item1.quality * withdrawnGood.Item2;
						totalResourceAmount += withdrawnGood.Item2;
					}
				}

				// Product Quality Calculation
				float productQuality = buildings[i].quality;
				if(totalResourceAmount > 0)
				{
					productQuality = (buildings[i].quality + (resourceQualitySum / totalResourceAmount)) / 2.0f;
				}
				// Special Calculation for Resource Buildings, because those do not profit as much from e.g. Tools and many Resources can rot
				else
				{
					productQuality = 0.5f + (buildings[i].quality * 0.5f);
				}

				// Production
				if(buildings[i].buildingData.products.Length > 0)
				{
					buildings[i].connectedInventory.DepositGood(
						new Good(goodManager.GetGoodData(buildings[i].buildingData.products[buildings[i].currentProductId]), productQuality, productQuality, buildings[i].connectedInventory),
						buildings[i].buildingData.productOutputs[buildings[i].currentProductId] * minProductionBatches);
				}
			}

			// Building Degradation
			// Do not destroy fresh Construction Sites
			if(!(buildings[i].underConstruction && constructionSites[buildings[i]].action == ConstructionSite.Action.Construction && buildings[i].quality <= 0.0f))
			{
				// Quality Loss: y = (1 / (10 * x)) with y: Quality Loss, x: current Quality in %
				if(buildings[i].quality > Mathf.Epsilon)
				{
					buildings[i].quality -= ((1.0f / (buildings[i].quality * 100.0f * 10.0f)) / 100.0f) * buildingDegradationFactor;
				}
				if(buildings[i].quality <= Mathf.Epsilon)
				{
					buildingsToDestroy.Add(buildings[i]);
					infoController.AddMessage(buildings[i].buildingData.buildingName + " in " + townName + " withered away!", true, true);
				}
				else if(buildings[i].quality <= 0.01f && !buildings[i].decayWarningIssued)
				{
					infoController.AddMessage(buildings[i].buildingData.buildingName + " in " + townName + " is in bad Condition!", true, false);
					buildings[i].decayWarningIssued = true;
				}
			}
		}
		foreach(Building building in buildingsToDestroy)
		{
			DestroyBuilding(building);
		}

		// Construction Sites
		List<Building> buildingsUnderConstruction = new List<Building>(constructionSites.Keys);
		foreach(Building building in buildingsUnderConstruction)
		{
			if(constructionSites[building].AdvanceConstruction())
			{
				// Construction
				if(constructionSites[building].action == ConstructionSite.Action.Construction)
				{
					TerminateConstructionSite(building, true);
					infoController.AddMessage("Construction of " + building.buildingData.buildingName + " complete", false, true);
				}
				// Repair
				else if(constructionSites[building].action == ConstructionSite.Action.Repair)
				{
					building.quality += constructionSites[building].materialQuality;
					building.quality = Mathf.Clamp(building.quality, 0.0f, building.buildingStyle.baseQuality); // Clamping is necessary, because necessary Repair Materials are rounded up
					building.decayWarningIssued = false;
					TerminateConstructionSite(building, true);
					infoController.AddMessage("Repair of " + building.buildingData.buildingName + " complete", false, false);
				}
				// Deconstruction
				else if(constructionSites[building].action == ConstructionSite.Action.Deconstruction)
				{
					float materialQuality = building.quality * (1.0f / building.buildingStyle.baseQuality);
					List<Tuple<string, int>> deconstructionMaterials = ConstructionSite.GetDeconstructionMaterials(building, constructionSites[building].destructionCount);
					for(int i = 0; i < building.buildingStyle.materials.Length; i++)
					{
						building.connectedInventory.DepositGood(new Good(
							goodManager.GetGoodData(deconstructionMaterials[i].Item1),
							materialQuality, materialQuality, building.connectedInventory),
							deconstructionMaterials[i].Item2);
					}

					building.size -= constructionSites[building].destructionCount;

					if(building.size <= 0)
					{
						DestroyBuilding(building);
					}
					else
					{
						TerminateConstructionSite(building, false);
					}

					infoController.AddMessage("Deconstruction of " + building.buildingData.buildingName + " complete", false, false);
				}
			}
		}
		buildings.Sort(CompareBuildings);
	}

	public void UpdateWorkers(double time)
	{
		Dictionary<Player, int> playerBudgetsLeft = new Dictionary<Player, int>();
		bool fired = false;
		LinkedList<Tuple<Building, int>> openPositions = new LinkedList<Tuple<Building, int>>(); // Building, Number of open Positions
		int totalOpenPositions = 0;
		for(int i = 0; i < buildings.Count; ++i)
		{
			// Fire unwanted Workers
			while(buildings[i].playerWorkers.Count > 0 && buildings[i].playerWorkers.Count > buildings[i].wantedWorkers)
			{
				buildings[i].playerWorkers[buildings[i].playerWorkers.Count - 1].ResetAction(false, false, true);
			}
			int wantedTownWorkerCount = buildings[i].wantedWorkers - buildings[i].playerWorkers.Count;
			if(buildings[i].townWorkers > wantedTownWorkerCount)
			{
				populationController.ChangeIncome(buildings[i].wage, 0, buildings[i].townWorkers - wantedTownWorkerCount);
				buildings[i].townWorkers = wantedTownWorkerCount;
			}

			// Check Player Liquidity and fire People if they can not be payed later on
			if(buildings[i].wantedWorkers > 0)
			{
				if(!playerBudgetsLeft.ContainsKey(buildings[i].owner))
				{
					playerBudgetsLeft.Add(buildings[i].owner, buildings[i].connectedInventory.GetMoney());
				}

				if(playerBudgetsLeft[buildings[i].owner] < ((buildings[i].wantedWorkers - (buildings[i].playerWorkers.Contains(localPlayer) ? 1 : 0)) * buildings[i].wage))
				{
					fired = true;

					foreach(Player player in buildings[i].playerWorkers)
					{
						if(player != localPlayer)
						{
							player.ResetAction(false, false, true);
						}
					}

					populationController.ChangeIncome(buildings[i].wage, 0, buildings[i].townWorkers);
					buildings[i].townWorkers = 0;
					buildings[i].wantedWorkers = 0;
				}
				else
				{
					playerBudgetsLeft[buildings[i].owner] -= buildings[i].wantedWorkers * buildings[i].wage;
				}
			}

			// Fill Hire List
			int totalWorkerCount = buildings[i].townWorkers + buildings[i].playerWorkers.Count;
			if(totalWorkerCount < buildings[i].wantedWorkers)
			{
				Tuple<Building, int> newOpenPosition = new Tuple<Building, int>(buildings[i], buildings[i].wantedWorkers - totalWorkerCount);
				totalOpenPositions += newOpenPosition.Item2;
				LinkedListNode<Tuple<Building, int>> currentPosition = openPositions.First;
				// Sorted by Wage descending
				while(currentPosition != null && (currentPosition.Value.Item1.wage >= newOpenPosition.Item1.wage))
				{
					currentPosition = currentPosition.Next;
				}

				if(currentPosition != null)
				{
					openPositions.AddBefore(currentPosition, newOpenPosition);
				}
				else
				{
					openPositions.AddFirst(newOpenPosition);
				}
			}
		}
		if(fired)
		{
			infoController.AddMessage("Unable to pay Workers in " + townName + "!", true, true);
		}

		// Update Job Market
		Tuple<Dictionary<Building, int>, Dictionary<int, int>> hireFireLists = populationController.UpdateJobMarket(openPositions);

		// Fire based on Job Market
		foreach(KeyValuePair<int, int> firePosition in hireFireLists.Item2)
		{
			int peopleLeftToFire = firePosition.Value;
			foreach(Building building in buildings)
			{
				if(building.wage == firePosition.Key && building.townWorkers > 0)
				{
					int fireCount = Mathf.Min(building.townWorkers, peopleLeftToFire);

					building.townWorkers -= fireCount;

					peopleLeftToFire -= fireCount;

					if(peopleLeftToFire <= 0)
					{
						break;
					}
				}
			}
			if(peopleLeftToFire > 0)
			{
				Debug.LogWarning("Unable to fire more People, there are not enough People working for " + firePosition.Key + "G to fire " + firePosition.Value + " of them!");
			}
		}

		// Hire based on Job Market
		foreach(KeyValuePair<Building, int> hirePosition in hireFireLists.Item1)
		{
			hirePosition.Key.townWorkers += hirePosition.Value;
		}

		// Pay Workers
		for(int i = 0; i < buildings.Count; ++i)
		{
			if(buildings[i].wantedWorkers > 0)
			{
				// Pay
				foreach(Player player in buildings[i].playerWorkers)
				{
					if(player != localPlayer && player.IsProductive() && !buildings[i].connectedInventory.TransferMoney(player.GetInventory(), buildings[i].wage))
					{
						Debug.LogWarning("Unable to pay " + player.GetPlayerName() + " from " + buildings[i].buildingData.buildingName + " of " + buildings[i].owner.GetPlayerName() + " in " + townName);
					}
				}
				if(!buildings[i].connectedInventory.ChangeMoney(-buildings[i].townWorkers * buildings[i].wage))
				{
					Debug.LogWarning("Unable to pay " + buildings[i].townWorkers + " Town Workers of " + buildings[i].buildingData.buildingName + " of " + buildings[i].owner.GetPlayerName() + " in " + townName);
				}
			}
		}

		panelManager.QueuePanelUpdate(this);
	}

	public override void UpdatePanel(RectTransform panel)
	{
		base.UpdatePanel(panel);

		Inventory playerInventory = EnsurePlayerPresence();
		if(playerInventory == null)
		{
			return;
		}
		Player player = playerInventory.GetPlayer();
		string playerName = player.GetPlayerName();
		if(localPlayer == null)
		{
			localPlayer = player;
		}

		panel.GetChild(0).GetChild(0).GetComponent<TMP_Text>().text = "Buildings - " + townName;

		RectTransform topInfoBar = (RectTransform)panel.GetChild(1);
		topInfoBar.GetChild(1).GetComponent<TMP_Text>().text = populationController.GetUnemployedPopulation() + "/" + populationController.GetTotalPopulation();
		topInfoBar.GetChild(3).GetComponent<TMP_Text>().text = populationController.CalculateAverageIncome() + "G";

		TMP_Dropdown wageGroupSettingDropdown = topInfoBar.GetChild(5).GetComponent<TMP_Dropdown>();
		wageGroupSettingDropdown.ClearOptions();
		for(int j = 1; j <= wageGroupCount; ++j)
		{
			wageGroupSettingDropdown.options.Add(new TMP_Dropdown.OptionData(MathUtil.GetRomanNumber(j)));
		}
		wageGroupSettingDropdown.value = currentWageGroupSetting;
		wageGroupSettingDropdown.RefreshShownValue();
		wageGroupSettingDropdown.onValueChanged.RemoveAllListeners();
		wageGroupSettingDropdown.onValueChanged.AddListener(delegate
		{
			currentWageGroupSetting = wageGroupSettingDropdown.value;
			topInfoBar.GetChild(6).GetComponent<TMP_InputField>().text = populationController.GetWage(playerName, currentWageGroupSetting).ToString();
		});

		TMP_InputField wageGroupField = topInfoBar.GetChild(6).GetComponent<TMP_InputField>();
		wageGroupField.text = populationController.GetWage(playerName, currentWageGroupSetting).ToString();
		wageGroupField.onValueChanged.RemoveAllListeners();
		wageGroupField.onValueChanged.AddListener(delegate
		{
			int newWage = wageGroupField.text != string.Empty ? Mathf.Max(Int32.Parse(wageGroupField.text), 1) : 1;

			populationController.SetWage(playerName, currentWageGroupSetting, newWage, wageGroupCount);

			foreach(Building building in buildings)
			{
				if(building.wageGroup == currentWageGroupSetting)
				{
					populationController.ChangeIncome(building.wage, newWage, building.townWorkers);
					building.wage = newWage;
				}
			}

			panelManager.QueuePanelUpdate(this);
		});

		// LIST
		RectTransform listParent = (RectTransform)panel.GetChild(2).GetChild(0).GetChild(0);

		// Destroy preemptively and repopulate, because there are 2 different Types of Entries which can't be distinguished easily
		while(listParent.childCount > 0)
		{
			Transform child = listParent.GetChild(0);
			child.SetParent(null, false);
			GameObject.Destroy(child.gameObject);
		}

		int i = 1;
		float totalHeight = 0.0f;
		foreach(Building building in buildings)
		{
			Building localBuilding = building;
			RectTransform buildingEntry = null;
			RectTransform buildingInfo;
			// Finished Building
			if(!building.underConstruction)
			{
				buildingEntry = GameObject.Instantiate<RectTransform>(buildingEntryPrefab, listParent);
				buildingEntry.anchoredPosition = new Vector2(buildingEntry.anchoredPosition.x, -totalHeight);

				buildingInfo = (RectTransform)buildingEntry.GetChild(2);

				buildingInfo.GetChild(3).GetComponent<TMP_Text>().text = building.GetCurrentWorkerCount() + "/" + (building.buildingData.maxWorkerCount * building.size);
				buildingInfo.GetChild(4).GetComponent<TMP_Text>().text = building.currentProductId >= 0 ? (building.buildingData.products[building.currentProductId] + " (" + building.CalculateOutput() + "/day)") : "none";
			}
			// Construction Site
			else
			{
				ConstructionSite constructionSite = constructionSites[building];

				buildingEntry = GameObject.Instantiate<RectTransform>(constructionSiteEntryPrefab, listParent);
				buildingEntry.anchoredPosition = new Vector2(buildingEntry.anchoredPosition.x, -totalHeight);

				buildingInfo = (RectTransform)buildingEntry.GetChild(2);
				buildingInfo.GetChild(3).GetComponent<TMP_Text>().text = building.GetCurrentWorkerCount() + "/" + building.wantedWorkers;

				Transform materialInfo = buildingInfo.GetChild(4);
				if(constructionSite.action != ConstructionSite.Action.Deconstruction)
				{
					int storedMaterialSum = 0;
					int necessaryMaterialSum = 0;
					for(int j = 0; j < constructionSite.necessaryBuildingMaterials.Count; ++j)
					{
						storedMaterialSum += constructionSite.storedBuildingMaterials[j].Item2;
						necessaryMaterialSum += constructionSite.necessaryBuildingMaterials[j].Item2;
					}
					materialInfo.GetComponent<TMP_Text>().text = storedMaterialSum + "/" + necessaryMaterialSum;
					materialInfo.GetChild(0).gameObject.SetActive(!constructionSite.enoughMaterial);
				}
				else
				{
					materialInfo.GetComponent<TMP_Text>().text = "none";
					materialInfo.GetChild(0).gameObject.SetActive(false);
				}

				if(building.GetCurrentWorkerCount() > 0)
				{
					buildingInfo.GetChild(5).GetComponent<TMP_Text>().text = MathUtil.GetTimespanString(constructionSites[building].GetTimeLeft());
				}
				else
				{
					buildingInfo.GetChild(5).GetComponent<TMP_Text>().text = "No Workers";
				}
			}

			buildingEntry.GetChild(0).GetComponent<TMP_Text>().text = building.buildingData.buildingName;
			buildingEntry.GetChild(1).GetComponent<TMP_Text>().text = building.owner != null ? building.owner.GetPlayerName() : townName;
			buildingInfo.GetChild(1).GetComponent<TMP_Text>().text = building.size.ToString();
			buildingInfo.GetChild(2).GetComponent<TMP_Text>().text = Mathf.RoundToInt(building.quality * 100.0f) + "%";

			Button listButton = buildingEntry.GetComponent<Button>();
			listButton.onClick.RemoveAllListeners();
			listButton.onClick.AddListener(delegate
			{
				currentBuilding = localBuilding;
				currentDestructionCount = 0;
				panelManager.QueuePanelUpdate(this);
			});

			Image backgroundImage = buildingEntry.GetComponent<Image>();
			if(i % 2 != 0)
			{
				backgroundImage.enabled = false;
			}
			if(currentBuilding != null && building == currentBuilding)
			{
				backgroundImage.color = selectionColor;
				backgroundImage.enabled = true;
			}
			else
			{
				backgroundImage.color = buildingEntryPrefab.GetComponent<Image>().color;
			}

			++i;
			totalHeight += buildingEntry.sizeDelta.y;
		}

		// INFO
		RectTransform infoParent = (RectTransform)panel.GetChild(3);
		if(currentBuilding != null)
		{
			infoParent.GetChild(0).GetComponent<TMP_Text>().text = currentBuilding.buildingData.buildingName;

			RectTransform buildingInfo = (RectTransform)infoParent.GetChild(2);

			// General Info
			buildingInfo.GetChild(1).GetComponent<TMP_Text>().text = currentBuilding.size.ToString();
			buildingInfo.GetChild(3).GetComponent<TMP_Text>().text = Mathf.RoundToInt(currentBuilding.quality * 100.0f) + "%";
			buildingInfo.GetChild(8).GetComponent<TMP_Text>().text = MathUtil.GetTimespanString(CalculateLifespan(currentBuilding.quality));

			// Production
			bool playerOwned = currentBuilding.owner != null && currentBuilding.owner == player;
			TMP_Dropdown productDropdown = buildingInfo.GetChild(6).GetComponent<TMP_Dropdown>();
			TMP_Text productText = buildingInfo.GetChild(7).GetComponent<TMP_Text>();
			TMP_Text outputLabel = buildingInfo.GetChild(4).GetComponent<TMP_Text>();
			TMP_Text outputText = buildingInfo.GetChild(5).GetComponent<TMP_Text>();
			if(currentBuilding.buildingData.products.Length > 1 && playerOwned)
			{
				productDropdown.ClearOptions();
				for(int j = 0; j < currentBuilding.buildingData.products.Length; ++j)
				{
					productDropdown.options.Add(new TMP_Dropdown.OptionData(currentBuilding.buildingData.products[j]));
					if(j == currentBuilding.currentProductId)
					{
						productDropdown.value = j;
					}
				}
				productDropdown.RefreshShownValue();
				productDropdown.onValueChanged.AddListener(delegate
				{
					currentBuilding.currentProductId = productDropdown.value;
					panelManager.QueuePanelUpdate(this);
				});
				outputText.text = currentBuilding.CalculateOutput() + "/day";

				productDropdown.gameObject.SetActive(true);
				productText.gameObject.SetActive(false);
				outputLabel.gameObject.SetActive(true);
				outputText.gameObject.SetActive(true);
			}
			else if(currentBuilding.buildingData.products.Length > 0)
			{
				productText.text = currentBuilding.buildingData.products[currentBuilding.currentProductId];
				outputText.text = currentBuilding.CalculateOutput() + "/day";

				productDropdown.gameObject.SetActive(false);
				productText.gameObject.SetActive(true);
				outputLabel.gameObject.SetActive(true);
				outputText.gameObject.SetActive(true);
			}
			else
			{
				productDropdown.gameObject.SetActive(false);
				productText.gameObject.SetActive(false);
				outputLabel.gameObject.SetActive(false);
				outputText.gameObject.SetActive(false);
			}

			// Input
			StringBuilder resourceString = new StringBuilder();
			bool first = true;
			for(int k = 0; k < currentBuilding.buildingData.resources.Length; ++k)
			{
				if(currentBuilding.buildingData.resourceProductIds[k] == currentBuilding.currentProductId)
				{
					// Necessary, because not all Resources appear in all Recipes, so we can not just use k
					if(!first)
					{
						resourceString.Append(", ");
					}
					first = false;
					resourceString.Append(currentBuilding.buildingData.resources[k]);
					resourceString.Append(" (");
					resourceString.Append((currentBuilding.buildingData.resourceInputs[k] * currentBuilding.GetCurrentWorkerCount()));
					resourceString.Append("/day)");
				}
			}
			if(resourceString.Length <= 0)
			{
				resourceString.Append("none");
			}
			infoParent.GetChild(4).GetChild(1).GetComponent<TMP_Text>().text = resourceString.ToString();

			// Job Display
			RectTransform jobEntry = (RectTransform)infoParent.GetChild(6);

			jobEntry.GetChild(3).GetComponent<TMP_Text>().text = currentBuilding.wage + "G";
			jobEntry.GetChild(5).GetComponent<TMP_Text>().text = currentBuilding.GetCurrentWorkerCount() + "/";
			jobEntry.GetChild(8).GetComponent<TMP_Text>().text = "/" + (currentBuilding.underConstruction ? " - " : (currentBuilding.buildingData.maxWorkerCount * currentBuilding.size));

			TMP_Dropdown wageDropdown = jobEntry.GetChild(2).GetComponent<TMP_Dropdown>();
			TMP_InputField workerAmountField = jobEntry.GetChild(6).GetComponent<TMP_InputField>();
			TMP_Text workerText = jobEntry.GetChild(7).GetComponent<TMP_Text>();
			if(playerOwned)
			{
				wageDropdown.ClearOptions();
				for(int j = 1; j <= wageGroupCount; ++j)
				{
					wageDropdown.options.Add(new TMP_Dropdown.OptionData(MathUtil.GetRomanNumber(j)));
				}
				wageDropdown.value = currentBuilding.wageGroup;
				wageDropdown.RefreshShownValue();
				wageDropdown.onValueChanged.RemoveAllListeners();
				wageDropdown.onValueChanged.AddListener(delegate
				{
					int newWage = populationController.GetWage(playerName, wageDropdown.value);
					if(populationController.ChangeIncome(currentBuilding.wage, newWage, currentBuilding.townWorkers))
					{
						currentBuilding.wage = newWage;
						currentBuilding.wageGroup = wageDropdown.value;
					}

					panelManager.QueuePanelUpdate(this);
				});

				workerAmountField.text = currentBuilding.wantedWorkers.ToString();
				workerAmountField.onEndEdit.RemoveAllListeners();
				workerAmountField.onEndEdit.AddListener(delegate
				{
					int amount = 0;
					if(workerAmountField.text != string.Empty)
					{
						if(currentBuilding.underConstruction)
						{
							amount = Mathf.Max(Int32.Parse(workerAmountField.text), 0);
						}
						else
						{
							amount = Mathf.Clamp(Int32.Parse(workerAmountField.text), 0, currentBuilding.buildingData.maxWorkerCount * currentBuilding.size);
						}

						currentBuilding.wantedWorkers = amount;

						panelManager.QueuePanelUpdate(this);
					}
				});

				wageDropdown.gameObject.SetActive(true);
				workerAmountField.gameObject.SetActive(true);

				workerText.gameObject.SetActive(false);
			}
			else
			{
				workerText.GetComponent<TMP_Text>().text = currentBuilding.wantedWorkers.ToString();

				wageDropdown.gameObject.SetActive(false);
				workerAmountField.gameObject.SetActive(false);

				workerText.gameObject.SetActive(true);
			}

			Button workButton = jobEntry.GetChild(9).GetComponent<Button>();
			if(!currentBuilding.playerWorkers.Contains(localPlayer)
				&& (currentBuilding.playerWorkers.Count < currentBuilding.wantedWorkers
				|| (currentBuilding.owner == localPlayer && (currentBuilding.playerWorkers.Count < currentBuilding.buildingData.maxWorkerCount || currentBuilding.underConstruction))))
			{
				workButton.onClick.RemoveAllListeners();
				workButton.onClick.AddListener(delegate
				{
					localPlayer.WageLabour(currentBuilding, this);
					currentBuilding.playerWorkers.Add(localPlayer);

					if(currentBuilding.townWorkers + currentBuilding.playerWorkers.Count > currentBuilding.wantedWorkers)
					{
						if(currentBuilding.wantedWorkers < currentBuilding.buildingData.maxWorkerCount || currentBuilding.underConstruction)
						{
							++currentBuilding.wantedWorkers;
						}
						else
						{
							populationController.ChangeIncome(currentBuilding.wage, 0, 1);
							currentBuilding.townWorkers -= 1;
						}
					}

					panelManager.QueuePanelUpdate(this);
				});

				workButton.gameObject.SetActive(true);
			}
			else
			{
				workButton.gameObject.SetActive(false);
			}

			// Actions
			RectTransform buildingActions = (RectTransform)infoParent.GetChild(8);
			RectTransform constructionActions = (RectTransform)infoParent.GetChild(9);
			TMP_Text ownerText = infoParent.GetChild(10).GetComponent<TMP_Text>();
			if(playerOwned)
			{
				// Finished Building
				if(!currentBuilding.underConstruction)
				{
					// Job Title
					jobEntry.GetChild(0).GetComponent<TMP_Text>().text = currentBuilding.buildingData.jobTitle;

					TMP_Text repairCostText = buildingActions.GetChild(1).GetComponent<TMP_Text>();
					StringBuilder repairCostString = new StringBuilder();
					repairCostString.Append(MathUtil.GetTimespanString(Mathf.CeilToInt(ConstructionSite.GetRepairTime(currentBuilding))));
					List<Tuple<string, int>> repairMaterials = ConstructionSite.GetRepairMaterials(currentBuilding);
					foreach(Tuple<string, int> repairMaterial in repairMaterials)
					{
						repairCostString.Append(", " + repairMaterial.Item2 + " " + repairMaterial.Item1);
					}
					repairCostText.text = repairCostString.ToString();

					Button repairButton = buildingActions.GetChild(2).GetComponent<Button>();
					repairButton.onClick.RemoveAllListeners();
					repairButton.onClick.AddListener(delegate
					{
						StartConstructionSite(currentBuilding, new ConstructionSite(currentBuilding, ConstructionSite.Action.Repair), true);
					});

					TMP_InputField destructionAmountField = buildingActions.GetChild(5).GetComponent<TMP_InputField>();
					if(currentDestructionCount <= 0)
					{
						currentDestructionCount = currentBuilding.size;
					}
					destructionAmountField.text = currentDestructionCount.ToString();
					destructionAmountField.onEndEdit.RemoveAllListeners();
					destructionAmountField.onEndEdit.AddListener(delegate
					{
						currentDestructionCount = destructionAmountField.text != string.Empty ? Mathf.Clamp(Int32.Parse(destructionAmountField.text), 1, currentBuilding.size) : 1;
						panelManager.QueuePanelUpdate(this);
					});

					TMP_Text destructionGainText = buildingActions.GetChild(4).GetComponent<TMP_Text>();
					StringBuilder destructionGainString = new StringBuilder();
					destructionGainString.Append(MathUtil.GetTimespanString(Mathf.CeilToInt(ConstructionSite.GetDeconstructionTime(currentBuilding, currentDestructionCount))));
					List<Tuple<string, int>> deconstructionMaterials = ConstructionSite.GetDeconstructionMaterials(currentBuilding, currentDestructionCount);
					foreach(Tuple<string, int> deconstructionMaterial in deconstructionMaterials)
					{
						destructionGainString.Append(", " + deconstructionMaterial.Item2 + " " + deconstructionMaterial.Item1);
					}
					destructionGainText.text = destructionGainString.ToString();

					Button destructButton = buildingActions.GetChild(6).GetComponent<Button>();
					destructButton.onClick.RemoveAllListeners();
					destructButton.onClick.AddListener(delegate
					{
						StartConstructionSite(currentBuilding, new ConstructionSite(currentBuilding, ConstructionSite.Action.Deconstruction, currentDestructionCount), true);
					});

					buildingActions.gameObject.SetActive(true);
					constructionActions.gameObject.SetActive(false);
					ownerText.gameObject.SetActive(false);
				}
				// Construction Site
				else
				{
					ConstructionSite constructionSite = constructionSites[currentBuilding];

					// Job Title
					jobEntry.GetChild(0).GetComponent<TMP_Text>().text = "Construction Worker";

					Button addMaterialButton = constructionActions.GetChild(2).GetComponent<Button>();
					TMP_Text constructionCostText = constructionActions.GetChild(1).GetComponent<TMP_Text>();
					if(constructionSite.action != ConstructionSite.Action.Deconstruction)
					{
						// Building Materials
						constructionActions.GetChild(0).GetComponent<TMP_Text>().text = "Missing Materials:";
						StringBuilder costString = new StringBuilder();
						bool missingMaterials = false;
						for(int k = 0; k < constructionSite.necessaryBuildingMaterials.Count; ++k)
						{
							if(k > 0)
							{
								costString.Append(", ");
							}

							int missingMaterialAmount = constructionSite.necessaryBuildingMaterials[k].Item2 - constructionSite.storedBuildingMaterials[k].Item2;
							if(missingMaterialAmount > 0)
							{
								missingMaterials = true;
							}

							costString.Append(missingMaterialAmount);
							costString.Append(" ");
							costString.Append(constructionSite.necessaryBuildingMaterials[k].Item1);
						}
						if(costString.Length <= 0)
						{
							costString.Append("none");
						}
						constructionCostText.text = costString.ToString();

						// Add Materials
						if(missingMaterials)
						{
							addMaterialButton.onClick.RemoveAllListeners();
							addMaterialButton.onClick.AddListener(delegate
							{
								int totalNecessaryBuildingMaterials = 0;
								for(int j = 0; j < constructionSite.necessaryBuildingMaterials.Count; ++j)
								{
									totalNecessaryBuildingMaterials += constructionSite.necessaryBuildingMaterials[j].Item2;
								}

								for(int j = 0; j < constructionSite.necessaryBuildingMaterials.Count; ++j)
								{
									int storedAmount = constructionSite.storedBuildingMaterials[j].Item2;
									float addedQuality = 0.0f;

									List<Tuple<Good, int>> sortedInventoryContents = playerInventory.GetStoredGoods(constructionSite.necessaryBuildingMaterials[j].Item1, Inventory.SortType.PerceivedQualityDescending);
									foreach(Tuple<Good, int> inventoryGood in sortedInventoryContents)
									{
										int addedAmount = playerInventory.WithdrawGoodPartially(inventoryGood.Item1, constructionSite.necessaryBuildingMaterials[j].Item2 - storedAmount, true);
										storedAmount += addedAmount;
										addedQuality += (addedAmount * inventoryGood.Item1.quality * currentBuilding.buildingStyle.baseQuality) / totalNecessaryBuildingMaterials;
										if(addedAmount > 0)
										{
											constructionSite.enoughMaterial = true;
										}
										if(storedAmount >= constructionSite.necessaryBuildingMaterials[j].Item2)
										{
											break;
										}
									}

									if(storedAmount < constructionSite.necessaryBuildingMaterials[j].Item2 && warehouseInventories.ContainsKey(playerName))
									{
										sortedInventoryContents = warehouseInventories[playerName].GetStoredGoods(constructionSite.necessaryBuildingMaterials[j].Item1, Inventory.SortType.PerceivedQualityDescending);
										foreach(Tuple<Good, int> inventoryGood in sortedInventoryContents)
										{
											int addedAmount = warehouseInventories[playerName].WithdrawGoodPartially(inventoryGood.Item1, constructionSite.necessaryBuildingMaterials[j].Item2 - storedAmount, false);
											storedAmount += addedAmount;
											addedQuality += (addedAmount * inventoryGood.Item1.quality * currentBuilding.buildingStyle.baseQuality) / totalNecessaryBuildingMaterials;
											if(addedAmount > 0)
											{
												constructionSite.enoughMaterial = true;
											}
											if(storedAmount >= constructionSite.necessaryBuildingMaterials[j].Item2)
											{
												break;
											}
										}
									}

									constructionSite.materialQuality += addedQuality;

									constructionSite.storedBuildingMaterials[j] = new Tuple<string, int>(constructionSite.storedBuildingMaterials[j].Item1, storedAmount);
									if(constructionSite.action == ConstructionSite.Action.Construction)
									{
										currentBuilding.quality += addedQuality;
										currentBuilding.quality = Mathf.Clamp(currentBuilding.quality, 0.0f, currentBuilding.buildingStyle.baseQuality); // Clamping is necessary, because necessary Repair Materials are rounded up
									}

									panelManager.QueuePanelUpdate(this);
								}
							});
							addMaterialButton.gameObject.SetActive(true);
						}
						else
						{
							addMaterialButton.gameObject.SetActive(false);
						}
					}
					else
					{
						// Building Materials
						constructionActions.GetChild(0).GetComponent<TMP_Text>().text = "Deconstruction Yield:";
						StringBuilder destructionGainString = new StringBuilder();
						List<Tuple<string, int>> deconstructionMaterials = ConstructionSite.GetDeconstructionMaterials(currentBuilding, currentDestructionCount);
						for(int j = 0; j < deconstructionMaterials.Count; j++)
						{
							if(j > 0)
							{
								destructionGainString.Append(", ");
							}
							destructionGainString.Append(deconstructionMaterials[j].Item2 + " " + deconstructionMaterials[j].Item1);
						}

						constructionCostText.text = destructionGainString.ToString();

						addMaterialButton.gameObject.SetActive(false);
					}

					// Cancel Building Operation
					Button cancelButton = constructionActions.GetChild(3).GetComponent<Button>();
					cancelButton.onClick.RemoveAllListeners();
					cancelButton.onClick.AddListener(delegate
					{
						infoController.ActivateConfirmationPanel("Do you want to abort the Building Operation?", delegate
						{
							float materialQuality = constructionSite.materialQuality / currentBuilding.buildingStyle.baseQuality;
							if(constructionSite.action == ConstructionSite.Action.Construction)
							{
								materialQuality = currentBuilding.quality / currentBuilding.buildingStyle.baseQuality;
								buildings.Remove(currentBuilding);
							}
							for(int i = 0; i < constructionSite.storedBuildingMaterials.Count; ++i)
							{
								if(!(warehouseInventories.ContainsKey(playerName) && warehouseInventories[playerName].DepositGood(new Good(
									goodManager.GetGoodData(constructionSite.storedBuildingMaterials[i].Item1),
									materialQuality, materialQuality, warehouseInventories[playerName]),
									constructionSite.storedBuildingMaterials[i].Item2)))
								{
									playerInventory.DepositGood(new Good(
										goodManager.GetGoodData(constructionSite.storedBuildingMaterials[i].Item1),
										materialQuality, materialQuality, playerInventory),
										constructionSite.storedBuildingMaterials[i].Item2);
								}
							}

							TerminateConstructionSite(currentBuilding, !(constructionSite.action == ConstructionSite.Action.Construction));
							currentBuilding = null;
							currentDestructionCount = 0;
						});
					});
					cancelButton.gameObject.SetActive(true);

					buildingActions.gameObject.SetActive(false);
					constructionActions.gameObject.SetActive(true);
					ownerText.gameObject.SetActive(false);
				}
			}
			// Not Player owned
			else
			{
				ownerText.text = "owned by\n" + (currentBuilding.owner != null ? currentBuilding.owner.GetPlayerName() : townName);

				buildingActions.gameObject.SetActive(false);
				constructionActions.gameObject.SetActive(false);
				ownerText.gameObject.SetActive(true);
			}

			infoParent.gameObject.SetActive(true);
		}
		else
		{
			infoParent.gameObject.SetActive(false);
		}

		listParent.sizeDelta = new Vector2(listParent.sizeDelta.x, totalHeight);
	}

	public bool KillTownWorkers(int income, int count)
	{
		if(count <= 0)
		{
			return true;
		}

		int peopleLeftToKill = count;
		foreach(Building building in buildings)
		{
			if(building.wage == income)
			{
				int fireCount = Mathf.Min(building.townWorkers, peopleLeftToKill);
				building.townWorkers -= fireCount;

				peopleLeftToKill -= fireCount;

				if(peopleLeftToKill <= 0)
				{
					return true;
				}
			}
		}

		Debug.LogWarning("Not enough Workers to kill " + count + " People with an Income of " + income + "G");
		return false;
	}

	public void OrderBuilding(BuildingData buildingData, BuildingStyle buildingStyle, int constructionCount, Player owner = null)
	{
		if(owner == null)
		{
			Inventory playerInventory = EnsurePlayerPresence();
			if(playerInventory == null)
			{
				Debug.LogWarning("Player is not present but can order Building in " + townName);
				return;
			}
			owner = playerInventory.GetPlayer();
		}
		string ownerName = owner.GetPlayerName();

		Building building = new Building(buildingData, buildingStyle, constructionCount, populationController.GetWage(ownerName, 0), warehouseInventories[ownerName], owner);
		buildings.Add(building);
		buildings.Sort(CompareBuildings);

		StartConstructionSite(building, new ConstructionSite(building, ConstructionSite.Action.Construction), false);

		panelManager.OpenPanel(this);
	}

	public void StartConstructionSite(Building building, ConstructionSite constructionSite, bool fireWorkers)
	{
		if(building.buildingData.buildingName == "Warehouse"
			&& (constructionSite.action == ConstructionSite.Action.Repair || constructionSite.action == ConstructionSite.Action.Deconstruction))
		{
			string ownerName = (building.owner != null) ? building.owner.GetPlayerName() : ("/" + townName);
			warehouseInventories[ownerName].ChangeBulkCapacity(Mathf.FloorToInt(-building.size * building.buildingStyle.baseQuality * warehouseBulkPerSize));
		}

		building.underConstruction = true;
		constructionSites.Add(building, constructionSite);

		if(fireWorkers)
		{
			populationController.ChangeIncome(building.wage, 0, building.townWorkers);
			Player[] firedPlayerWorkers = building.playerWorkers.ToArray();
			foreach(Player player in firedPlayerWorkers)
			{
				player.ResetAction(false, false, true);
			}
			building.townWorkers = 0;
			building.wantedWorkers = 0;
		}

		panelManager.QueuePanelUpdate(this);
	}

	public void TerminateConstructionSite(Building building, bool increaseInventoryCapacity)
	{
		if(building.buildingData.buildingName == "Warehouse" && building.underConstruction && increaseInventoryCapacity)
		{
			string ownerName = (building.owner != null) ? building.owner.GetPlayerName() : ("/" + townName);
			warehouseInventories[ownerName].ChangeBulkCapacity(Mathf.FloorToInt(building.size * building.buildingStyle.baseQuality * warehouseBulkPerSize));
		}

		building.underConstruction = false;
		constructionSites.Remove(building);

		populationController.ChangeIncome(building.wage, 0, building.townWorkers);
		Player[] firedPlayerWorkers = building.playerWorkers.ToArray();
		foreach(Player player in firedPlayerWorkers)
		{
			player.ResetAction(false, false, true);
		}
		building.townWorkers = 0;
		building.wantedWorkers = 0;

		panelManager.QueuePanelUpdate(this);
	}

	public void DestroyBuilding(Building building)
	{
		populationController.ChangeIncome(building.wage, 0, building.townWorkers);
		Player[] firedPlayerWorkers = building.playerWorkers.ToArray();
		foreach(Player player in firedPlayerWorkers)
		{
			player.ResetAction(false, false, true);
			infoController.AddMessage("Fired " + player.GetPlayerName() + "!", true, false);
		}

		if(building == currentBuilding)
		{
			currentBuilding = null;
			currentDestructionCount = 0;
		}

		if(building.underConstruction)
		{
			TerminateConstructionSite(building, false);
		}
		else if(building.buildingData.buildingName == "Warehouse")
		{
			string ownerName = (building.owner != null) ? building.owner.GetPlayerName() : ("/" + townName);
			warehouseInventories[ownerName].ChangeBulkCapacity(Mathf.FloorToInt(-building.size * building.buildingStyle.baseQuality * warehouseBulkPerSize));
		}
		buildings.Remove(building);

		panelManager.QueuePanelUpdate(this);
	}

	public int CalculateLifespan(float quality)
	{
		// Quality Loss: y = (1 / (10 * x)) with y: Quality Loss, x: current Quality in %
		float currentQuality = quality;
		int daysToLive = 0;
		while(currentQuality > Mathf.Epsilon)
		{
			currentQuality -= ((1.0f / (currentQuality * 100.0f * 10.0f)) / 100.0f) * buildingDegradationFactor;
			++daysToLive;
		}

		return daysToLive;
	}

	public void AddPlayerWarehouseInventory(Player player)
	{
		string playerName = player.GetPlayerName();
		warehouseInventories[playerName] = GameObject.Instantiate<Inventory>(warehouseInventoryPrefab, transform);
		// We can not use the buffered townName here, because this Method gets called during Start() and the Buffer might not be initialized yet
		warehouseInventories[playerName].SetPlayer(player, gameObject.GetComponent<Town>().GetTownName());
	}

	// TODO: Introduce a Flag Variable in Inventory Class, which gets updated on hire/fire, so that we do not have to iterate all Buildings on every Update Tick
	public bool IsWarehouseAdministered(Player player)
	{
		foreach(Building building in buildings)
		{
			if(!building.underConstruction && building.owner == player && building.buildingData.buildingName == "Warehouse" && building.GetCurrentWorkerCount() > 0)
			{
				return true;
			}
		}

		return false;
	}

	public Inventory GetWarehouseInventory(string playerName)
	{
		return warehouseInventories[playerName];
	}

	public ConstructionSite GetConstructionSite(Building building)
	{
		return constructionSites[building];
	}
}
