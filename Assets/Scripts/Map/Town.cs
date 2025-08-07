using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Town : PanelObject, IListener
{
	private new Transform transform = null;
	private Market market = null;
	private BuildingController buildingController = null;
	private PopulationController populationController = null;
	private string townName = null;
	private Vector2Int position = Vector2Int.zero;

	private void Awake()
	{
		transform = gameObject.GetComponent<Transform>();
		market = gameObject.GetComponent<Market>();
		buildingController = gameObject.GetComponent<BuildingController>();
		populationController = gameObject.GetComponent<PopulationController>();
	}

	protected override void Start()
	{
		base.Start();

		townName = GenerateTownName();
		position = gameObject.GetComponentInParent<Tile>().GetPosition();

		TimeController.GetInstance().AddDailyUpdateListener(this, TimeController.Order.Town);
	}

	public override void UpdatePanel(RectTransform panel, bool add = true)
	{
		base.UpdatePanel(panel);

		panel.GetChild(0).GetChild(0).GetComponent<TMP_Text>().text = townName + " (" + position.x + "|" + position.y + ")";

		RectTransform statPanel = (RectTransform)panel.GetChild(1);

		PopulationController.PopulationGroup[] populationGroups = populationController.GetPopulationGroups();
		int totalPopulation = populationController.GetTotalPopulation();

		statPanel.GetChild(2).GetComponent<TMP_Text>().text = totalPopulation.ToString();

		int[] townData = new int[]
		{
			int.MaxValue, 0, int.MinValue,
			int.MaxValue, 0, int.MinValue,
			int.MaxValue, 0, int.MinValue
		};
		foreach(PopulationController.PopulationGroup populationGroup in populationGroups)
		{
			if(populationGroup.count <= 0)
			{
				continue;
			}

			// Satisfaction
			int satisfaction = Mathf.FloorToInt(populationGroup.satisfaction * 100);
			if(satisfaction < townData[0])
			{
				townData[0] = satisfaction;
			}
			townData[1] += satisfaction * populationGroup.count;
			if(satisfaction > townData[2])
			{
				townData[2] = satisfaction;
			}

			// Income
			if(populationGroup.income < townData[3])
			{
				townData[3] = populationGroup.income;
			}
			townData[4] += populationGroup.income * populationGroup.count;
			if(populationGroup.income > townData[5])
			{
				townData[5] = populationGroup.income;
			}

			// Savings
			int savingsPerPerson = Mathf.RoundToInt((float)populationGroup.savings / (float)populationGroup.count);
			if(savingsPerPerson < townData[6])
			{
				townData[6] = savingsPerPerson;
			}
			townData[7] += populationGroup.savings;
			if(savingsPerPerson > townData[8])
			{
				townData[8] = savingsPerPerson;
			}
		}
		townData[1] = Mathf.RoundToInt((float)townData[1] / (float)totalPopulation);
		townData[4] = Mathf.RoundToInt((float)townData[4] / (float)totalPopulation);
		townData[7] = Mathf.RoundToInt((float)townData[7] / (float)totalPopulation);

		for(int i = 0; i < 9; ++i)
		{
			statPanel.GetChild(9 + i).GetComponent<TMP_Text>().text = townData[i].ToString() + (i < 3 ? "%" : "") + (i >= 3 ? "G" : "");
		}

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
			marketButton.gameObject.SetActive(true);

			Button storageButton = detailPanel.GetChild(2).GetComponent<Button>();
			storageButton.onClick.RemoveAllListeners();
			storageButton.onClick.AddListener(delegate
			{
				panelManager.OpenPanel(buildingController.GetWarehouseInventory(player.GetPlayerName()));
			});
			storageButton.gameObject.SetActive(true);

			Button buildingButton = detailPanel.GetChild(3).GetComponent<Button>();
			buildingButton.onClick.RemoveAllListeners();
			buildingButton.onClick.AddListener(delegate
			{
				panelManager.OpenPanel(buildingController);
			});
			buildingButton.gameObject.SetActive(true);

			detailPanel.GetChild(4).gameObject.SetActive(false);
		}
		else
		{
			detailPanel.GetChild(1).gameObject.SetActive(false);
			detailPanel.GetChild(2).gameObject.SetActive(false);
			detailPanel.GetChild(3).gameObject.SetActive(false);

			detailPanel.GetChild(4).gameObject.SetActive(true);
		}
	}

	public void Notify()
	{
		buildingController.UpdateBuildings();
		populationController.UpdatePopulation();

		panelManager.QueuePanelUpdate(this);
		panelManager.QueuePanelUpdate(market); // Update Town Stats in Market Panel even if no Trades occur
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
