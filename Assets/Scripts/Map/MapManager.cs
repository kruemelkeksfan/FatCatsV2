using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Map
{
	public GameObject mapModel;
	public Tile[,] tiles;
	public List<Town> towns;
	public float tileSize;
	public float eyeHeight;

	public Map(GameObject mapModel, Tile[,] tiles, List<Town> towns, float tileSize, float eyeHeight)
	{
		this.mapModel = mapModel;
		this.tiles = tiles;
		this.towns = towns;
		this.tileSize = tileSize;
		this.eyeHeight = eyeHeight;
	}

	public void UpdateFogOfWar(Tile playerTile)
	{
		for(int y = 0; y < tiles.GetLength(1); ++y)
		{
			for(int x = 0; x < tiles.GetLength(0); ++x)
			{
				Vector3 sightLine = tiles[x, y].GetTransform().position - playerTile.GetTransform().position;
				float sightLineMagnitude = sightLine.magnitude;
				if(sightLineMagnitude < tileSize * 1.5f																					// Always show Neighbour Tiles
					|| (sightLineMagnitude < (tileSize * tiles[x, y].GetMaxVisionRange() + 1.0f)										// Never show far away Tiles
					&& !Physics.Raycast(playerTile.GetTransform().position + Vector3.up * eyeHeight, sightLine, sightLineMagnitude)))	// Show Tiles if Sightline is unobstructed
				{
					tiles[x, y].SetFogOfWar(Tile.FogOfWar.Visible);
				}
				else
				{
					if(tiles[x, y].GetFogOfWar() == Tile.FogOfWar.Visible)
					{
						tiles[x, y].SetFogOfWar(Tile.FogOfWar.Partial);
					}
				}
			}
		}
	}

	public void UpdateResourceFilter(string resourceFilter)
	{
		for(int y = 0; y < tiles.GetLength(1); ++y)
		{
			for(int x = 0; x < tiles.GetLength(0); ++x)
			{
				tiles[x, y].UpdateResourceDisplay(resourceFilter);
			}
		}
	}

	public Tile GetTile(Vector2Int position)
	{
		if(position.x >= 0 && position.x < tiles.GetLength(0) && position.y >= 0 && position.y < tiles.GetLength(1))
		{
			return tiles[position.x, position.y];
		}

		return null;
	}

	public Vector2Int GetMapSize()
	{
		return new Vector2Int(tiles.GetLength(0), tiles.GetLength(1));
	}

	public int GetTotalSavings()
	{
		int totalSavings = Player.GetInstance().GetInventory().GetMoney();
		foreach(Town town in towns)
		{
			totalSavings += town.GetBuildingController().GetTotalSavings();
		}

		return totalSavings;
	}
}

[Serializable]
public class TilePool
{
	public int minTileReserve;
	public int maxTileReserve;
	public int tilesPerUpdate;
	public Stack<Transform> tiles;
	public Transform tilePrefab;
	public Transform poolParent;

	public TilePool(int minTileReserve, int maxTileReserve, int tilesPerUpdate, Transform tilePrefab)
	{
		this.minTileReserve = minTileReserve;
		this.maxTileReserve = maxTileReserve;
		this.tilesPerUpdate = tilesPerUpdate;
		this.tilePrefab = tilePrefab;

		tiles = new Stack<Transform>(maxTileReserve);
		poolParent = (new GameObject("TilePoolParent")).GetComponent<Transform>();
	}

	public void Update()
	{
		for(int i = 0; i < tilesPerUpdate; ++i)
		{
			if(tiles.Count < minTileReserve)
			{
				Transform tile = GameObject.Instantiate<Transform>(tilePrefab, Vector3.zero, Quaternion.identity, poolParent);
				tile.gameObject.SetActive(false);
				tiles.Push(tile);

				// Debug.Log(tiles.Count);
			}
			else
			{
				break;
			}
		}
	}

	public Transform GetTile(Transform parent, Vector3 position)
	{
		if(tiles.Count < 1)
		{
			Update();
		}

		Transform tile = tiles.Pop();
		tile.SetParent(parent, false);
		tile.position = position;
		tile.gameObject.SetActive(true);

		return tile;
	}

	public void ReturnTile(Transform tileTransform)
	{
		if(tiles.Count > maxTileReserve)
		{
			GameObject.Destroy(tileTransform.gameObject);
			return;
		}

		// Destroy Towns and Markers, but leave Fog of War and Resources
		for(int i = 3; i < tileTransform.childCount; ++i)
		{
			GameObject.Destroy(tileTransform.GetChild(i).gameObject);
		}

		Tile tile = tileTransform.GetComponent<Tile>();
		tile.SetTown(null);
		tile.SetFogOfWar(Tile.FogOfWar.Invisible);

		tileTransform.SetParent(poolParent, false);
		tileTransform.gameObject.SetActive(false);
		tiles.Push(tileTransform);
	}
}

public class MapManager : MonoBehaviour
{
	private static MapManager instance = null;

	[SerializeField] private int mapWidth = 100;
	[SerializeField] private int mapHeight = 100;
	[SerializeField] private float tileSize = 10.0f;
	[SerializeField] private Transform[] terrainPrefabs = { };
	[SerializeField] private Transform[] terrainHeightThresholds = { };
	[SerializeField] private Material[] terrainMaterials = { };
	[SerializeField] private int seed = 0;
	[SerializeField] private float smoothness = 5.0f;
	[SerializeField] private float steepness = 2.0f;
	[SerializeField] private float forestThreshold = 0.5f;
	[SerializeField] private float resourceSmoothness = 5.0f;
	[SerializeField] private float oreDepositChance = 0.05f;
	[SerializeField] private Town[] townPrefabs = { };
	[SerializeField] private int maxTownCount = 20;
	[SerializeField] private Transform terrainParent = null;
	[SerializeField] private int minTileReserve = 100;
	[SerializeField] private int maxTileReserve = 200;
	[SerializeField] private int tilesPerUpdate = 10;
	[SerializeField] private Player playerPrefab = null;
	[SerializeField] private float sightlineEyeHeight = 5.5f;
	private TilePool tilePool = null;
	private Map map = null;

	public static MapManager GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		instance = this;

		tilePool = new TilePool(minTileReserve, maxTileReserve, tilesPerUpdate, terrainPrefabs[0]);
		map = MapGenerator.GenerateMap(seed, mapWidth, mapHeight, tileSize, false, sightlineEyeHeight,
			terrainPrefabs, terrainHeightThresholds, terrainMaterials,
			smoothness, steepness,
			forestThreshold, resourceSmoothness, oreDepositChance,
			townPrefabs, maxTownCount,
			terrainParent,
			tilePool);

		// Spawn Player
		StartCoroutine(SetPlayerPosition(GameObject.Instantiate<Player>(playerPrefab)));
	}

	public void Update()
	{
		tilePool.Update();
	}

	// Delay setting Player Position, because Player Object needs to be fully awake and started
	private IEnumerator SetPlayerPosition(Player player)
	{
		yield return null;

		player.SetPosition(map.towns[UnityEngine.Random.Range(0, map.towns.Count)].gameObject.GetComponentInParent<Tile>());
	}

	public Map GetMap()
	{
		return map;
	}
}
