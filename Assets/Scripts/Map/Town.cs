using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Town : PanelObject
{
	[SerializeField] RectTransform needEntryPrefab = null;
	[SerializeField] Color badColor = new Color();
	[SerializeField] Color mehColor = new Color();
	[SerializeField] Color goodColor = new Color();
	[SerializeField] Color disabledColor = new Color();
	private new Transform transform = null;
	private Market market = null;
	private BuildingController buildingController = null;
	private ConstructionPanelController constructionPanelController = null;
	private PopulationController populationController = null;
	private string townName = null;
	private Vector2Int position = Vector2Int.zero;
	private PopulationController.NeedData[] needData = null;
	private float satisfactionBarSize = 0.0f;
	private Color defaultFontColor = new Color();

	private void Awake()
	{
		transform = gameObject.GetComponent<Transform>();
		market = gameObject.GetComponent<Market>();
		buildingController = gameObject.GetComponent<BuildingController>();
		constructionPanelController = gameObject.GetComponent<ConstructionPanelController>();
		populationController = gameObject.GetComponent<PopulationController>();

		townName = GenerateTownName();
	}

	protected override void Start()
	{
		base.Start();

		position = gameObject.GetComponentInParent<Tile>().GetPosition();

		TimeController.GetInstance().AddDailyUpdateListener(TownUpdate, TimeController.PriorityCategory.Town);
		needData = populationController.GetNeedData();
	}

	public void TownUpdate(double time)
	{
		populationController.UpdatePopulation();

		panelManager.QueuePanelUpdate(this);
		panelManager.QueuePanelUpdate(market); // Update Town Stats in Market Panel even if no Trades occur
	}

	public override void UpdatePanel(RectTransform panel)
	{
		base.UpdatePanel(panel);

		panel.GetChild(0).GetChild(0).GetComponent<TMP_Text>().text = townName + " (" + position.x + "|" + position.y + ")";

		// STAT PANEL
		RectTransform statPanel = (RectTransform)panel.GetChild(1);

		// Number Stats
		int totalPopulation = populationController.GetTotalPopulation();
		int growth = populationController.GetGrowth();

		statPanel.GetChild(1).GetComponent<TMP_Text>().text = totalPopulation.ToString();
		TMP_Text growthText = statPanel.GetChild(2).GetComponent<TMP_Text>();
		if(growth > 0)
		{
			growthText.text = "+" + populationController.GetGrowth();
			growthText.color = goodColor;
		}
		else if(growth < 0)
		{
			growthText.text = populationController.GetGrowth().ToString();
			growthText.color = badColor;
		}
		else
		{
			growthText.text = "±0";
			growthText.color = mehColor;
		}
		statPanel.GetChild(4).GetComponent<TMP_Text>().text = populationController.GetUnemployedPopulation().ToString();

		// Population Data Table
		RectTransform populationDataPanel = (RectTransform) statPanel.GetChild(5).GetChild(1);

		List<PopulationController.PopulationGroupUpdateResult> populationUpdateResults = populationController.GetPopulationUpdateResults();

		populationDataPanel.GetChild(0).GetComponent<TMP_Text>().text = populationUpdateResults[0].income + "G";
		populationDataPanel.GetChild(1).GetComponent<TMP_Text>().text = populationUpdateResults[populationUpdateResults.Count - 1].income + "G";
		int avgIncome = 0;
		foreach(var populationGroupUpdate in populationUpdateResults)
		{
			avgIncome += populationGroupUpdate.income * populationGroupUpdate.count;
		}
		populationDataPanel.GetChild(2).GetComponent<TMP_Text>().text = Mathf.Round((float) avgIncome / (float) totalPopulation) + "G";

		populationDataPanel.GetChild(3).GetComponent<TMP_Text>().text = populationUpdateResults[0].savings + "G";
		populationDataPanel.GetChild(4).GetComponent<TMP_Text>().text = populationUpdateResults[populationUpdateResults.Count - 1].savings + "G";
		int avgSavings = 0;
		foreach(var populationGroupUpdate in populationUpdateResults)
		{
			avgSavings += populationGroupUpdate.savings * populationGroupUpdate.count;
		}
		populationDataPanel.GetChild(5).GetComponent<TMP_Text>().text = Mathf.Round((float) avgSavings / (float) totalPopulation) + "G";

		populationDataPanel.GetChild(6).GetComponent<TMP_Text>().text = populationUpdateResults[0].age.ToString();
		populationDataPanel.GetChild(7).GetComponent<TMP_Text>().text = populationUpdateResults[populationUpdateResults.Count - 1].age.ToString();
		int avgAge = 0;
		foreach(var populationGroupUpdate in populationUpdateResults)
		{
			avgAge += populationGroupUpdate.age * populationGroupUpdate.count;
		}
		populationDataPanel.GetChild(8).GetComponent<TMP_Text>().text = Mathf.Round((float) avgAge / (float) totalPopulation).ToString();

		TMP_Text richSatisfactionText = populationDataPanel.GetChild(9).GetComponent<TMP_Text>();
		richSatisfactionText.text = Mathf.Round(populationUpdateResults[0].satisfaction * 100.0f) + "%";
		richSatisfactionText.color = populationUpdateResults[0].satisfaction >= 0.75f ? defaultFontColor : (populationUpdateResults[0].satisfaction >= 0.5f ? mehColor : badColor);
		TMP_Text poorSatisfactionText = populationDataPanel.GetChild(10).GetComponent<TMP_Text>();
		poorSatisfactionText.text = Mathf.Round(populationUpdateResults[populationUpdateResults.Count - 1].satisfaction * 100.0f) + "%";
		poorSatisfactionText.color = populationUpdateResults[populationUpdateResults.Count - 1].satisfaction >= 0.75f ? defaultFontColor : (populationUpdateResults[populationUpdateResults.Count - 1].satisfaction >= 0.5f ? mehColor : badColor);
		populationDataPanel.GetChild(10).GetComponent<TMP_Text>().text = Mathf.Round(populationUpdateResults[populationUpdateResults.Count - 1].satisfaction * 100.0f) + "%";
		float avgSatisfaction = populationController.GetSatisfaction();
		TMP_Text avgSatisfactionText = populationDataPanel.GetChild(11).GetComponent<TMP_Text>();
		avgSatisfactionText.text = Mathf.Round(avgSatisfaction * 100.0f) + "%";
		avgSatisfactionText.color = avgSatisfaction >= 0.75f ? defaultFontColor : (avgSatisfaction >= 0.5f ? mehColor : badColor);

		// Need Data Chart
		RectTransform needDataParent = (RectTransform) statPanel.GetChild(6).GetChild(1);
		float totalHeight = 0.0f;
		for(int i = 0; i < needData.Length; ++i)
		{
			RectTransform needEntry = null;
			if((i + 1) < needDataParent.childCount)
			{
				needEntry = (RectTransform)needDataParent.GetChild(i + 1);
			}
			else
			{
				needEntry = GameObject.Instantiate<RectTransform>(needEntryPrefab, needDataParent);
				needEntry.anchoredPosition = new Vector2(needEntry.anchoredPosition.x, -totalHeight);
				if(satisfactionBarSize <= 0.0f)
				{
					satisfactionBarSize = ((RectTransform) needEntry.GetChild(10)).sizeDelta.x;
					defaultFontColor = needEntry.GetChild(7).GetComponent<TMP_Text>().color;
				}

				needEntry.GetChild(0).GetComponent<TMP_Text>().text = needData[i].goodCategory + (needData[i].essential ? "*" : "");
			}

			needEntry.GetChild(1).GetComponent<TMP_Text>().text = populationUpdateResults[0].needBudgets[i] + "G";
			needEntry.GetChild(2).GetComponent<TMP_Text>().text = populationUpdateResults[populationUpdateResults.Count - 1].needBudgets[i] + "G";
			Color richSupplyColor = populationUpdateResults[0].saleAmounts[i] >= needData[i].maxBuyAmount ? defaultFontColor : badColor;
			Color poorSupplyColor = populationUpdateResults[populationUpdateResults.Count - 1].saleAmounts[i] >= needData[i].maxBuyAmount ? defaultFontColor : badColor;
			TMP_Text richSupplyText = needEntry.GetChild(3).GetComponent<TMP_Text>();
			richSupplyText.text = populationUpdateResults[0].saleAmounts[i].ToString();
			richSupplyText.color = richSupplyColor;
			TMP_Text poorSupplyText = needEntry.GetChild(4).GetComponent<TMP_Text>();
			poorSupplyText.text = populationUpdateResults[populationUpdateResults.Count - 1].saleAmounts[i].ToString();
			poorSupplyText.color = poorSupplyColor;
			TMP_Text richDemandText = needEntry.GetChild(5).GetComponent<TMP_Text>();
			richDemandText.text = "/" + needData[i].maxBuyAmount;
			richDemandText.color = richSupplyColor;
			TMP_Text poorDemandText = needEntry.GetChild(6).GetComponent<TMP_Text>();
			poorDemandText.text = "/" + needData[i].maxBuyAmount;
			poorDemandText.color = poorSupplyColor;
			TMP_Text richQualityText = needEntry.GetChild(7).GetComponent<TMP_Text>();
			richQualityText.text = Mathf.RoundToInt(populationUpdateResults[0].saleQuality[i] * 100.0f) + "%";
			richQualityText.color = populationUpdateResults[0].saleAmounts[i] >= needData[i].maxBuyAmount ? defaultFontColor : disabledColor;
			TMP_Text poorQualityText = needEntry.GetChild(8).GetComponent<TMP_Text>();
			poorQualityText.text = Mathf.RoundToInt(populationUpdateResults[populationUpdateResults.Count - 1].saleQuality[i] * 100.0f) + "%";
			poorQualityText.color = populationUpdateResults[populationUpdateResults.Count - 1].saleAmounts[i] >= needData[i].maxBuyAmount ? defaultFontColor : disabledColor;

			RectTransform richBar = (RectTransform) needEntry.GetChild(10);
			richBar.sizeDelta = new Vector2(satisfactionBarSize * populationUpdateResults[0].needSatisfactions[i], richBar.sizeDelta.y);
			if(populationUpdateResults[0].needSatisfactions[i] >= 0.75f)
			{
				richBar.GetComponent<Image>().color = goodColor;
			}
			else if(populationUpdateResults[0].needSatisfactions[i] >= 0.5f)
			{
				richBar.GetComponent<Image>().color = mehColor;
			}
			else
			{
				richBar.GetComponent<Image>().color = badColor;
			}

			RectTransform poorBar = (RectTransform) needEntry.GetChild(11);
			poorBar.sizeDelta = new Vector2(satisfactionBarSize * populationUpdateResults[populationUpdateResults.Count - 1].needSatisfactions[i], poorBar.sizeDelta.y);
			if(populationUpdateResults[populationUpdateResults.Count - 1].needSatisfactions[i] >= 0.75f)
			{
				poorBar.GetComponent<Image>().color = goodColor;
			}
			else if(populationUpdateResults[populationUpdateResults.Count - 1].needSatisfactions[i] >= 0.5f)
			{
				poorBar.GetComponent<Image>().color = mehColor;
			}
			else
			{
				poorBar.GetComponent<Image>().color = badColor;
			}
			
			needEntry.GetChild(12).GetComponent<TMP_Text>().text = Mathf.RoundToInt(populationUpdateResults[0].needSatisfactions[i] * 100.0f) + "%";
			needEntry.GetChild(13).GetComponent<TMP_Text>().text = Mathf.RoundToInt(populationUpdateResults[populationUpdateResults.Count - 1].needSatisfactions[i] * 100.0f) + "%";

			Image backgroundImage = needEntry.GetComponent<Image>();
			if(i % 2 != 0)
			{
				backgroundImage.enabled = false;
			}

			totalHeight += needEntry.sizeDelta.y;
		}
		needDataParent.sizeDelta = new Vector2(needDataParent.sizeDelta.x, totalHeight);

		// DETAIL PANEL
		RectTransform detailPanel = (RectTransform)panel.GetChild(2);

		Player player = transform.parent.gameObject.GetComponentInChildren<Player>();
		if(player != null && player.IsLocalPlayer())
		{
			Button marketButton = detailPanel.GetChild(1).GetComponent<Button>();
			marketButton.onClick.RemoveAllListeners();
			marketButton.onClick.AddListener(delegate
			{
				panelManager.OpenPanel(market);
			});

			Button storageButton = detailPanel.GetChild(2).GetComponent<Button>();
			storageButton.onClick.RemoveAllListeners();
			storageButton.onClick.AddListener(delegate
			{
				panelManager.OpenPanel(buildingController.GetWarehouseInventory(player.GetPlayerName()));
			});

			Button constructButton = detailPanel.GetChild(3).GetComponent<Button>();
			constructButton.onClick.RemoveAllListeners();
			constructButton.onClick.AddListener(delegate
			{
				panelManager.OpenPanel(constructionPanelController);
			});

			Button buildingButton = detailPanel.GetChild(4).GetComponent<Button>();
			buildingButton.onClick.RemoveAllListeners();
			buildingButton.onClick.AddListener(delegate
			{
				panelManager.OpenPanel(buildingController);
			});

			for(int i = 1; i < detailPanel.childCount; ++i)
			{
				detailPanel.GetChild(i).gameObject.SetActive(true);
			}

			detailPanel.GetChild(0).gameObject.SetActive(false);
		}
		else
		{
			for(int i = 1; i < detailPanel.childCount; ++i)
			{
				detailPanel.GetChild(i).gameObject.SetActive(false);
			}

			detailPanel.GetChild(0).gameObject.SetActive(true);
		}
	}

	private string GenerateTownName()
	{
		string[] firstParts = new string[] { "Birming", "Glas", "Bris", "Liver", "Man", "Shef", "Edin", "Lei", "Brad", "Coven", "Car", "Bel", "Notting", "Kings", "New", "South", "North", "Ports", "Ply", "Wolver", "Nor", "Bourne", "Aber", "Sunder" };
		string[] secondParts = new string[] { "town", "ville", "ham", "gow", "tol", "pool", "chester", "field", "burg", "burgh", "berg", "ford", "fort", "try", "diff", "fast", "ton", "castle", "hampton", "ding", "mouth", "burn", "port", "fax", "hill", "stadt", "sted" };

		return firstParts[UnityEngine.Random.Range(0, firstParts.Length)] + secondParts[UnityEngine.Random.Range(0, secondParts.Length)];
	}

	public string GetTownName()
	{
		return townName;
	}
}
