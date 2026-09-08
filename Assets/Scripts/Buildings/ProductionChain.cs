using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ProductionChain
{
	public string product;
	public BuildingData finalBuilding;
	public int finalProductId;
	public Dictionary<BuildingData, int> buildingCounts;
	public int totalBuildingCount;

	public static ProductionChain CalculateProductionChain(Dictionary<string, ProductionChain> productionChains, BuildingData[] buildingData, string product)
	{
		// Terminate early, if Production Chain already exists
		if(productionChains.ContainsKey(product))
		{
			return productionChains[product];
		}

		// Search for Production Building
		foreach(BuildingData building in buildingData)
		{
			// Search for Product ID in Building
			string[] buildingProducts = building.products;
            for(int i = 0; i < buildingProducts.Length; i++)
			{
                string buildingProduct = buildingProducts[i];
                if(buildingProduct == product)
				{
					// Instantiate new Production Chain
					ProductionChain productionChain = new ProductionChain(building, i);

					// Calculate Resource Production Chains
					for(int j = 0; j < building.resources.Length; ++j)
					{
						if(building.resourceProductIds[j] == i)
						{
							ProductionChain resourceChain = CalculateProductionChain(productionChains, buildingData, building.resources[j]);

							// Add Resource Production Chains to current Production Chain
							int buildingCountFactor = Mathf.CeilToInt((float) (building.resourceInputs[j] * building.maxWorkerCount)
								/ (float) (resourceChain.finalBuilding.productOutputs[resourceChain.finalProductId] * resourceChain.finalBuilding.maxWorkerCount));
							foreach(KeyValuePair<BuildingData, int> buildingCount in resourceChain.buildingCounts)
							{
								productionChain.AddBuilding(buildingCount.Key, buildingCount.Value * buildingCountFactor);
							}
						}
					}

					// Add Production Chain to Dictionary
					productionChains.Add(product, productionChain);

					return productionChain;
				}
			}
		}

		Debug.LogWarning("Could not find Production Building for " + product + " Production!");
		return null;
	}

	public ProductionChain(BuildingData buildingData, int productId)
	{
		product = buildingData.products[productId];
		finalBuilding = buildingData;
		finalProductId = productId;
		buildingCounts = new Dictionary<BuildingData, int>();
		totalBuildingCount = 1;
		buildingCounts.Add(buildingData, 1);
	}

	public void AddBuilding(BuildingData buildingData, int count)
	{
		buildingCounts.Add(buildingData, count);
		totalBuildingCount += count;
	}
}
