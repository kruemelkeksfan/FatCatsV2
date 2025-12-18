using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EncounterMap : Map
{
	public int encounterMapIndex;
	public List<Player> players;
	public Transform terrainParent;

	public EncounterMap(int encounterMapIndex, List<Player> players, Transform terrainParent, GameObject mapModel, Tile[,] tiles, float tileSize, float eyeHeight) : base(mapModel, tiles, new List<Town>(0), tileSize, eyeHeight)
	{
		this.encounterMapIndex = encounterMapIndex;
		this.players = players;
		this.terrainParent = terrainParent;
		this.tiles = tiles;
	}
}

public class EncounterMapManager : MonoBehaviour
{
	private static EncounterMapManager instance = null;	

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
	[SerializeField] private int maxEncounterMapCount = 168; // 8 + 16 + 24 + 32 + 40 + 48 = 168 => 6 Layers around World Map provide Room for 168 Encounter Maps
	[SerializeField] private float encounterMapOffset = 1000.0f;
	[SerializeField] private Transform exitMarkerPrefab = null;
	[SerializeField] private int minTileReserve = 100;
	[SerializeField] private int maxTileReserve = 200;
	[SerializeField] private int tilesPerUpdate = 10;
	[SerializeField] private float sightlineEyeHeight = 5.5f;
	private PanelManager panelManager = null;
	private InfoController infoController = null;
	private TilePool tilePool = null;
	private List<EncounterMap> encounterMaps = null;
	private Map map = null;

	public static EncounterMapManager GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		tilePool = new TilePool(minTileReserve, maxTileReserve, tilesPerUpdate, terrainPrefabs[0]);
		encounterMaps = new List<EncounterMap>(maxEncounterMapCount);

		instance = this;
	}

	private void Start()
	{
		panelManager = PanelManager.GetInstance();
		infoController = InfoController.GetInstance();

		map = MapManager.GetInstance().GetMap();

		/*for(int i = 0; i < maxEncounterMapCount; ++i)
		{
			EnterEncounterMap(null, Vector2Int.left);
		}*/
	}

	public void Update()
	{
		tilePool.Update();
	}

	public EncounterMap EnterEncounterMap(List<Player> players, Vector2Int position)
	{
		int currentLayer = 0;
		int currentTile = 0;
		float offsetX = 0.0f;
		float offsetY = 0.0f;
		for(int i = 0; i < maxEncounterMapCount; ++i)
		{
			++currentTile;

			// Calculate Offset
			if(currentTile >= currentLayer * 8)
			{
				++currentLayer;
				currentTile = 0;

				offsetX = -currentLayer * encounterMapOffset;
				offsetY = -currentLayer * encounterMapOffset;
			}
			else
			{
				if(currentTile < (currentLayer * 2 + 1))
				{
					offsetX += encounterMapOffset;
				}
				else if(currentTile < (currentLayer * 4 + 1))
				{
					offsetY += encounterMapOffset;
				}
				else if(currentTile < (currentLayer * 6 + 1))
				{
					offsetX -= encounterMapOffset;
				}
				else
				{
					offsetY -= encounterMapOffset;
				}
			}

			if(encounterMaps.Count >= i)
			{
				encounterMaps.Add(null);
			}

			if(encounterMaps[i] == null)
			{
				// We want to create a unique Seed for each Tile from the x- and y-Coordinates
				// Source: https://en.wikipedia.org/wiki/Pairing_function
				int seed = Mathf.RoundToInt(0.5f * (position.x + position.y) * (position.x + position.y + 1.0f) + position.y) + this.seed;

				Transform terrainParent = (new GameObject("EncounterMap" + i)).GetComponent<Transform>();
				Map encounterMap = MapGenerator.GenerateMap(seed, mapWidth, mapHeight, tileSize, true, sightlineEyeHeight,
					terrainPrefabs, terrainHeightThresholds, terrainMaterials,
					smoothness, steepness,
					forestThreshold, resourceSmoothness, oreDepositChance,
					null, 0,
					terrainParent,
					tilePool,
					offsetX, offsetY);

				encounterMaps[i] = new EncounterMap(i, players, terrainParent, encounterMap.mapModel, encounterMap.tiles, tileSize, sightlineEyeHeight);

				panelManager.CloseTilePanels();

				foreach(Player player in players)
				{
					// TODO: Prevent multiple Units from occupying the same Tile

					Vector2 playerPosition = player.GetEncounterPosition();
					int spawnX = Mathf.RoundToInt((playerPosition.x + 0.5f) * (encounterMap.tiles.GetLength(0) - 1));
					int spawnY = Mathf.RoundToInt((playerPosition.y + 0.5f) * (encounterMap.tiles.GetLength(1) - 1));
					player.SetPosition(encounterMap.tiles[spawnX, spawnY], encounterMaps[i]);
				}

				return encounterMaps[i];
			}
		}

		infoController.AddMessage("Too many active Encounter Maps, please wait", true, true);
		return null;
	}

	public void ExitEncounterMap(Player player, EncounterMap encounterMap, Tile mapTile)
	{
		// TODO: Allow Exit only if Player is at Map Edge and escape to next Tile
		// TODO: Alternatively allow Exit to Tile Center anywhere if out of Combat

		panelManager.CloseTilePanels();
		player.SetPosition(mapTile, map);
		
		encounterMap.players.Remove(player);
		if(encounterMap.players.Count <= 0)
		{
			foreach(Tile tile in encounterMap.tiles)
			{
				tilePool.ReturnTile(tile.GetTransform());
			}

			encounterMaps[encounterMap.encounterMapIndex] = null;
			GameObject.Destroy(encounterMap.terrainParent.gameObject);
		}
	}

	public int GetMapWidth()
	{
		return mapWidth;
	}

	public int GetMapHeight()
	{
		return mapHeight;
	}

	public float GetTileSize()
	{
		return tileSize;
	}
}
