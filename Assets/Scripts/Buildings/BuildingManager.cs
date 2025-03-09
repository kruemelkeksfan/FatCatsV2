using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct BuildingData
{
	public string buildingName;
	public string[] products;
	public int[] productOutputs;
	public int[] resourceProductIds;
	public string[] resources;
	public int[] resourceInputs;
	public float buildingTime;
	public string[] jobTitles;
	public int[] maxWorkerCounts;
}

[Serializable]
public struct BuildingStyle
{
	public string buildingStyleName;
	public float baseQuality;
	public string[] materials;
	public int[] materialAmountPerDay;
}

[Serializable]
public struct Job
{
	public string jobName;
	public int townWorkers;
	public List<Player> playerWorkers;
	public int wantedWorkers;
	public int wage;

	public Job(string name)
	{
		this.jobName = name;

		townWorkers = 0;
		playerWorkers = new List<Player>(1);
		wantedWorkers = 0;
		wage = 1;
	}

	public Job(string name, int townWorkers, List<Player> playerWorkers, int wantedWorkers, int wage)
	{
		this.jobName = name;
		this.townWorkers = townWorkers;
		this.playerWorkers = playerWorkers;
		this.wantedWorkers = wantedWorkers;
		this.wage = wage;
	}
}

[Serializable]
public class Building
{
	public BuildingData buildingData;
	public BuildingStyle buildingStyle;
	public float quality;
	public int size;
	public int currentProductId;
	public List<Tuple<string, int>> currentResourceInputs;
	public Job[] jobs;
	public Inventory connectedInventory;
	public Player owner;
	public bool underConstruction;
	public bool decayWarningIssued;

	public Building(BuildingData buildingData, BuildingStyle buildingStyle, int size, Inventory connectedInventory, Player owner)
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
			ChangeProduction();
		}
		jobs = new Job[buildingData.jobTitles.Length + 1];
		jobs[0] = new Job("Construction Worker");
		for(int i = 0; i < buildingData.jobTitles.Length; ++i)
		{
			jobs[i + 1] = new Job(buildingData.jobTitles[i]);
		}
		underConstruction = true; // Has to be true on Initialization to know whether a Building is freshly built or renovated in BuildingController
		decayWarningIssued = false;
	}

	public int GetCurrentWorkerCount(string jobName = null)
	{
		int totalWorkers = 0;
		foreach(Job job in jobs)
		{
			if(jobName == null || job.jobName == jobName)
			{
				int playerCount = 0;
				foreach(Player player in job.playerWorkers)
				{
					if(player.IsProductive())
					{
						++playerCount;
					}
				}
				totalWorkers += job.townWorkers + playerCount;
			}
		}

		return totalWorkers;
	}

	public int CalculateOutput()
	{
		if(currentProductId >= 0)
		{
			return buildingData.productOutputs[currentProductId] * GetCurrentWorkerCount();
		}

		return 0;
	}

	public void ChangeProduction(PanelManager panelManager = null, PanelObject panelObject = null)
	{
		currentProductId = (currentProductId + 1) % buildingData.products.Length;
		currentResourceInputs.Clear();
		for(int i = 0; i < buildingData.resourceProductIds.Length; ++i)
		{
			if(i == currentProductId)
			{
				currentResourceInputs.Add(new Tuple<string, int>(buildingData.resources[i], buildingData.resourceInputs[i]));
			}
		}

		if(panelObject != null)
		{
			panelManager?.QueuePanelUpdate(panelObject);
		}
	}
}

[Serializable]
public class ConstructionSite
{
	public enum Action { Construction, Repair, Deconstruction };

	public Building building;
	public Action action = Action.Construction;
	public List<Tuple<string, int>> storedBuildingMaterials;
	public List<Tuple<string, int>> necessaryBuildingMaterials;
	public bool enoughMaterial;
	public float passedBuildingTime;
	public float necessaryBuildingTime;
	public float newQuality;
	public int destructionCount;
	public float constructionParallelizationPotential;
	public float deconstructionYieldFactor;

	public ConstructionSite(Building building, Action action, int destructionCount = 0)
	{
		this.building = building;
		this.action = action;
		this.destructionCount = destructionCount;

		BuildingManager buildingManager = BuildingManager.GetInstance();
		constructionParallelizationPotential = buildingManager.GetConstructionParallelizationPotential();
		deconstructionYieldFactor = buildingManager.GetDeconstructionYieldFactor();

		storedBuildingMaterials = new List<Tuple<string, int>>();
		necessaryBuildingMaterials = new List<Tuple<string, int>>();
		if(action == Action.Construction)
		{
			for(int i = 0; i < building.buildingStyle.materials.Length; ++i)
			{
				storedBuildingMaterials.Add(new Tuple<string, int>(building.buildingStyle.materials[i], 0));
				necessaryBuildingMaterials.Add(new Tuple<string, int>(
					building.buildingStyle.materials[i], Mathf.CeilToInt(building.buildingStyle.materialAmountPerDay[i] * building.buildingData.buildingTime * building.size)));
			}

			necessaryBuildingTime = building.buildingData.buildingTime * building.buildingStyle.baseQuality * building.size;
		}
		else if(action == Action.Repair)
		{
			float costFactor = GetRepairCostFactor();

			for(int i = 0; i < building.buildingStyle.materials.Length; ++i)
			{
				storedBuildingMaterials.Add(new Tuple<string, int>(building.buildingStyle.materials[i], 0));
				necessaryBuildingMaterials.Add(new Tuple<string, int>(
					building.buildingStyle.materials[i], GetRepairCost(i, costFactor)));
			}

			necessaryBuildingTime = building.buildingData.buildingTime * building.buildingStyle.baseQuality * building.size * costFactor;
		}
		else if(action == Action.Deconstruction)
		{
			necessaryBuildingTime = building.buildingData.buildingTime * building.buildingStyle.baseQuality * destructionCount;
		}

		newQuality = building.quality;
		enoughMaterial = true;
		passedBuildingTime = 0.0f;
	}

	public int GetTimeLeft()
	{
		// Check Progress until tomorrow
		float passedBuildingTimeTomorrow = passedBuildingTime + GetSpeedup(false); // Daily Progress = 1 Day * Speedup = Speedup
		if(passedBuildingTimeTomorrow >= necessaryBuildingTime)
		{
			return 1;
		}

		// Return Time Estimate after all Workers are productive
		return 1 + Mathf.CeilToInt((necessaryBuildingTime - passedBuildingTimeTomorrow) * GetSpeedup(true));
	}

	public bool AdvanceConstruction()
	{
		float minBuildingMaterialProgress = 1.0f;
		for(int j = 0; j < necessaryBuildingMaterials.Count; ++j)
		{
			float buildingMaterialProgress = (float)storedBuildingMaterials[j].Item2 / (float)necessaryBuildingMaterials[j].Item2;
			if(buildingMaterialProgress < minBuildingMaterialProgress)
			{
				minBuildingMaterialProgress = buildingMaterialProgress;
			}
		}

		float speedup = GetSpeedup(false);
		if(speedup > 0)
		{
			// Progress = 1.0 Days * Speedup = Speedup
			float workProgress = (passedBuildingTime + speedup) / necessaryBuildingTime;

			enoughMaterial = true;
			if(minBuildingMaterialProgress < workProgress)
			{
				passedBuildingTime = minBuildingMaterialProgress * necessaryBuildingTime;
				enoughMaterial = false;
			}
			else
			{
				passedBuildingTime = workProgress * necessaryBuildingTime;
			}

			if(passedBuildingTime >= necessaryBuildingTime)
			{
				return true;
			}
		}

		return false;
	}

	private float GetSpeedup(bool countUnproductive)
	{
		int playerCount = countUnproductive ? building.jobs[0].playerWorkers.Count : 0;
		if(!countUnproductive)
		{
			foreach(Player player in building.jobs[0].playerWorkers)
			{
				if(player.IsProductive())
				{
					++playerCount;
				}
			}
		}
		int totalWorkerCount = building.jobs[0].townWorkers + playerCount;
		if(totalWorkerCount > 0)
		{
			// Source: https://de.wikipedia.org/wiki/Amdahlsches_Gesetz
			return 1.0f / ((1.0f - constructionParallelizationPotential) + (constructionParallelizationPotential / totalWorkerCount));
		}

		return 0.0f;
	}

	public float GetRepairCostFactor()
	{
		return (building.buildingStyle.baseQuality - building.quality) / building.buildingStyle.baseQuality;
	}

	public int GetConstructionCost(int materialIndex)
	{
		return Mathf.CeilToInt(building.buildingStyle.materialAmountPerDay[materialIndex] * building.buildingData.buildingTime * building.size);
	}

	public int GetRepairCost(int materialIndex, float costFactor)
	{
		return Mathf.CeilToInt(building.buildingStyle.materialAmountPerDay[materialIndex] * building.buildingData.buildingTime * building.size * costFactor);
	}

	public int GetDesconstructionYield(int materialIndex)
	{
		return Mathf.FloorToInt(building.buildingStyle.materialAmountPerDay[materialIndex] * building.buildingData.buildingTime * destructionCount * deconstructionYieldFactor);
	}
}

public class BuildingManager : MonoBehaviour
{
	private static BuildingManager instance = null;

	[SerializeField] private BuildingData[] buildingData = { };
	[SerializeField] private BuildingStyle[] buildingStyles = { };
	[SerializeField] private float constructionParallelizationPotential = 0.95f;
	[SerializeField] private float destructionYieldFactor = 0.8f;
	private Dictionary<string, BuildingData> buildingDataDictionary = null;

	public static BuildingManager GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		buildingDataDictionary = new Dictionary<string, BuildingData>(buildingData.Length);
		foreach(BuildingData building in buildingData)
		{
			buildingDataDictionary.Add(building.buildingName, building);
		}

		instance = this;
	}

	public BuildingData GetBuildingData(string buildingName)
	{
		return buildingDataDictionary[buildingName];
	}

	public BuildingData[] GetBuildingData()
	{
		return buildingData;
	}

	public BuildingStyle[] GetBuildingStyles()
	{
		return buildingStyles;
	}

	public float GetConstructionParallelizationPotential()
	{
		return constructionParallelizationPotential;
	}

	public float GetDeconstructionYieldFactor()
	{
		return destructionYieldFactor;
	}
}
