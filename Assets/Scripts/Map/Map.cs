using System;
using System.Collections.Generic;
using UnityEngine;

public class Map : MonoBehaviour
{
	private static Map instance = null;

	[SerializeField] private int mapWidth = 100;
	[SerializeField] private int mapHeight = 100;
	[SerializeField] private float tileSize = 10.0f;
	[SerializeField] private Transform[] terrainPrefabs = { };
	[SerializeField] private Transform[] terrainHeightThresholds = { };
	[SerializeField] private int seed = 0;
	[SerializeField] private float smoothness = 5.0f;
	[SerializeField] private float steepness = 2.0f;
	[SerializeField] private Transform[] forestPrefabs = { };
	[SerializeField] private float forestThreshold = 0.5f;
	[SerializeField] private float resourceSmoothness = 5.0f;
	[SerializeField] private Town[] townPrefabs = { };
	[SerializeField] private int maxTownCount = 20;
	[SerializeField] private bool generateResources = true;
	[SerializeField] private Transform terrainParent = null;
	[SerializeField] private Player playerPrefab = null;
	private Tile[,] tiles = null;
	private List<Town> towns = null;

	public static Map GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		instance = this;
	}

	private void Start()
	{
		Tuple<Tile[,], List<Town>> mapResult = MapGenerator.GenerateMap(seed, mapWidth, mapHeight, tileSize,
			terrainPrefabs, terrainHeightThresholds,
			smoothness, steepness,
			forestPrefabs, forestThreshold,
			resourceSmoothness,
			townPrefabs, maxTownCount,
			generateResources,
			terrainParent);
		tiles = mapResult.Item1;
		towns = mapResult.Item2;

		// Spawn Player
		GameObject.Instantiate<Player>(playerPrefab).SetPosition(towns[UnityEngine.Random.Range(0, towns.Count)].gameObject.GetComponentInParent<Tile>());
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
