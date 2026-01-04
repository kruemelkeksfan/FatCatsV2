using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ConstructionPanelController : PanelObject
{
	[SerializeField] private RectTransform newBuildingEntryPrefab = null;
	[SerializeField] private Color selectionColor = new Color();
	private BuildingController buildingController = null;
	private string townName = null;
	private BuildingData[] availableBuildingData = null;
	private BuildingStyle[] availableBuildingStyles = null;
	private BuildingData? currentBuildingData = null;
	private int currentBuildingStyle = 0;
	private int currentConstructionCount = 1;

	protected override void Start()
	{
		base.Start();

		buildingController = gameObject.GetComponent<BuildingController>();
		townName = gameObject.GetComponent<Town>().GetTownName();
		BuildingManager buildingManager = BuildingManager.GetInstance();
		availableBuildingData = buildingManager.GetBuildingData();
		availableBuildingStyles = buildingManager.GetBuildingStyles();
	}

	public override void UpdatePanel(RectTransform panel)
	{
		base.UpdatePanel(panel);

		if(!EnsurePlayerPresence())
		{
			return;
		}

		panel.GetChild(0).GetChild(0).GetComponent<TMP_Text>().text = "Construction - " + townName;

		// RectTransform topInfoBar = (RectTransform)panel.GetChild(1);
		// topInfoBar.GetChild(1).GetComponent<TMP_Text>().text = populationController.GetUnemployedPopulation() + "/" + populationController.GetTotalPopulation();
		// topInfoBar.GetChild(3).GetComponent<TMP_Text>().text = populationController.CalculateAverageIncome() + "G";

		// LIST
		RectTransform listParent = (RectTransform)panel.GetChild(2).GetChild(0).GetChild(0);
		int i = 0;
		float totalHeight = 0.0f;
		foreach(BuildingData buildingData in availableBuildingData)
		{
			BuildingData localBuildingData = buildingData;

			RectTransform buildingEntry = null;
			if(i < listParent.childCount)
			{
				buildingEntry = (RectTransform)listParent.GetChild(i);
			}
			else
			{
				buildingEntry = GameObject.Instantiate<RectTransform>(newBuildingEntryPrefab, listParent);
				buildingEntry.anchoredPosition = new Vector2(buildingEntry.anchoredPosition.x, -totalHeight);
			}

			buildingEntry.GetChild(1).GetComponent<TMP_Text>().text = buildingData.buildingName;
			buildingEntry.GetChild(2).GetComponent<TMP_Text>().text = buildingData.description;

			Button listButton = buildingEntry.GetComponent<Button>();
			listButton.onClick.RemoveAllListeners();
			listButton.onClick.AddListener(delegate
			{
				currentBuildingData = localBuildingData;
				panelManager.QueuePanelUpdate(this);
			});

			Image backgroundImage = buildingEntry.GetComponent<Image>();
			if(i % 2 != 0)
			{
				backgroundImage.enabled = false;
			}
			if(currentBuildingData.HasValue && buildingData.buildingName == currentBuildingData.Value.buildingName)
			{
				backgroundImage.color = selectionColor;
				backgroundImage.enabled = true;
			}
			else
			{
				backgroundImage.color = newBuildingEntryPrefab.GetComponent<Image>().color;
			}

			++i;
			totalHeight += buildingEntry.sizeDelta.y;
		}
		while(i < listParent.childCount)
		{
			GameObject.Destroy(listParent.GetChild(i).gameObject);
		}

		listParent.sizeDelta = new Vector2(listParent.sizeDelta.x, totalHeight);

		// INFO
		RectTransform infoParent = (RectTransform)panel.GetChild(3);
		if(currentBuildingData.HasValue)
		{
			infoParent.GetChild(0).GetComponent<TMP_Text>().text = currentBuildingData.Value.buildingName;

			RectTransform buildingInfo = (RectTransform)infoParent.GetChild(2);

			TMP_InputField buildingAmountField = buildingInfo.GetChild(1).GetComponent<TMP_InputField>();
			buildingAmountField.text = currentConstructionCount.ToString();
			buildingAmountField.onEndEdit.RemoveAllListeners();
			buildingAmountField.onEndEdit.AddListener(delegate
			{
				currentConstructionCount = buildingAmountField.text != string.Empty ? Mathf.Max(Int32.Parse(buildingAmountField.text), 1) : 1;
				panelManager.QueuePanelUpdate(this);
			});

			int maxWorkerCount = currentBuildingData.Value.maxWorkerCount * currentConstructionCount;
			buildingInfo.GetChild(3).GetComponent<TMP_Text>().text = maxWorkerCount.ToString();
			buildingInfo.GetChild(5).GetComponent<TMP_Text>().text = Mathf.RoundToInt(availableBuildingStyles[currentBuildingStyle].baseQuality * 100.0f) + "%";
			buildingInfo.GetChild(12).GetComponent<TMP_Text>().text = MathUtil.GetTimespanString(buildingController.CalculateLifespan(availableBuildingStyles[currentBuildingStyle].baseQuality));
			StringBuilder buildingCostText = new StringBuilder();
			List<Tuple<string, int>> necessaryConstructionMaterials = ConstructionSite.GetConstructionMaterials(currentBuildingData.Value, availableBuildingStyles[currentBuildingStyle], currentConstructionCount);
			int k = 0;
			foreach(Tuple<string, int> buildingMaterial in necessaryConstructionMaterials)
			{
				if(k > 0)
				{
					buildingCostText.Append(", ");
				}
				buildingCostText.Append(buildingMaterial.Item2 + " " + buildingMaterial.Item1);

				++k;
			}
			buildingInfo.GetChild(7).GetComponent<TMP_Text>().text = buildingCostText.ToString();
			buildingInfo.GetChild(9).GetComponent<TMP_Text>().text = MathUtil.GetTimespanString(ConstructionSite.GetConstructionTime(currentBuildingData.Value, availableBuildingStyles[currentBuildingStyle], currentConstructionCount));

			TMP_Dropdown buildingStyleDropdown = buildingInfo.GetChild(10).GetComponent<TMP_Dropdown>();
			if(buildingStyleDropdown.options.Count <= 0)
			{
				for(int j = 0; j < availableBuildingStyles.Length; ++j)
				{

					buildingStyleDropdown.options.Add(new TMP_Dropdown.OptionData(availableBuildingStyles[j].buildingStyleName));
					if(j == currentBuildingStyle)
					{
						buildingStyleDropdown.value = j;
					}
				}
				buildingStyleDropdown.RefreshShownValue();
				buildingStyleDropdown.onValueChanged.AddListener(delegate
				{
					currentBuildingStyle = buildingStyleDropdown.value;
					panelManager.QueuePanelUpdate(this);
				});
			}

			Button buildButton = buildingInfo.GetChild(11).GetComponent<Button>();
			buildButton.onClick.RemoveAllListeners();
			buildButton.onClick.AddListener(delegate
			{
				buildingController.OrderBuilding(currentBuildingData.Value, availableBuildingStyles[currentBuildingStyle], currentConstructionCount);
			});

			RectTransform recipeParent = (RectTransform)infoParent.GetChild(4);

			for(int j = 0; j < recipeParent.childCount; ++j)
			{
				RectTransform recipeEntry = (RectTransform)recipeParent.GetChild(j);

				if(j < currentBuildingData.Value.products.Length)
				{
					recipeEntry.GetChild(1).GetComponent<TMP_Text>().text = currentBuildingData.Value.products[j] + " (" + (currentBuildingData.Value.productOutputs[j] * maxWorkerCount) + "/day)";

					StringBuilder resourceString = new StringBuilder();
					bool first = true;
					for(int l = 0; l < currentBuildingData.Value.resources.Length; ++l)
					{
						if(currentBuildingData.Value.resourceProductIds[l] == j)
						{
							// Necessary, because not all Resources appear in all Recipes, so we can not just use k
							if(!first)
							{
								resourceString.Append(", ");
							}
							first = false;
							resourceString.Append(currentBuildingData.Value.resources[l]);
							resourceString.Append(" (");
							resourceString.Append((currentBuildingData.Value.resourceInputs[l] * maxWorkerCount));
							resourceString.Append("/day)");
						}
					}
					if(resourceString.Length <= 0)
					{
						resourceString.Append("none");
					}
					recipeEntry.GetChild(3).GetComponent<TMP_Text>().text = resourceString.ToString();

					recipeEntry.gameObject.SetActive(true);
				}
				else
				{
					recipeEntry.gameObject.SetActive(false);
				}
			}

			infoParent.gameObject.SetActive(true);
		}
		else
		{
			infoParent.gameObject.SetActive(false);
		}
	}
}
