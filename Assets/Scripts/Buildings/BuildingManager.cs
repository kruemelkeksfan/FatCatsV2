using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[Serializable]
public struct BuildingData
{
	public int id;
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

public class BuildingManager : MonoBehaviour
{
	private static BuildingManager instance = null;

	[SerializeField] private BuildingData[] buildingData = { };
	[SerializeField] private BuildingStyle[] buildingStyles = { };
	[SerializeField] private float constructionParallelizationPotential = 0.95f;
	[SerializeField] private float destructionYieldFactor = 0.8f;
	private ProductionChainPanelController productionChainPanelController = null;
	private Dictionary<string, BuildingData> buildingDataDictionary = null;
	private Dictionary<string, ProductionChain> productionChains = null;

	public static BuildingManager GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		buildingDataDictionary = new Dictionary<string, BuildingData>(buildingData.Length);
		productionChains = new Dictionary<string, ProductionChain>();
		foreach(BuildingData building in buildingData)
		{
			buildingDataDictionary.Add(building.buildingName, building);

			foreach(string product in building.products)
			{
				ProductionChain.CalculateProductionChain(productionChains, buildingData, product);
			}

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

		// Debug Print Production Chains
		/*foreach(ProductionChain productionChain in productionChains.Values)
		{
			Debug.LogWarning(productionChain.finalBuilding.products[productionChain.finalProductId] + " (" + productionChain.totalBuildingCount + "): ");
			foreach(KeyValuePair<BuildingData, int> buildingCount in productionChain.buildingCounts)
			{
				Debug.LogWarning(buildingCount.Value + "x" + buildingCount.Key.buildingName);
			}
		}*/
	}

    private void Start()
    {
        productionChainPanelController = gameObject.GetComponent<ProductionChainPanelController>();
		productionChainPanelController.SetProductionChains(productionChains);
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

	public ProductionChainPanelController GetProductionChainPanelController()
	{
		return productionChainPanelController;
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
