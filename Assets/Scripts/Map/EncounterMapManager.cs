using UnityEngine;

public class EncounterMapManager : MonoBehaviour
{
	private static EncounterMapManager instance = null;

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
	//[SerializeField] private bool generateResources = true;
	[SerializeField] private Transform terrainParent = null;
	[SerializeField] private Player playerPrefab = null;

    public static EncounterMapManager GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		instance = this;
	}

	public void EnterEncounterMap(Player player, Vector2Int position)
	{
		// We want to create a unique Seed for each Tile from the x- and y-Coordinates
		// Source: https://en.wikipedia.org/wiki/Pairing_function
		int seed = Mathf.RoundToInt(0.5f * (position.x + position.y) * (position.x + position.y + 1.0f) + position.y);

		Tile[,] encounterMap = MapGenerator.GenerateMap(seed, mapWidth, mapHeight, tileSize,
			terrainPrefabs, terrainHeightThresholds,
			smoothness, steepness,
			forestPrefabs, forestThreshold,
			resourceSmoothness,
			townPrefabs, maxTownCount,
			generateResources,
			terrainParent).Item1;
	}
}
