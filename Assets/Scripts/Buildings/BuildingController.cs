using System;
using System.Collections.Generic;
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
	[SerializeField] private RectTransform newBuildingEntryPrefab = null;
	[SerializeField] private RectTransform jobEntryPrefab = null;
	[SerializeField] private Inventory warehouseInventoryPrefab = null;
	private new Transform transform = null;
	private BuildingManager buildingManager = null;
	private GoodManager goodManager = null;
	private InfoController infoController = null;
	private BuildingStyle[] availableBuildingStyles = null;
	private int currentBuildingStyle = 0;
	private List<Building> buildings = null;
	private Dictionary<Building, ConstructionSite> constructionSites = null;
	private PopulationController populationController = null;
	private string townName = "Unknown Town";
	private Dictionary<int, List<Tuple<int, int>>> jobsByWage = null;
	private Dictionary<string, Inventory> warehouseInventories = null;

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
		}

		if(lho.underConstruction != rho.underConstruction)
		{
			return lho.underConstruction ? -1 : 1;
		}

		if(lho.buildingData.buildingName != rho.buildingData.buildingName)
		{
			if(lho.buildingData.buildingName == "Warehouse")
			{
				return -1;
			}
			else if(rho.buildingData.buildingName == "Warehouse")
			{
				return 1;
			}

			return lho.buildingData.buildingName.CompareTo(rho.buildingData.buildingName);
		}

		if(lho.owner != rho.owner)
		{
			if(lho.owner == null)
			{
				return -1;
			}
			else if(rho.owner == null)
			{
				return 1;
			}
		}

		if(lho.size != rho.size)
		{
			return lho.size - rho.size;
		}

		return Mathf.RoundToInt(rho.quality * 100.0f) - Mathf.RoundToInt(lho.quality * 100.0f);
	}

	private void Awake()
	{
		transform = gameObject.GetComponent<Transform>();
		populationController = gameObject.GetComponent<PopulationController>();

		buildings = new List<Building>();
		constructionSites = new Dictionary<Building, ConstructionSite>();
		jobsByWage = new Dictionary<int, List<Tuple<int, int>>>();
		warehouseInventories = new Dictionary<string, Inventory>();
	}

	protected override void Start()
	{
		base.Start();

		buildingManager = BuildingManager.GetInstance();
		goodManager = GoodManager.GetInstance();
		availableBuildingStyles = buildingManager.GetBuildingStyles();
		townName = gameObject.GetComponent<Town>().GetTownName();
		infoController = InfoController.GetInstance();
	}

	public override void UpdatePanel(RectTransform panel, bool add = true)
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

		panel.GetChild(2).GetComponent<TMP_Text>().text = populationController.GetUnemployedPopulation() + "/" + populationController.GetTotalPopulation();
		panel.GetChild(4).GetComponent<TMP_Text>().text = populationController.CalculateAverageIncome() + "G";

		RectTransform buildingsParent = (RectTransform)panel.GetChild(5).GetChild(0).GetChild(0);

		// Destroy preemptively and repopulate, because there are 2 different Types of Entries which can't be distinguished easily
		while(buildingsParent.childCount > 0)
		{
			Transform child = buildingsParent.GetChild(0);
			child.SetParent(null, false);
			GameObject.Destroy(child.gameObject);
		}

		int i = 1;
		float totalHeight = 0.0f;
		Building[] iterationBuildings = buildings.ToArray();
		foreach(Building building in iterationBuildings)
		{
			Building localBuilding = building;

			RectTransform buildingEntry = GameObject.Instantiate<RectTransform>(building.underConstruction ? constructionSiteEntryPrefab : buildingEntryPrefab, buildingsParent);
			buildingEntry.anchoredPosition = new Vector2(buildingEntry.anchoredPosition.x, -totalHeight);

			RectTransform buildingInfo = (RectTransform)buildingEntry.GetChild(0);

			buildingInfo.GetChild(1).GetComponent<TMP_Text>().text = building.buildingData.buildingName;
			buildingInfo.GetChild(2).GetComponent<TMP_Text>().text = building.size.ToString();
			buildingInfo.GetChild(3).GetComponent<TMP_Text>().text = Mathf.RoundToInt(building.quality * 100.0f) + "%";

			// CONSTRUCTION SITE
			if(building.underConstruction)
			{
				ConstructionSite constructionSite = constructionSites[building];

				Transform daysLeftText = buildingInfo.GetChild(4);
				if(building.jobs[0].townWorkers + building.jobs[0].playerWorkers.Count > 0)
				{
					daysLeftText.GetComponent<TMP_Text>().text = constructionSite.GetTimeLeft() + " Days";
				}
				else
				{
					daysLeftText.GetComponent<TMP_Text>().text = "No Workers";
				}
				daysLeftText.gameObject.SetActive(true);

				string buildingMaterialText = "";
				for(int j = 0; j < constructionSite.necessaryBuildingMaterials.Count; ++j)
				{
					buildingMaterialText += (j > 0 ? ", " : "") + constructionSite.necessaryBuildingMaterials[j].Item1
						+ " [" + constructionSite.storedBuildingMaterials[j].Item2 + "/" + constructionSite.necessaryBuildingMaterials[j].Item2 + "]";
				}
				buildingInfo.GetChild(6).GetComponent<TMP_Text>().text = buildingMaterialText;

				buildingInfo.GetChild(7).GetComponent<Image>().enabled = !constructionSite.enoughMaterial;

				RectTransform jobEntryParent = (RectTransform)buildingEntry.GetChild(1);

				bool playerOwned = building.owner != null && building.owner == player;
				if(playerOwned)
				{
					if(constructionSite.action != ConstructionSite.Action.Deconstruction)
					{
						Button addMaterialButton = buildingInfo.GetChild(5).GetComponent<Button>();
						addMaterialButton.onClick.RemoveAllListeners();
						addMaterialButton.onClick.AddListener(delegate
						{
							for(int j = 0; j < constructionSite.necessaryBuildingMaterials.Count; ++j)
							{
								int storedAmount = constructionSite.storedBuildingMaterials[j].Item2;

								List<Tuple<Good, int>> sortedInventoryContents = playerInventory.GetStoredGoods(constructionSite.necessaryBuildingMaterials[j].Item1, Inventory.SortType.PerceivedQualityDescending);
								foreach(Tuple<Good, int> inventoryGood in sortedInventoryContents)
								{
									int addedAmount = playerInventory.WithdrawGoodPartially(inventoryGood.Item1, constructionSite.necessaryBuildingMaterials[j].Item2 - storedAmount, true);
									storedAmount += addedAmount;
									constructionSite.newQuality += (addedAmount * inventoryGood.Item1.quality * localBuilding.buildingStyle.baseQuality) / constructionSite.GetConstructionCost(j);
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
										constructionSite.newQuality += (addedAmount * inventoryGood.Item1.quality * localBuilding.buildingStyle.baseQuality) / constructionSite.GetConstructionCost(j);
										if(storedAmount >= constructionSite.necessaryBuildingMaterials[j].Item2)
										{
											break;
										}
									}
								}

								constructionSite.storedBuildingMaterials[j] = new Tuple<string, int>(constructionSite.storedBuildingMaterials[j].Item1, storedAmount);
								if(constructionSite.action == ConstructionSite.Action.Construction)
								{
									building.quality = constructionSite.newQuality;
								}

								panelManager.QueuePanelUpdate(this);
							}
						});
						addMaterialButton.gameObject.SetActive(true);
					}
					else
					{
						buildingInfo.GetChild(5).gameObject.SetActive(false);
					}

					// Cancel Building Operation
					Button cancelButton = buildingInfo.GetChild(8).GetComponent<Button>();
					cancelButton.onClick.RemoveAllListeners();
					cancelButton.onClick.AddListener(delegate
					{
						infoController.ActivateConfirmationPanel("Do you want to abort Building Operation?", delegate
						{
							TerminateConstructionSite(localBuilding, false);

							// Construction
							if(constructionSite.action == ConstructionSite.Action.Construction)
							{
								buildings.Remove(localBuilding);
								RefundBuildingCosts(playerName, playerInventory, constructionSite, building.quality * (1.0f / building.buildingStyle.baseQuality));
							}
							// Repair
							else if(constructionSite.action == ConstructionSite.Action.Repair)
							{
								RefundBuildingCosts(playerName, playerInventory, constructionSite, constructionSite.newQuality * (1.0f / building.buildingStyle.baseQuality));
							}

							panelManager.QueuePanelUpdate(this);
						});
					});
					cancelButton.gameObject.SetActive(true);

					buildingInfo.GetChild(0).GetChild(3).gameObject.SetActive(true);
					buildingEntry.GetChild(2).gameObject.SetActive(false);
				}
				else
				{
					buildingInfo.GetChild(0).GetChild(3).gameObject.SetActive(false);
					buildingInfo.GetChild(4).gameObject.SetActive(false);
					buildingInfo.GetChild(5).gameObject.SetActive(false);
					buildingInfo.GetChild(8).gameObject.SetActive(false);

					TMP_Text ownerText = buildingEntry.GetChild(2).GetComponent<TMP_Text>();
					ownerText.text = "owned by\n" + (building.owner != null ? building.owner.GetPlayerName() : townName);
					ownerText.gameObject.SetActive(true);
				}

				UpdateJobEntries(i - 1, buildingEntry, jobEntryParent,
						new Job[] { building.jobs[0] },
						null, playerOwned);
			}
			// WORKING BUILDING
			else
			{
				// Product Display and Setting
				if(building.buildingData.products.Length > 1 && building.owner != null && building.owner == player)
				{
					Button productButton = buildingInfo.GetChild(4).GetComponent<Button>();
					productButton.GetComponentInChildren<TMP_Text>().text = building.buildingData.products[building.currentProductId];
					productButton.onClick.RemoveAllListeners();
					productButton.onClick.AddListener(delegate
					{
						localBuilding.ChangeProduction(panelManager, this);
					});

					productButton.gameObject.SetActive(true);
					buildingInfo.GetChild(5).gameObject.SetActive(false);
				}
				else if(building.buildingData.products.Length > 0)
				{
					TMP_Text productText = buildingInfo.GetChild(5).GetComponent<TMP_Text>();
					productText.text = building.buildingData.products[building.currentProductId];

					buildingInfo.GetChild(4).gameObject.SetActive(false);
					productText.gameObject.SetActive(true);
				}
				else
				{
					buildingInfo.GetChild(4).gameObject.SetActive(false);
					buildingInfo.GetChild(5).gameObject.SetActive(false);
				}

				// Output Display
				if(building.currentProductId >= 0)
				{
					TMP_Text outputText = buildingInfo.GetChild(6).GetComponent<TMP_Text>();
					outputText.text = building.CalculateOutput() + "/day";

					buildingInfo.GetChild(0).GetChild(3).gameObject.SetActive(true);
					outputText.gameObject.SetActive(true);
				}
				else
				{
					buildingInfo.GetChild(0).GetChild(3).gameObject.SetActive(false);
					buildingInfo.GetChild(6).gameObject.SetActive(false);
				}

				// Resource Display
				string resourceText = "";
				for(int j = 0; j < building.currentResourceInputs.Count; ++j)
				{
					resourceText += (j > 0 ? ", " : "") + building.currentResourceInputs[j].Item1 + " [" + building.currentResourceInputs[j].Item2 + "/day]";
				}
				buildingInfo.GetChild(7).GetComponent<TMP_Text>().text = resourceText;

				bool playerOwned = building.owner != null && building.owner == player;

				// Job Display
				RectTransform jobEntryParent = (RectTransform)buildingEntry.GetChild(1);
				UpdateJobEntries(i - 1, buildingEntry, jobEntryParent, building.jobs, building.buildingData.maxWorkerCounts, playerOwned);

				// Action Display
				RectTransform actionInfo = (RectTransform)buildingEntry.GetChild(2);
				if(playerOwned)
				{
					TMP_Text repairCostText = actionInfo.GetChild(0).GetComponent<TMP_Text>();
					UpdateRepairCost(building, repairCostText);
					Button repairButton = actionInfo.GetChild(1).GetComponent<Button>();
					repairButton.onClick.RemoveAllListeners();
					repairButton.onClick.AddListener(delegate
					{
						StartConstructionSite(building, new ConstructionSite(building, ConstructionSite.Action.Repair), true, true);
					});

					TMP_Text destructionGainText = actionInfo.GetChild(2).GetComponent<TMP_Text>();
					TMP_InputField destructionAmountField = actionInfo.GetChild(3).GetComponent<TMP_InputField>();
					UpdateDestructionGain(building, destructionAmountField, destructionGainText);
					destructionAmountField.onValueChanged.RemoveAllListeners();
					destructionAmountField.onValueChanged.AddListener(delegate
					{
						UpdateDestructionGain(building, destructionAmountField, destructionGainText);
					});
					Button destructButton = actionInfo.GetChild(4).GetComponent<Button>();
					destructButton.onClick.RemoveAllListeners();
					destructButton.onClick.AddListener(delegate
					{
						int destructionAmount = destructionAmountField.text != string.Empty ? Mathf.Clamp(Int32.Parse(destructionAmountField.text), 1, building.size) : 1;
						StartConstructionSite(building, new ConstructionSite(building, ConstructionSite.Action.Deconstruction, destructionAmount), true, true);
					});

					actionInfo.gameObject.SetActive(true);
					buildingEntry.GetChild(3).gameObject.SetActive(false);
				}
				else
				{
					TMP_Text ownerText = buildingEntry.GetChild(3).GetComponent<TMP_Text>();
					ownerText.text = "owned by " + (building.owner != null ? building.owner.GetPlayerName() : townName);

					actionInfo.gameObject.SetActive(false);
					ownerText.gameObject.SetActive(true);
				}
			}

			// Line Visibility Background
			if(i % 2 == 0)
			{
				buildingEntry.GetComponent<Image>().enabled = false;
			}

			++i;
			totalHeight += buildingEntry.sizeDelta.y;
		}

		// NEW BUILDINGS
		BuildingData[] buildingDataList = buildingManager.GetBuildingData();
		foreach(BuildingData buildingData in buildingDataList)
		{
			RectTransform newBuildingEntry = GameObject.Instantiate<RectTransform>(newBuildingEntryPrefab, buildingsParent);
			newBuildingEntry.anchoredPosition = new Vector2(newBuildingEntry.anchoredPosition.x, -totalHeight);

			RectTransform buildingInfo = (RectTransform)newBuildingEntry.GetChild(0);

			buildingInfo.GetChild(1).GetComponent<TMP_Text>().text = buildingData.buildingName;

			if(buildingData.products.Length > 0)
			{
				string productString = buildingData.products[0];
				for(int j = 1; j < buildingData.products.Length; ++j)
				{
					productString += ", " + buildingData.products[j];
				}

				Transform productText = buildingInfo.GetChild(2);
				productText.GetComponent<TMP_Text>().text = productString;
				productText.gameObject.SetActive(true);
			}
			else
			{
				buildingInfo.GetChild(2).gameObject.SetActive(false);
			}

			RectTransform actionInfo = (RectTransform)newBuildingEntry.GetChild(1);

			Button buildingStyleButton = actionInfo.GetChild(0).GetComponent<Button>();
			buildingStyleButton.GetComponentInChildren<TMP_Text>().text = availableBuildingStyles[currentBuildingStyle].buildingStyleName;
			buildingStyleButton.onClick.RemoveAllListeners();
			buildingStyleButton.onClick.AddListener(delegate
			{
				currentBuildingStyle = (currentBuildingStyle + 1) % availableBuildingStyles.Length;
				panelManager.QueuePanelUpdate(this);
			});

			actionInfo.GetChild(2).GetComponent<TMP_Text>().text = Mathf.RoundToInt(availableBuildingStyles[currentBuildingStyle].baseQuality * 100.0f) + "%";

			TMP_Text buildingCostText = actionInfo.GetChild(3).GetComponent<TMP_Text>();
			TMP_InputField buildingAmountField = actionInfo.GetChild(4).GetComponent<TMP_InputField>();
			UpdateBuildingCosts(buildingData, availableBuildingStyles[currentBuildingStyle], playerInventory, player, buildingAmountField, buildingCostText);
			buildingAmountField.onValueChanged.RemoveAllListeners();
			buildingAmountField.onValueChanged.AddListener(delegate
			{
				UpdateBuildingCosts(buildingData, availableBuildingStyles[currentBuildingStyle], playerInventory, player, buildingAmountField, buildingCostText);
			});

			BuildingData localBuildingData = buildingData;
			Button buildButton = actionInfo.GetChild(5).GetComponent<Button>();
			buildButton.onClick.RemoveAllListeners();
			buildButton.onClick.AddListener(delegate
			{
				Building building = new Building(buildingData,
					availableBuildingStyles[currentBuildingStyle],
					buildingAmountField.text != string.Empty ? Mathf.Max(Int32.Parse(buildingAmountField.text), 1) : 1,
					warehouseInventories[playerName], player);
				buildings.Add(building);
				buildings.Sort(CompareBuildings);

				StartConstructionSite(building, new ConstructionSite(building, ConstructionSite.Action.Construction), false, false);
			});

			if(i % 2 == 0)
			{
				newBuildingEntry.GetComponent<Image>().enabled = false;
			}

			++i;
			totalHeight += newBuildingEntry.sizeDelta.y;
		}

		buildingsParent.sizeDelta = new Vector2(buildingsParent.sizeDelta.x, totalHeight);
	}

	private void UpdateJobEntries(int buildingId, RectTransform buildingEntry, RectTransform jobEntryParent, Job[] jobs, int[] maxWorkerCounts, bool playerOwned)
	{
		int localBuildingId = buildingId;
		float totalHeight = 0.0f;
		for(int i = (maxWorkerCounts != null ? 1 : 0); i < (maxWorkerCounts != null ? jobs.Length : 1); ++i)
		{
			RectTransform jobEntry = GameObject.Instantiate<RectTransform>(jobEntryPrefab, jobEntryParent);
			jobEntry.anchoredPosition = new Vector2(jobEntryParent.anchoredPosition.x, -jobEntry.sizeDelta.y * ((maxWorkerCounts != null ? 0 : 1) + i));

			jobEntry.GetChild(0).GetComponent<TMP_Text>().text = jobs[i].jobName;
			jobEntry.GetChild(1).GetComponent<TMP_Text>().text = (jobs[i].townWorkers + jobs[i].playerWorkers.Count) + "/";
			jobEntry.GetChild(4).GetComponent<TMP_Text>().text = "/" + (maxWorkerCounts != null ? (maxWorkerCounts[i - 1] * buildings[buildingId].size) : " - ");

			int localI = i;
			if(playerOwned)
			{
				TMP_InputField workerAmountField = jobEntry.GetChild(2).GetComponent<TMP_InputField>();
				workerAmountField.text = jobs[localI].wantedWorkers.ToString();
				workerAmountField.onEndEdit.RemoveAllListeners();
				workerAmountField.onEndEdit.AddListener(delegate
				{
					int amount = 0;
					if(workerAmountField.text != string.Empty)
					{
						if(maxWorkerCounts != null)
						{
							amount = Mathf.Clamp(Int32.Parse(workerAmountField.text), 0, maxWorkerCounts[localI - 1] * buildings[localBuildingId].size);
						}
						else
						{
							amount = Mathf.Max(Int32.Parse(workerAmountField.text), 0);
						}
					}

					buildings[buildingId].jobs[localI].wantedWorkers = amount;

					panelManager.QueuePanelUpdate(this);
				});

				TMP_InputField wageAmountField = jobEntry.GetChild(5).GetComponent<TMP_InputField>();
				wageAmountField.text = jobs[i].wage.ToString();
				wageAmountField.onEndEdit.RemoveAllListeners();
				wageAmountField.onEndEdit.AddListener(delegate
				{
					int amount = wageAmountField.text != string.Empty ? Mathf.Max(Int32.Parse(wageAmountField.text), 1) : 1;
					if(populationController.ChangeIncome(buildings[buildingId].jobs[localI].wage, amount, buildings[buildingId].jobs[localI].townWorkers))
					{
						buildings[buildingId].jobs[localI].wage = amount;

						if(!jobsByWage.ContainsKey(amount))
						{
							jobsByWage.Add(amount, new List<Tuple<int, int>>());
						}
						jobsByWage[amount].Add(new Tuple<int, int>(buildingId, localI));
					}

					panelManager.QueuePanelUpdate(this);
				});

				workerAmountField.gameObject.SetActive(true);
				wageAmountField.gameObject.SetActive(true);

				jobEntry.GetChild(3).gameObject.SetActive(false);
				jobEntry.GetChild(6).gameObject.SetActive(false);
			}
			else
			{
				Transform wantedWorkersText = jobEntry.GetChild(3);
				wantedWorkersText.GetComponent<TMP_Text>().text = jobs[i].wantedWorkers.ToString();

				Transform wageText = jobEntry.GetChild(7);
				wageText.GetComponent<TMP_Text>().text = jobs[i].wage.ToString();

				jobEntry.GetChild(2).gameObject.SetActive(false);
				jobEntry.GetChild(5).gameObject.SetActive(false);

				wantedWorkersText.gameObject.SetActive(true);
				wageText.gameObject.SetActive(true);
			}

			Button workButton = jobEntry.GetChild(8).GetComponent<Button>();
			if(jobs[i].playerWorkers.Count < jobs[i].wantedWorkers && !jobs[i].playerWorkers.Contains(localPlayer))
			{
				workButton.onClick.RemoveAllListeners();
				workButton.onClick.AddListener(delegate
				{
					localPlayer.WageLabour(buildings[localBuildingId], localI, this);
					buildings[localBuildingId].jobs[localI].playerWorkers.Add(localPlayer);

					if(buildings[localBuildingId].jobs[localI].townWorkers + buildings[localBuildingId].jobs[localI].playerWorkers.Count > buildings[localBuildingId].jobs[localI].wantedWorkers)
					{
						populationController.Fire(buildings[localBuildingId].jobs[localI].wage, 1);
						buildings[localBuildingId].jobs[localI].townWorkers -= 1;
					}

					panelManager.QueuePanelUpdate(this);
				});

				workButton.gameObject.SetActive(true);
			}
			else
			{
				workButton.gameObject.SetActive(false);
			}

			totalHeight += jobEntry.sizeDelta.y;
		}

		buildingEntry.sizeDelta = new Vector2(buildingEntry.sizeDelta.x, buildingEntry.sizeDelta.y + totalHeight);
	}

	public void UpdateBuildingCosts(BuildingData buildingData, BuildingStyle buildingStyle, Inventory playerInventory, Player player, TMP_InputField amountField, TMP_Text costText)
	{
		int buildingSize = amountField.text != string.Empty ? Mathf.Max(Int32.Parse(amountField.text), 1) : 1;
		Building potentialBuilding = new Building(buildingData, buildingStyle, buildingSize, playerInventory, player);
		ConstructionSite potentialConstructionSite = new ConstructionSite(potentialBuilding, ConstructionSite.Action.Construction);

		string buildingCostText = potentialConstructionSite.GetTimeLeft() + " days";
		foreach(Tuple<string, int> buildingMaterial in potentialConstructionSite.necessaryBuildingMaterials)
		{
			buildingCostText += ", " + buildingMaterial.Item2 + " " + buildingMaterial.Item1;
		}

		costText.text = buildingCostText;
	}

	public void UpdateRepairCost(Building building, TMP_Text costText)
	{
		ConstructionSite potentialConstructionSite = new ConstructionSite(building, ConstructionSite.Action.Repair);

		string repairCostText = potentialConstructionSite.GetTimeLeft() + " days";
		foreach(Tuple<string, int> buildingMaterial in potentialConstructionSite.necessaryBuildingMaterials)
		{
			repairCostText += ", " + buildingMaterial.Item2 + " " + buildingMaterial.Item1;
		}

		costText.text = repairCostText;
	}

	public void UpdateDestructionGain(Building building, TMP_InputField amountField, TMP_Text gainText)
	{
		int amount = amountField.text != string.Empty ? Mathf.Clamp(Int32.Parse(amountField.text), 1, building.size) : 1;
		ConstructionSite potentialConstructionSite = new ConstructionSite(building, ConstructionSite.Action.Deconstruction, amount);

		string destructionYield = potentialConstructionSite.GetTimeLeft() + " days";
		for(int i = 0; i < building.buildingStyle.materials.Length; i++)
		{
			destructionYield += ", " + potentialConstructionSite.GetDesconstructionYield(i) + " " + building.buildingStyle.materials[i];
		}

		gainText.text = destructionYield;
	}

	public void RefundBuildingCosts(string playerName, Inventory playerInventory, ConstructionSite constructionSite, float quality)
	{
		for(int i = 0; i < constructionSite.storedBuildingMaterials.Count; ++i)
		{
			if(!warehouseInventories.ContainsKey(playerName) || !warehouseInventories[playerName].DepositGood(new Good(
				goodManager.GetGoodData(constructionSite.storedBuildingMaterials[i].Item1),
				quality, quality, warehouseInventories[playerName]),
				constructionSite.storedBuildingMaterials[i].Item2))
			{
				playerInventory.DepositGood(new Good(
					goodManager.GetGoodData(constructionSite.storedBuildingMaterials[i].Item1),
					quality, quality, playerInventory),
					constructionSite.storedBuildingMaterials[i].Item2);
			}
		}
	}

	public void UpdateBuildings()
	{
		// TODO: Implement Seasons for Yield Differences (Growth Modifier is determined and retrieved from Time Controller)

		bool fired = false;
		List<Building> buildingsToDestroy = new List<Building>();
		for(int i = 0; i < buildings.Count; ++i)
		{
			// TODO: Connect Town Buildings to Town Warehouse
			if(buildings[i].connectedInventory == null)
			{
				continue;
			}

			// Fire or Pay Workers
			for(int j = 0; j < buildings[i].jobs.Length; ++j)
			{
				// Fire
				while(buildings[i].jobs[j].playerWorkers.Count > 0 && buildings[i].jobs[j].playerWorkers.Count > buildings[i].jobs[j].wantedWorkers)
				{
					int lastIndex = buildings[i].jobs[j].playerWorkers.Count - 1;
					buildings[i].jobs[j].playerWorkers[lastIndex].ResetAction(false, false, true);
					buildings[i].jobs[j].playerWorkers.RemoveAt(lastIndex);
				}
				int wantedTownWorkerCount = buildings[i].jobs[j].wantedWorkers - buildings[i].jobs[j].playerWorkers.Count;
				if(buildings[i].jobs[j].townWorkers > wantedTownWorkerCount)
				{
					populationController.Fire(buildings[i].jobs[j].wage, buildings[i].jobs[j].townWorkers - wantedTownWorkerCount);
					buildings[i].jobs[j].townWorkers = wantedTownWorkerCount;
				}
				if(buildings[i].jobs[j].wantedWorkers <= 0)
				{
					continue;
				}

				// Pay
				foreach(Player player in buildings[i].jobs[j].playerWorkers)
				{
					if(player != localPlayer && player.IsProductive() && !buildings[i].connectedInventory.TransferMoney(player.GetInventory(), buildings[i].jobs[j].wage))
					{
						player.ResetAction(false, false, true);
						infoController.AddMessage("Fired " + player.GetPlayerName() + "!", true, true);

						--buildings[i].jobs[j].wantedWorkers;
					}
				}
				if(!buildings[i].connectedInventory.ChangeMoney(-buildings[i].jobs[j].townWorkers * buildings[i].jobs[j].wage))
				{
					fired = true;

					populationController.Fire(buildings[i].jobs[j].wage, buildings[i].jobs[j].townWorkers);
					buildings[i].jobs[j].townWorkers = 0;
					buildings[i].jobs[j].wantedWorkers = 0;
				}
			}

			if(!buildings[i].underConstruction)
			{
				// Resource Consumption
				int minProducedItems = buildings[i].CalculateOutput();
				foreach(Tuple<string, int> resourceInput in buildings[i].currentResourceInputs)
				{
					int producedItems = (buildings[i].connectedInventory.GetInventoryAmount(resourceInput.Item1) / resourceInput.Item2) * minProducedItems;
					if(producedItems < minProducedItems)
					{
						minProducedItems = producedItems;
						infoController.AddMessage("Not enough " + resourceInput.Item1 + " in " + townName + "!", true, true);
					}
				}

				// Resource Quality Calculation
				float resourceQualitySum = 0.0f;
				int totalResourceAmount = 0;
				foreach(Tuple<string, int> resourceInput in buildings[i].currentResourceInputs)
				{
					List<Tuple<Good, int>> withdrawnGoods = buildings[i].connectedInventory.WithdrawGoodUnchecked(resourceInput.Item1, minProducedItems * resourceInput.Item2, true, false);
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
						minProducedItems);
				}

				// Building Degradation
				// Quality Loss: y = (1 / (2 * x)) with y: Quality Loss, x: current Quality in %
				if(buildings[i].quality > Mathf.Epsilon)
				{
					buildings[i].quality -= ((1.0f / (buildings[i].quality * 100.0f * 10.0f * buildingDegradationFactor)) / 100.0f);
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
		if(fired)
		{
			infoController.AddMessage("Unable to pay Workers in " + townName + "!", true, true);
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
					building.quality = constructionSites[building].newQuality;
					building.decayWarningIssued = false;
					TerminateConstructionSite(building, true);
					infoController.AddMessage("Repair of " + building.buildingData.buildingName + " complete", false, false);
				}
				// Deconstruction
				else if(constructionSites[building].action == ConstructionSite.Action.Deconstruction)
				{
					float materialQuality = building.quality * (1.0f / building.buildingStyle.baseQuality);
					for(int i = 0; i < building.buildingStyle.materials.Length; i++)
					{
						building.connectedInventory.DepositGood(new Good(
							goodManager.GetGoodData(building.buildingStyle.materials[i]),
							materialQuality, materialQuality, building.connectedInventory),
							constructionSites[building].GetDesconstructionYield(i));
					}

					building.size -= constructionSites[building].destructionCount;
					if(building.size <= 0)
					{
						DestroyBuilding(building);
					}

					infoController.AddMessage("Deconstruction of " + building.buildingData.buildingName + " complete", false, false);
				}

				panelManager.QueuePanelUpdate(this);
			}
		}
		buildings.Sort(CompareBuildings);

		// Hire Workers
		// Hire after Production/Construction Phase to ensure that Workers who got hired during the Day (e.g. at 23:59) don't contribute to Production/Construction
		LinkedList<Tuple<int, int, int, int>> openPositions = new LinkedList<Tuple<int, int, int, int>>(); // Building ID, Job ID, Number of open Positions, Wage
		int totalOpenPositions = 0;
		for(int i = 0; i < buildings.Count; ++i)
		{
			for(int j = 0; j < buildings[i].jobs.Length; ++j)
			{
				int totalWorkerCount = buildings[i].jobs[j].townWorkers + buildings[i].jobs[j].playerWorkers.Count;
				if(totalWorkerCount < buildings[i].jobs[j].wantedWorkers)
				{
					Tuple<int, int, int, int> newOpenPosition = new Tuple<int, int, int, int>(i, j, buildings[i].jobs[j].wantedWorkers - totalWorkerCount, buildings[i].jobs[j].wage);
					totalOpenPositions += newOpenPosition.Item3;
					LinkedListNode<Tuple<int, int, int, int>> currentPosition = openPositions.First;
					// Sorted by Wage descending
					while(currentPosition != null && (currentPosition.Value.Item4 >= newOpenPosition.Item4))
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
		}
		Tuple<Dictionary<Tuple<int, int>, int>, Dictionary<int, int>> hireFireLists = populationController.UpdateJobMarket(openPositions);
		foreach(KeyValuePair<int, int> firePosition in hireFireLists.Item2)
		{
			int peopleLeftToFire = firePosition.Value;
			List<Tuple<int, int>> positionIds = jobsByWage[firePosition.Key];
			for(int i = positionIds.Count - 1; i >= 0; --i)
			{
				if(buildings[positionIds[i].Item1].jobs[positionIds[i].Item2].wage != firePosition.Key || buildings[positionIds[i].Item1].jobs[positionIds[i].Item2].townWorkers <= 0)
				{
					jobsByWage[firePosition.Key].Remove(jobsByWage[firePosition.Key][i]);
					continue;
				}

				int fireCount = Mathf.Min(buildings[positionIds[i].Item1].jobs[positionIds[i].Item2].townWorkers, peopleLeftToFire);

				buildings[positionIds[i].Item1].jobs[positionIds[i].Item2].townWorkers -= fireCount;

				peopleLeftToFire -= fireCount;

				if(peopleLeftToFire <= 0)
				{
					break;
				}
			}

			if(peopleLeftToFire > 0)
			{
				Debug.LogWarning("Unable to fire more People, there are not enough People working for " + firePosition.Key + "G to fire " + firePosition.Value + " of them!");
			}
		}
		foreach(KeyValuePair<Tuple<int, int>, int> hirePosition in hireFireLists.Item1)
		{
			buildings[hirePosition.Key.Item1].jobs[hirePosition.Key.Item2].townWorkers += hirePosition.Value;

			int wage = buildings[hirePosition.Key.Item1].jobs[hirePosition.Key.Item2].wage;
			if(!jobsByWage.ContainsKey(wage))
			{
				jobsByWage.Add(wage, new List<Tuple<int, int>>());
			}
			jobsByWage[wage].Add(hirePosition.Key);
		}

		panelManager.QueuePanelUpdate(this);
	}

	public bool KillTownWorkers(int income, int count)
	{
		int peopleLeftToFire = count;
		List<Tuple<int, int>> positionIds = jobsByWage[income];
		for(int i = positionIds.Count - 1; i >= 0; --i)
		{
			Job job = buildings[positionIds[i].Item1].jobs[positionIds[i].Item2];
			int fireCount = Mathf.Min(job.townWorkers, peopleLeftToFire);
			buildings[positionIds[i].Item1].jobs[positionIds[i].Item2].townWorkers -= fireCount;

			peopleLeftToFire -= fireCount;

			if(peopleLeftToFire <= 0)
			{
				return true;
			}
		}

		Debug.LogWarning("Not enough Workers to fire!");
		return false;
	}

	public void StartConstructionSite(Building building, ConstructionSite constructionSite, bool fireWorkers, bool existingBuilding)
	{
		if(building.buildingData.buildingName == "Warehouse" && existingBuilding)
		{
			string ownerName = (building.owner != null) ? building.owner.GetPlayerName() : ("/" + townName);
			warehouseInventories[ownerName].ChangeBulkCapacity(Mathf.FloorToInt(-building.size * building.buildingStyle.baseQuality * warehouseBulkPerSize));
		}

		building.underConstruction = true;
		constructionSites.Add(building, constructionSite);

		if(fireWorkers)
		{
			for(int i = 1; i < building.jobs.Length; ++i)
			{
				populationController.Fire(building.jobs[i].wage, building.jobs[i].townWorkers);
				Player[] playerWorkers = building.jobs[i].playerWorkers.ToArray();
				foreach(Player player in playerWorkers)
				{
					player.ResetAction(false, false, true);
				}
				building.jobs[i] = new Job(building.jobs[i].jobName, 0, building.jobs[i].playerWorkers, 0, building.jobs[i].wage);
			}
		}

		panelManager.QueuePanelUpdate(this);
	}

	public void TerminateConstructionSite(Building building, bool completion)
	{
		if(building.buildingData.buildingName == "Warehouse" && building.underConstruction && completion)
		{
			string ownerName = (building.owner != null) ? building.owner.GetPlayerName() : ("/" + townName);
			warehouseInventories[ownerName].ChangeBulkCapacity(Mathf.FloorToInt(building.size * building.buildingStyle.baseQuality * warehouseBulkPerSize));
		}

		building.underConstruction = false;
		constructionSites.Remove(building);

		populationController.Fire(building.jobs[0].wage, building.jobs[0].townWorkers);
		Player[] playerWorkers = building.jobs[0].playerWorkers.ToArray();
		foreach(Player player in playerWorkers)
		{
			player.ResetAction(false, false, true);
		}
		building.jobs[0] = new Job("Construction Worker", 0, building.jobs[0].playerWorkers, 0, building.jobs[0].wage);
	}

	public void DestroyBuilding(Building building)
	{
		if(building.buildingData.buildingName == "Warehouse" && !building.underConstruction)
		{
			string ownerName = (building.owner != null) ? building.owner.GetPlayerName() : ("/" + townName);
			warehouseInventories[ownerName].ChangeBulkCapacity(Mathf.FloorToInt(-building.size * building.buildingStyle.baseQuality * warehouseBulkPerSize));
		}

		for(int j = 0; j < building.jobs.Length; ++j)
		{
			Player[] playerWorkers = building.jobs[j].playerWorkers.ToArray();
			foreach(Player player in playerWorkers)
			{
				player.ResetAction(false, false, true);
				infoController.AddMessage("Fired " + player.GetPlayerName() + "!", true, false);
			}

			populationController.Fire(building.jobs[j].wage, building.jobs[j].townWorkers);
		}

		if(building.underConstruction)
		{
			TerminateConstructionSite(building, false);
		}
		buildings.Remove(building);
	}

	public void AddPlayerWarehouseInventory(Player player)
	{
		string playerName = player.GetPlayerName();
		warehouseInventories[playerName] = GameObject.Instantiate<Inventory>(warehouseInventoryPrefab, transform);
		warehouseInventories[playerName].SetPlayer(player, true);
	}

	public bool IsWarehouseAdministered(Player player)
	{
		foreach(Building building in buildings)
		{
			if(building.owner == player && building.buildingData.buildingName == "Warehouse" && building.GetCurrentWorkerCount("Administrator") > 0)
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
