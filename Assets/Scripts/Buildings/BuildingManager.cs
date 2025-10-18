using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[Serializable]
public struct BuildingData
{
	public string buildingName;
	public string description;
	public string[] products;
	public int[] productOutputs;
	public int[] resourceProductIds;
	public string[] resources;
	public int[] resourceInputs;
	public float buildingTime;
	public string jobTitle;
	public int maxWorkerCount;
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
		townWorkers = 0;
		playerWorkers = new List<Player>(1);
		wantedWorkers = 0;
		wage = 1;
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

	public float GetRepairCostFactor()
	{
		return (buildingStyle.baseQuality - quality) / buildingStyle.baseQuality;
	}
}

[Serializable]
public class ConstructionSite
{
	public enum Action { Construction, Repair, Deconstruction };

	public static List<Tuple<string, int>> GetConstructionMaterials(BuildingData buildingData, BuildingStyle buildingStyle, int size)
	{
		List<Tuple<string, int>> necessaryBuildingMaterials = new List<Tuple<string, int>>();
		for(int i = 0; i < buildingStyle.materials.Length; ++i)
		{
			necessaryBuildingMaterials.Add(new Tuple<string, int>(
				buildingStyle.materials[i], Mathf.CeilToInt(buildingStyle.materialAmountPerDay[i] * buildingData.buildingTime * size)));
		}

		return necessaryBuildingMaterials;
	}

	public static float GetConstructionTime(BuildingData buildingData, BuildingStyle buildingStyle, int size)
	{
		return buildingData.buildingTime * buildingStyle.baseQuality * size;
	}

	public static List<Tuple<string, int>> GetRepairMaterials(Building building)
	{
		float costFactor = building.GetRepairCostFactor();
		List<Tuple<string, int>> necessaryBuildingMaterials = new List<Tuple<string, int>>();
		for(int i = 0; i < building.buildingStyle.materials.Length; ++i)
		{
			necessaryBuildingMaterials.Add(new Tuple<string, int>(
				building.buildingStyle.materials[i], Mathf.CeilToInt(building.buildingStyle.materialAmountPerDay[i] * building.buildingData.buildingTime * building.size * costFactor)));
		}

		return necessaryBuildingMaterials;
	}

	public static float GetRepairTime(Building building)
	{
		return building.buildingData.buildingTime * building.buildingStyle.baseQuality * building.size * building.GetRepairCostFactor();
	}

	public static List<Tuple<string, int>> GetDeconstructionMaterials(Building building, int destructionCount)
	{
		List<Tuple<string, int>> deconstructionMaterials = new List<Tuple<string, int>>();
		for(int i = 0; i < building.buildingStyle.materials.Length; ++i)
		{
			deconstructionMaterials.Add(new Tuple<string, int>(
				building.buildingStyle.materials[i], Mathf.FloorToInt(building.buildingStyle.materialAmountPerDay[i] * building.buildingData.buildingTime * destructionCount * BuildingManager.GetInstance().GetDeconstructionYieldFactor())));
		}

		return deconstructionMaterials;
	}

	public static float GetDeconstructionTime(Building building, int destructionCount)
	{
		return building.buildingData.buildingTime * building.buildingStyle.baseQuality * destructionCount;
	}

	public Building building;
	public Action action = Action.Construction;
	public List<Tuple<string, int>> storedBuildingMaterials;
	public List<Tuple<string, int>> necessaryBuildingMaterials;
	public bool enoughMaterial;
	public float materialQuality;
	public float passedBuildingTime;
	public float necessaryBuildingTime;
	public float constructionParallelizationPotential;
	public int destructionCount;

	public ConstructionSite(Building building, Action action, int destructionCount = 0)
	{
		this.building = building;
		this.action = action;

		BuildingManager buildingManager = BuildingManager.GetInstance();
		constructionParallelizationPotential = buildingManager.GetConstructionParallelizationPotential();
		this.destructionCount = destructionCount;

		storedBuildingMaterials = new List<Tuple<string, int>>(building.buildingStyle.materials.Length);
		if(action == Action.Construction)
		{
			for(int i = 0; i < building.buildingStyle.materials.Length; ++i)
			{
				storedBuildingMaterials.Add(new Tuple<string, int>(building.buildingStyle.materials[i], 0));
			}
			necessaryBuildingMaterials = GetConstructionMaterials(building.buildingData, building.buildingStyle, building.size);
			necessaryBuildingTime = GetConstructionTime(building.buildingData, building.buildingStyle, building.size);
		}
		else if(action == Action.Repair)
		{
			for(int i = 0; i < building.buildingStyle.materials.Length; ++i)
			{
				storedBuildingMaterials.Add(new Tuple<string, int>(building.buildingStyle.materials[i], 0));
			}
			necessaryBuildingMaterials = GetRepairMaterials(building);
			necessaryBuildingTime = GetRepairTime(building);
		}
		else if(action == Action.Deconstruction)
		{
			necessaryBuildingTime = GetDeconstructionTime(building, destructionCount);
		}

		enoughMaterial = false;
		materialQuality = 0.0f;
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
		return 1 + Mathf.CeilToInt((necessaryBuildingTime - passedBuildingTimeTomorrow) / GetSpeedup(true));
	}

	public bool AdvanceConstruction()
	{
		float minBuildingMaterialProgress = 1.0f;
		if(action != ConstructionSite.Action.Deconstruction)
		{
			for(int j = 0; j < necessaryBuildingMaterials.Count; ++j)
			{
				float buildingMaterialProgress = (float)storedBuildingMaterials[j].Item2 / (float)necessaryBuildingMaterials[j].Item2;
				if(buildingMaterialProgress < minBuildingMaterialProgress)
				{
					minBuildingMaterialProgress = buildingMaterialProgress;
				}
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
		int playerCount = countUnproductive ? building.playerWorkers.Count : 0;
		if(!countUnproductive)
		{
			foreach(Player player in building.playerWorkers)
			{
				if(player.IsProductive())
				{
					++playerCount;
				}
			}
		}
		int totalWorkerCount = building.townWorkers + playerCount;
		if(totalWorkerCount > 0)
		{
			// Source: https://de.wikipedia.org/wiki/Amdahlsches_Gesetz
			return 1.0f / ((1.0f - constructionParallelizationPotential) + (constructionParallelizationPotential / totalWorkerCount));
		}

		return 0.0f;
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

			// Generate missing Building Descriptions
			if(building.description == string.Empty)
			{
				Debug.Log("Missing Description for " + building.buildingName + ":");

				if(building.products.Length > 0)
				{
					StringBuilder descriptionString = new StringBuilder();
					descriptionString.Append("Produces ");
					for(int i = 0; i < building.products.Length; ++i)
					{
						if(i > 0)
						{
							if(i >= building.products.Length - 1)
							{
								descriptionString.Append(" or ");
							}
							else
							{
								descriptionString.Append(", ");
							}
						}

						descriptionString.Append(building.products[i]);
					}
					if(building.resources.Length > 0)
					{
						descriptionString.Append(" from ");
						for(int i = 0; i < building.resources.Length; ++i)
						{
							if(i > 0)
							{
								if(i >= building.resources.Length - 1)
								{
									descriptionString.Append(" and ");
								}
								else
								{
									descriptionString.Append(", ");
								}
							}

							descriptionString.Append(building.resources[i]);
						}
					}

					Debug.Log(descriptionString.ToString());
				}
				else
				{
					Debug.Log("Unable to generate Description, because this is no Production Building!");
				}
			}
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
