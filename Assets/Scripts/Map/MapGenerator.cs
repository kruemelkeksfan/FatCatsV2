using System.Collections.Generic;
using UnityEngine;

public static class MapGenerator
{
	public static Map GenerateMap(int seed, int mapWidth, int mapHeight, float tileSize, float sightlineEyeHeight,
		Transform[] terrainPrefabs, Transform[] terrainHeightThresholds,
		float smoothness, float steepness,
		Transform[] forestPrefabs, float forestThreshold,
		float resourceSmoothness,
		Town[] townPrefabs, int maxTownCount,
		bool generateResources,
		Transform terrainParent,
		Transform exitMarkerPrefab,
		TilePool tilePool,
		float offsetX = 0, float offsetZ = 0)
	{
		// Seed Generator
		Random.InitState(seed);

		Tile[,] tiles = new Tile[mapWidth, mapHeight];
		List<Town> towns = new List<Town>();
		for(int z = 0; z < mapHeight; ++z)
		{
			for(int x = 0; x < mapWidth; ++x)
			{
				// Generate Terrain
				// Add 50000.0f, because negative Coordinates for Perlin Maps produce sad Results
				Vector3 position = new Vector3(x * tileSize - (z % 2 == 0 ? tileSize * 0.5f : 0.0f), 0.0f, z * tileSize);
				float height = Mathf.Clamp01(Mathf.PerlinNoise(((seed % 100000) + 50000.0f + position.x) / smoothness, (100000.0f + position.z) / smoothness)) * steepness;
				position += Vector3.up * height;
				Transform tileTransform = tilePool.GetTile(terrainParent, new Vector3(offsetX, 0.0f, offsetZ) + position);

				// Generate Forests
				float forestyness = Mathf.Clamp01(Mathf.PerlinNoise((seed % 100000) + (50000.0f + position.x) / resourceSmoothness, (200000.0f + position.z) / resourceSmoothness));
				bool forest = false;
				if(forestPrefabs.Length > 0)
				{
					if(forestyness > forestThreshold)
					{
						Transform forestTransform = GameObject.Instantiate<Transform>(forestPrefabs[0], new Vector3(offsetX + position.x, height + tileSize * 0.5f, offsetZ + position.z), Quaternion.identity, tileTransform);
						forest = true;
					}
				}

				// Generate Resources
				float[] resources = { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f };
				if(generateResources)
				{
					resources = new float[] {
					Mathf.Clamp01(forestyness),																															// Wood
					Mathf.Clamp01(forestyness),																															// Berries
					Mathf.Clamp01(forestyness),																															// Twigs
					Mathf.Clamp01(1.0f - forestyness),																													// Herbs
					Mathf.Clamp01(1.0f - forestyness),																													// Flax
					Mathf.Clamp01(1.0f),																																// Stone
					Mathf.Clamp01(Mathf.PerlinNoise((seed % 100000) + (50000.0f + position.x) / resourceSmoothness, (300000.0f + position.z) / resourceSmoothness)),	// Copper Ore
					Mathf.Clamp01(Mathf.PerlinNoise((seed % 100000) + (50000.0f + position.x) / resourceSmoothness, (400000.0f + position.z) / resourceSmoothness)),	// Iron Ore
					Mathf.Clamp01(Mathf.PerlinNoise((seed % 100000) + (50000.0f + position.x) / resourceSmoothness, (500000.0f + position.z) / resourceSmoothness)),	// Gold Ore
					Mathf.Clamp01(Mathf.PerlinNoise((seed % 100000) + (50000.0f + position.x) / resourceSmoothness, (600000.0f + position.z) / resourceSmoothness))		// Coal
					};
				}

				tiles[x, z] = tileTransform.GetComponent<Tile>();
				tiles[x, z].InitData(new Vector2Int(x, z), "Plains", height, forest, resources);

				if(exitMarkerPrefab != null)
				{
					if(z == 0)
					{
						GameObject.Instantiate<Transform>(exitMarkerPrefab, new Vector3(offsetX + position.x, height + tileSize * 0.5f, offsetZ + position.z), Quaternion.Euler(0.0f, 180.0f, 0.0f), tileTransform);
					}
					else if(z == mapHeight - 1)
					{
						GameObject.Instantiate<Transform>(exitMarkerPrefab, new Vector3(offsetX + position.x, height + tileSize * 0.5f, offsetZ + position.z), Quaternion.Euler(0.0f, 0.0f, 0.0f), tileTransform);
					}
					else if(x == 0)
					{
						GameObject.Instantiate<Transform>(exitMarkerPrefab, new Vector3(offsetX + position.x, height + tileSize * 0.5f, offsetZ + position.z), Quaternion.Euler(0.0f, -90.0f, 0.0f), tileTransform);
					}
					else if(x == mapWidth - 1)
					{
						GameObject.Instantiate<Transform>(exitMarkerPrefab, new Vector3(offsetX + position.x, height + tileSize * 0.5f, offsetZ + position.z), Quaternion.Euler(0.0f, 90.0f, 0.0f), tileTransform);
					}
				}
			}
		}

		// Generate Towns
		for(int i = 0; i < maxTownCount; ++i)
		{
			Transform tileTransform = terrainParent.GetChild(Random.Range(0, terrainParent.childCount - 1));
			if(tileTransform.GetComponentInChildren<Town>() == null)
			{
				Town town = GameObject.Instantiate<Town>(townPrefabs[0], new Vector3(tileTransform.position.x, tileTransform.position.y + tileSize * 0.5f, tileTransform.position.z), Quaternion.identity, tileTransform);

				Tile tile = tileTransform.GetComponent<Tile>();
				if(tile.IsForest())
				{
					tile.SetForest(false);
					GameObject.Destroy(tileTransform.GetChild(0).gameObject);
				}
				tile.SetTown(town);

				towns.Add(town);
			}
		}

		return new Map(tiles, towns, tileSize, sightlineEyeHeight);
	}
}
