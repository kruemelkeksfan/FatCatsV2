using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
	private static MapGenerator instance = null;

	[SerializeField] private int mapWidth = 100;
	[SerializeField] private int mapHeight = 100;
	[SerializeField] private float tileSize = 10.0f;
	[SerializeField] private Transform[] terrainPrefabs = { };
	[SerializeField] private Transform[] terrainHeightThresholds = { };
	[SerializeField] private int seed = 0;
	[SerializeField] private float smoothness = 500.0f;
	[SerializeField] private float steepness = 200.0f;
	[SerializeField] private Transform[] forestPrefabs = { };
	[SerializeField] private float forestThreshold = 0.5f;
	[SerializeField] private float resourceSmoothness = 500.0f;
	[SerializeField] private Town[] townPrefabs = { };
	[SerializeField] private int maxTownCount = 20;
	[SerializeField] private bool generateResources = true;
	[SerializeField] private Transform terrainParent = null;
	[SerializeField] private Player playerPrefab = null;
	private Tile[,] tiles = null;
	private List<Town> towns = null;

	public static MapGenerator GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		instance = this;
	}

	private void Start()
	{
		// Seed Generator
		Random.InitState(seed);

		tiles = new Tile[mapWidth, mapHeight];
		towns = new List<Town>();
		for(int z = 0; z < mapHeight; ++z)
		{
			for(int x = 0; x < mapWidth; ++x)
			{
				// Generate Terrain
				// Add 50000.0f, because negative Coordinates for Perlin Maps produce sad Results
				float height = Mathf.Clamp01(Mathf.PerlinNoise(((seed % 100000) + 50000.0f + x) / smoothness, (100000.0f + z) / smoothness)) * steepness;
				Transform tileTransform = GameObject.Instantiate<Transform>(terrainPrefabs[0], new Vector3(x * tileSize, height, z * tileSize), Quaternion.identity, terrainParent);

				// Generate Forests
				float forestyness = Mathf.Clamp01(Mathf.PerlinNoise((seed % 100000) + (50000.0f + x) / resourceSmoothness, (200000.0f + z) / resourceSmoothness));
				bool forest = false;
				if(forestPrefabs.Length > 0)
				{
					if(forestyness > forestThreshold)
					{
						Transform forestTransform = GameObject.Instantiate<Transform>(forestPrefabs[0], new Vector3(x * tileSize, height + tileSize * 0.5f, z * tileSize), Quaternion.identity, tileTransform);
						forest = true;
					}
				}

				// Generate Resources
				float[] resources = { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f };
				if(generateResources)
				{
					resources = new float[] {
					Mathf.Clamp01(forestyness),																										// Wood
					Mathf.Clamp01(forestyness),																										// Berries
					Mathf.Clamp01(forestyness),																										// Twigs
					Mathf.Clamp01((1.0f - forestyness)),																							// Herbs
					Mathf.Clamp01((1.0f - forestyness)),																							// Flax
					Mathf.Clamp01(1.0f),																											// Stone
					Mathf.Clamp01(Mathf.PerlinNoise((seed % 100000) + (50000.0f + x) / resourceSmoothness, (300000.0f + z) / resourceSmoothness)),	// Copper Ore
					Mathf.Clamp01(Mathf.PerlinNoise((seed % 100000) + (50000.0f + x) / resourceSmoothness, (400000.0f + z) / resourceSmoothness)),	// Iron Ore
					Mathf.Clamp01(Mathf.PerlinNoise((seed % 100000) + (50000.0f + x) / resourceSmoothness, (500000.0f + z) / resourceSmoothness)),	// Gold Ore
					Mathf.Clamp01(Mathf.PerlinNoise((seed % 100000) + (50000.0f + x) / resourceSmoothness, (600000.0f + z) / resourceSmoothness))	// Coal
					};
				}

				tiles[x, z] = tileTransform.GetComponent<Tile>();
				tiles[x, z].InitData(new Vector2Int(x, z), "Plains", height, forest, resources);
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

		// Spawn Player
		if(towns.Count > 0)
		{
			GameObject.Instantiate<Player>(playerPrefab).SetPosition(towns[Random.Range(0, towns.Count)].gameObject.GetComponentInParent<Tile>());
		}
		else
		{
			GameObject.Instantiate<Player>(playerPrefab).SetPosition(terrainParent.GetChild(Random.Range(0, terrainParent.childCount - 1)).GetComponent<Tile>());
		}
	}

	public float GetMinimumMovementCost()
	{
		return terrainPrefabs[0].GetComponent<Tile>().CalculateBestMovementCost();
	}

	public Tile GetTile(Vector2Int position)
	{
		if(position.x >= 0 && position.x < tiles.GetLength(0) && position.y >= 0 && position.y < tiles.GetLength(1))
		{
			return tiles[position.x, position.y];
		}

		return null;
	}

	public List<Town> GetTowns()
	{
		return towns;
	}
}
