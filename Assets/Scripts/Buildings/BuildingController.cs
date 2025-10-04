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
	private Dictionary<int, List<Building>> jobsByWage = null;
	private Dictionary<string, Inventory> warehouseInventories = null;
	private Building currentBuilding = null;
	private int currentDestructionCount = 1;
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
		}

		if(lho.underConstruction != rho.underConstruction)
		{
			return lho.underConstruction ? 1 : -1;
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
		jobsByWage = new Dictionary<int, List<Building>>();
		warehouseInventories = new Dictionary<string, Inventory>();
	}

	protected override void Start()
	{
		base.Start();

		goodManager = GoodManager.GetInstance();
		townName = gameObject.GetComponent<Town>().GetTownName();
		infoController = InfoController.GetInstance();
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
		});

		TMP_InputField wageGroupField = topInfoBar.GetChild(6).GetComponent<TMP_InputField>();
		wageGroupField.text = populationController.GetWage(playerName, currentWageGroupSetting).ToString();
		wageGroupField.onValueChanged.RemoveAllListeners();
		wageGroupField.onValueChanged.AddListener(delegate
		{
			int oldWage = populationController.GetWage(playerName, currentWageGroupSetting);
			int newWage = wageGroupField.text != string.Empty ? Mathf.Max(Int32.Parse(wageGroupField.text), 1) : 1;

			populationController.SetWage(playerName, currentWageGroupSetting, newWage, wageGroupCount);

			if(jobsByWage.ContainsKey(oldWage))
			{
				Building[] wageBuildings = jobsByWage[oldWage].ToArray();
				foreach(Building building in wageBuildings)
				{
					jobsByWage[oldWage].Remove(building);
					if(!jobsByWage.ContainsKey(newWage))
					{
						jobsByWage.Add(newWage, new List<Building>());
					}
					jobsByWage[newWage].Add(currentBuilding);

					populationController.ChangeIncome(oldWage, newWage, building.townWorkers);

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

				buildingInfo.GetChild(3).GetComponent<TMP_Text>().text = building.GetCurrentWorkerCount() + "/" + building.buildingData.maxWorkerCount;
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
					materialInfo.gameObject.SetActive(true);
				}
				else
				{
					materialInfo.gameObject.SetActive(false);
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
			buildingInfo.GetChild(8).GetComponent<TMP_Text>().text = "(" + MathUtil.GetTimespanString(CalculateLifespan(currentBuilding.quality)) + ")";

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
						if(jobsByWage.ContainsKey(currentBuilding.wage))
						{
							jobsByWage[currentBuilding.wage].Remove(currentBuilding);
						}

						currentBuilding.wage = newWage;
						currentBuilding.wageGroup = wageDropdown.value;

						if(!jobsByWage.ContainsKey(newWage))
						{
							jobsByWage.Add(newWage, new List<Building>());
						}
						jobsByWage[newWage].Add(currentBuilding);
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
						StartConstructionSite(currentBuilding, new ConstructionSite(currentBuilding, ConstructionSite.Action.Repair), true, true);
					});

					TMP_InputField destructionAmountField = buildingActions.GetChild(5).GetComponent<TMP_InputField>();
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
						StartConstructionSite(currentBuilding, new ConstructionSite(currentBuilding, ConstructionSite.Action.Deconstruction, currentDestructionCount), true, true);
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
						for(int k = 0; k < constructionSite.necessaryBuildingMaterials.Count; ++k)
						{
							if(k > 0)
							{
								costString.Append(", ");
							}
							costString.Append(constructionSite.necessaryBuildingMaterials[k].Item2 - constructionSite.storedBuildingMaterials[k].Item2);
							costString.Append(" ");
							costString.Append(constructionSite.necessaryBuildingMaterials[k].Item1);
						}
						if(costString.Length <= 0)
						{
							costString.Append("none");
						}
						constructionCostText.text = costString.ToString();

						// Add Materials
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
									constructionSite.materialQuality += (addedAmount * inventoryGood.Item1.quality * currentBuilding.buildingStyle.baseQuality) / constructionSite.necessaryBuildingMaterials[j].Item2;
									constructionSite.materialQuality = Mathf.Min(constructionSite.materialQuality, currentBuilding.buildingStyle.baseQuality - currentBuilding.quality); // Necessary, because necessary Repair Materials are rounded up
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
										constructionSite.materialQuality += (addedAmount * inventoryGood.Item1.quality * currentBuilding.buildingStyle.baseQuality) / constructionSite.necessaryBuildingMaterials[j].Item2;
										constructionSite.materialQuality = Mathf.Min(constructionSite.materialQuality, currentBuilding.buildingStyle.baseQuality - currentBuilding.quality); // Necessary, because necessary Repair Materials are rounded up
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

								constructionSite.storedBuildingMaterials[j] = new Tuple<string, int>(constructionSite.storedBuildingMaterials[j].Item1, storedAmount);
								if(constructionSite.action == ConstructionSite.Action.Construction)
								{
									currentBuilding.quality += constructionSite.materialQuality;
								}

								panelManager.QueuePanelUpdate(this);
							}
						});
						addMaterialButton.gameObject.SetActive(true);
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

							TerminateConstructionSite(currentBuilding, false);
							currentBuilding = null;
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
			// Fire
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
			if(buildings[i].wantedWorkers > 0)
			{
				// Pay
				foreach(Player player in buildings[i].playerWorkers)
				{
					if(player != localPlayer && player.IsProductive() && !buildings[i].connectedInventory.TransferMoney(player.GetInventory(), buildings[i].wage))
					{
						player.ResetAction(false, false, true);
						infoController.AddMessage("Fired " + player.GetPlayerName() + "!", true, true);

						--buildings[i].wantedWorkers;
					}
				}
				if(!buildings[i].connectedInventory.ChangeMoney(-buildings[i].townWorkers * buildings[i].wage))
				{
					fired = true;

					populationController.ChangeIncome(buildings[i].wage, 0, buildings[i].townWorkers);
					buildings[i].townWorkers = 0;
					buildings[i].wantedWorkers = buildings[i].playerWorkers.Count;
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
					building.quality += constructionSites[building].materialQuality;
					building.decayWarningIssued = false;
					TerminateConstructionSite(building, true);
					infoController.AddMessage("Repair of " + building.buildingData.buildingName + " complete", false, false);
				}
				// Deconstruction
				else if(constructionSites[building].action == ConstructionSite.Action.Deconstruction)
				{
					float materialQuality = building.quality * (1.0f / building.buildingStyle.baseQuality);
					List<Tuple<string, int>> deconstructionMaterials = ConstructionSite.GetDeconstructionMaterials(currentBuilding, constructionSites[building].destructionCount);
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

					infoController.AddMessage("Deconstruction of " + building.buildingData.buildingName + " complete", false, false);
				}
			}
		}
		buildings.Sort(CompareBuildings);

		// Hire Workers
		// Hire after Production/Construction Phase to ensure that Workers who got hired during the Day (e.g. at 23:59) don't contribute to Production/Construction
		LinkedList<Tuple<Building, int>> openPositions = new LinkedList<Tuple<Building, int>>(); // Building, Number of open Positions
		int totalOpenPositions = 0;
		for(int i = 0; i < buildings.Count; ++i)
		{
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
		Tuple<Dictionary<Building, int>, Dictionary<int, int>> hireFireLists = populationController.UpdateJobMarket(openPositions);
		foreach(KeyValuePair<int, int> firePosition in hireFireLists.Item2)
		{
			int peopleLeftToFire = firePosition.Value;
			List<Building> fireBuildings = jobsByWage[firePosition.Key];
			for(int i = fireBuildings.Count - 1; i >= 0; --i)   // Fire newest Employees first
			{
				if(fireBuildings[i].townWorkers <= 0)
				{
					jobsByWage[firePosition.Key].Remove(fireBuildings[i]);
					continue;
				}

				int fireCount = Mathf.Min(fireBuildings[i].townWorkers, peopleLeftToFire);

				fireBuildings[i].townWorkers -= fireCount;

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
		foreach(KeyValuePair<Building, int> hirePosition in hireFireLists.Item1)
		{
			hirePosition.Key.townWorkers += hirePosition.Value;

			int wage = hirePosition.Key.wage;
			if(!jobsByWage.ContainsKey(wage))
			{
				jobsByWage.Add(wage, new List<Building>());
			}
			jobsByWage[wage].Add(hirePosition.Key);
		}

		panelManager.QueuePanelUpdate(this);
	}

	public bool KillTownWorkers(int income, int count)
	{
		int peopleLeftToKill = count;
		List<Building> killBuildings = jobsByWage[income];
		for(int i = killBuildings.Count - 1; i >= 0; --i)
		{
			int fireCount = Mathf.Min(killBuildings[i].townWorkers, peopleLeftToKill);
			killBuildings[i].townWorkers -= fireCount;

			peopleLeftToKill -= fireCount;

			if(peopleLeftToKill <= 0)
			{
				return true;
			}
		}

		Debug.LogWarning("Not enough Workers to kill " + count + " People with an Income of " + income + "G");
		return false;
	}

	public void OrderBuilding(BuildingData buildingData, BuildingStyle buildingStyle, int constructionCount)
	{
		Inventory playerInventory = EnsurePlayerPresence();
		if(playerInventory == null)
		{
			Debug.LogWarning("Player is not present but can order Building in " + townName);
			return;
		}
		Player player = playerInventory.GetPlayer();

		Building building = new Building(buildingData, buildingStyle, constructionCount, warehouseInventories[player.GetPlayerName()], player);
		buildings.Add(building);
		buildings.Sort(CompareBuildings);

		StartConstructionSite(building, new ConstructionSite(building, ConstructionSite.Action.Construction), false, false);

		panelManager.OpenPanel(this);
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

	public void TerminateConstructionSite(Building building, bool completion)
	{
		if(building.buildingData.buildingName == "Warehouse" && building.underConstruction && completion)
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
		if(building.buildingData.buildingName == "Warehouse" && !building.underConstruction)
		{
			string ownerName = (building.owner != null) ? building.owner.GetPlayerName() : ("/" + townName);
			warehouseInventories[ownerName].ChangeBulkCapacity(Mathf.FloorToInt(-building.size * building.buildingStyle.baseQuality * warehouseBulkPerSize));
		}

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
		}

		if(building.underConstruction)
		{
			TerminateConstructionSite(building, false);
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
