using System;
using System.Collections.Generic;

[Serializable]
public class Building
{
	public BuildingData buildingData;
	public BuildingStyle buildingStyle;
	public float quality;
	public int size;
	public int currentProductId;
	public List<Tuple<string, int>> currentResourceInputs;
	public int townWorkers;
	public List<Player> playerWorkers;
	public int wantedWorkers;
	public int wage;
	public int wageGroup;
	public Inventory connectedInventory;
	public Player owner;
	public bool underConstruction;
	public bool decayWarningIssued;

	public Building(BuildingData buildingData, BuildingStyle buildingStyle, int size, int wage, Inventory connectedInventory, Player owner)
	{
		this.buildingData = buildingData;
		this.buildingStyle = buildingStyle;
		this.size = size;
		this.connectedInventory = connectedInventory;
		this.owner = owner;

		quality = 0.0f;
		currentProductId = -1;
		currentResourceInputs = new List<Tuple<string, int>>();
		// If this Building can produce anything, we will increment currentProductId to 0 with ChangeProduction()
		if(buildingData.products.Length > 0)
		{
			ChangeProduction(0);
		}
		townWorkers = 0;
		playerWorkers = new List<Player>(1);
		wantedWorkers = 0;
		this.wage = wage;
		wageGroup = 0;
		underConstruction = true;   // Has to be true on Initialization to know whether a Building is freshly built or renovated in BuildingController
		decayWarningIssued = false;
	}

	public int GetCurrentWorkerCount()
	{
		int playerCount = 0;
		foreach(Player player in playerWorkers)
		{
			if(player.IsProductive())
			{
				++playerCount;
			}
		}

		return townWorkers + playerCount;
	}

	public int CalculateOutput()
	{
		if(currentProductId >= 0)
		{
			return buildingData.productOutputs[currentProductId] * GetCurrentWorkerCount();
		}

		return 0;
	}

	public void ChangeProduction(int productId)
	{
		if(productId < buildingData.products.Length)
		{
			currentProductId = productId;
			currentResourceInputs.Clear();
			for(int i = 0; i < buildingData.resourceProductIds.Length; ++i)
			{
				if(i == productId)
				{
					currentResourceInputs.Add(new Tuple<string, int>(buildingData.resources[i], buildingData.resourceInputs[i]));
				}
			}
		}
	}

	public float GetRepairCostFactor()
	{
		return (buildingStyle.baseQuality - quality) / buildingStyle.baseQuality;
	}
}
