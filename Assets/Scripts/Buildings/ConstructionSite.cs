using System;
using System.Collections.Generic;
using UnityEngine;

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
