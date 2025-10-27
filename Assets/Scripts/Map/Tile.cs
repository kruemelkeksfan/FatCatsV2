using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public struct Resource
{
	public string goodName;
	[Tooltip("Maximum Amount of Reserves of this Resource on an optimal Tile.")]
	public int maxAmount;
	[Tooltip("Maximum Amount of Reserves of this Resource on this Tile.")]
	public int localMaxAmount;
	[Tooltip("Maximum Restocking of Resources on this Tile per Day.")]
	public float maxGrowthAmount;
	[Tooltip("Maximum Amount of this Resource on a single Tile in an Encounter Map.")]
	public int maxDepositSize;
	[Tooltip("Use Wood Amount to calculate Restocking Rate instead of the Amount of this Resource.")]
	public bool forestDependent;
	[Tooltip("The Tool Type necessary to harvest this Resource.")]
	public EquipmentCategory tool;
	[Tooltip("How much of this Resource can be harvested per Hour, disregarding Light, Weather and Skill.")]
	public float baseYieldPerHour;
}

public class Tile : PanelObject
{
	public enum FogOfWar { Visible, Partial, Invisible };

	[SerializeField] private Resource[] resourceTypes = { };
	[SerializeField] private RectTransform resourceEntryPrefab = null;
	[SerializeField] private Transform exitMarkerPrefab = null;
	[SerializeField] private Transform movementPathMarkerPrefab = null;
	[SerializeField] private Transform movementProgressMarkerPrefab = null;
	[SerializeField] private Transform movementTargetMarkerPrefab = null;
	[SerializeField] private float movementProgressHeight = 10.0f;
	[SerializeField] private float baseMovementCost = 1.0f;
	[SerializeField] private float heightMovementCostFactor = 0.1f;
	[SerializeField] private float climbingMovementCostFactor = 0.5f;
	[SerializeField] private float forestMovementCostFactor = 1.2f;
	[SerializeField] private float townMovementCostFactor = 0.8f;
	[SerializeField] private int maxVisionRange = 2;
	private Vector2Int position = Vector2Int.zero;
	private string biome = "";
	private float height = 0.0f;
	private Tile parentTile = null;
	private Town town = null;
	private bool forest = true;
	private Dictionary<string, int> resourceDictionary = null;
	private Resource woodReference = new Resource();
	private new Transform transform = null;
	private Transform movementPathMarker = null;
	private Transform movementProgressMarker = null;
	private Transform movementTargetMarker = null;
	private Vector3 movementDirection = Vector3.zero;
	private FogOfWar fogOfWar = FogOfWar.Visible;
	private Vector3 initialResourceMarkerSize = Vector3.one;
	private string currentResourceFilter = string.Empty;

	private void Awake()
	{
		transform = gameObject.GetComponent<Transform>();

		SetFogOfWar(FogOfWar.Invisible);
	}

	protected override void Start()
	{
		base.Start();

		if(parentTile == null)
		{
			TimeController.GetInstance().AddDailyUpdateListener(UpdateTile, TimeController.PriorityCategory.Tile);
		}

		// Rotate Fog of War randomly
		transform.GetChild(0).Rotate(0.0f, UnityEngine.Random.Range(0.0f, 360.0f), 0.0f);
		transform.GetChild(1).Rotate(0.0f, UnityEngine.Random.Range(0.0f, 360.0f), 0.0f);

		// Save Resource Marker Size
		initialResourceMarkerSize = transform.GetChild(2).GetChild(0).localScale;
	}

	public void InitData(Vector2Int position, string biome, float height, bool forest, Tile parentTile)
	{
		this.position = position;
		this.biome = biome;
		this.height = height;
		this.parentTile = parentTile;
		this.forest = forest;
	}

	public void InitResources(float[] resourceAmountFactors)
	{
		resourceDictionary = new Dictionary<string, int>(resourceTypes.Length);
		for(int i = 0; i < resourceTypes.Length; ++i)
		{
			resourceTypes[i].localMaxAmount = Mathf.FloorToInt(resourceAmountFactors[i] * resourceTypes[i].maxAmount);
			resourceTypes[i].baseYieldPerHour *= ((float)resourceTypes[i].localMaxAmount) / ((float)resourceTypes[i].maxAmount);
			resourceDictionary.Add(resourceTypes[i].goodName, resourceTypes[i].localMaxAmount);

			if(i == 0)
			{
				woodReference = resourceTypes[i];
			}
		}

		SetFogOfWar(Tile.FogOfWar.Invisible);
	}

	public void InitEncounterMapResources(int[] resourceAmounts, float? exitMarkerAngle)
	{
		resourceDictionary = new Dictionary<string, int>(resourceTypes.Length);
		for(int i = 0; i < resourceTypes.Length; ++i)
		{
			resourceTypes[i].maxAmount = resourceTypes[i].maxDepositSize;
			resourceTypes[i].localMaxAmount = resourceAmounts[i];
			resourceTypes[i].baseYieldPerHour *= ((float)resourceTypes[i].localMaxAmount) / ((float)resourceTypes[i].maxAmount);
			resourceDictionary.Add(resourceTypes[i].goodName, resourceTypes[i].localMaxAmount);

			if(i == 0)
			{
				woodReference = resourceTypes[i];
			}
		}

		if(exitMarkerAngle.HasValue)
		{
			GameObject.Instantiate<Transform>(exitMarkerPrefab, transform.position, Quaternion.Euler(0.0f, exitMarkerAngle.Value, 0.0f), transform);
		}

		SetFogOfWar(Tile.FogOfWar.Partial);
	}

	public void UpdateTile(double time)
	{
		// Only called on Overworld Map Tiles, Listener is not set up for Encounter Map Tiles

		// Update Resources
		foreach(Resource resource in resourceTypes)
		{
			if(resource.localMaxAmount <= 0)
			{
				continue;
			}

			if(resource.forestDependent)
			{
				resourceDictionary[resource.goodName] = Mathf.Clamp(
					resourceDictionary[resource.goodName] + Mathf.RoundToInt(resource.maxGrowthAmount * ((float)resourceDictionary["Wood"] / (float)woodReference.localMaxAmount)),
					0, resource.localMaxAmount);
			}
			else
			{
				resourceDictionary[resource.goodName] = Mathf.Clamp(
					resourceDictionary[resource.goodName] + Mathf.RoundToInt(resource.maxGrowthAmount * ((float)resourceDictionary[resource.goodName] / (float)resource.localMaxAmount)),
					0, resource.localMaxAmount);
			}
		}
		UpdateResourceDisplay();
	}

	public override void UpdatePanel(RectTransform panel)
	{
		base.UpdatePanel(panel);

		panel.GetChild(0).GetComponentInChildren<TMP_Text>().text = "(" + position.x + "|" + position.y + ") " + biome;

		RectTransform resourceParent = (RectTransform)panel.GetChild(1);

		// TODO: Show Estimations instead of real Amounts (Estimation Accuracy based on Foragng Skill)
		// TODO: Show last (visible) Estimation when Tile is partially visible
		int i = 0;
		foreach(Resource resource in resourceTypes)
		{
			RectTransform resourceEntry;
			if(i < resourceParent.childCount)
			{
				resourceEntry = (RectTransform)resourceParent.GetChild(i);
			}
			else
			{
				resourceEntry = GameObject.Instantiate<RectTransform>(resourceEntryPrefab, resourceParent);
				resourceEntry.anchoredPosition = new Vector2(resourceEntry.anchoredPosition.x, -resourceEntry.sizeDelta.y * i);
			}

			resourceEntry.GetChild(0).GetComponent<TMP_Text>().text = resource.goodName;
			resourceEntry.GetChild(1).GetComponent<TMP_Text>().text = resourceDictionary[resource.goodName].ToString();
			resourceEntry.GetChild(2).GetComponent<TMP_Text>().text = "/" + resource.localMaxAmount;
			Player player = null;
			if(parentTile != null && (player = gameObject.GetComponentInChildren<Player>()) != null) // TODO: After Multiplayer Implementation check, if this is the local Player
			{
				Button collectButton = resourceEntry.GetChild(3).GetComponent<Button>();
				collectButton.gameObject.SetActive(true);

				collectButton.onClick.RemoveAllListeners();
				Resource localResource = resource;
				collectButton.onClick.AddListener(delegate
				{
					player.CollectResources(localResource, this, player.GetInventory());
				});
			}
			else
			{
				resourceEntry.GetChild(3).gameObject.SetActive(false);
			}
			// resourceEntry.GetChild(4).GetComponent<Button>().onClick.AddListener(); // TODO: Add ResourceInfoPanel

			if(i % 2 == 0)
			{
				resourceEntry.GetComponent<Image>().enabled = false;
			}

			++i;
		}

		while(i < resourceParent.childCount)
		{
			GameObject.Destroy(resourceParent.GetChild(i).gameObject);
			++i;
		}
	}

	public float CalculateMovementCost(Tile sourceTile = null, float movementCostFactor = 1.0f, Vector2? startPosition = null, Vector2? targetPosition = null)
	{
		// Source for Approximate Movement Costs: https://traildweller.com/hiking-time-calculator/
		float movementCost = baseMovementCost + (height * heightMovementCostFactor);

		if(sourceTile != null)
		{
			movementCost += Mathf.Abs(height - sourceTile.GetHeight()) * climbingMovementCostFactor;
		}

		if(forest)
		{
			movementCost *= forestMovementCostFactor;
		}
		if(town != null)
		{
			movementCost *= townMovementCostFactor;
		}

		movementCost *= movementCostFactor;

		if(startPosition != null && targetPosition != null)
		{
			movementCost *= Mathf.Max((targetPosition.Value - startPosition.Value).magnitude, 0.0001f);
		}

		return movementCost;
	}

	public float CalculateBestMovementCost()
	{
		return baseMovementCost * townMovementCostFactor;
	}

	public void MarkMovementPath()
	{
		if(movementPathMarker == null)
		{
			movementPathMarker = GameObject.Instantiate<Transform>(movementPathMarkerPrefab, transform);
		}
	}

	public void MarkMovementProgress(Tile nextTile)
	{
		if(movementProgressMarker == null)
		{
			movementProgressMarker = GameObject.Instantiate<Transform>(movementProgressMarkerPrefab, transform);

			Vector3 currentTilePosition = new Vector3(transform.position.x, transform.position.y + movementProgressHeight, transform.position.z);
			Vector3 nextTilePosition = new Vector3(nextTile.transform.position.x, nextTile.transform.position.y + movementProgressHeight, nextTile.transform.position.z);
			movementDirection = nextTilePosition - currentTilePosition;

			movementProgressMarker.position = currentTilePosition;
			movementProgressMarker.rotation = Quaternion.LookRotation(movementDirection, Vector3.up);
			movementProgressMarker.localScale = new Vector3(
				movementProgressMarker.localScale.x,
				movementProgressMarker.localScale.y,
				0.0f);
		}
	}

	public void MarkMovementTarget()
	{
		if(movementTargetMarker == null)
		{
			movementTargetMarker = GameObject.Instantiate<Transform>(movementTargetMarkerPrefab, transform);
		}
	}

	public void UpdateMovementProgress(float progress)
	{
		if(movementProgressMarker != null)
		{
			if(movementDirection.magnitude * progress * 0.1f > 1000.0f)
			{
				Debug.LogWarning("Invalid Movement Direction: " + movementDirection.magnitude + " " + progress);
			}

			movementProgressMarker.localScale = new Vector3(
				movementProgressMarker.localScale.x,
				movementProgressMarker.localScale.y,
				movementDirection.magnitude * progress * 0.1f);
		}
	}

	public void UnsetMovementMarkers()
	{
		if(movementPathMarker != null)
		{
			GameObject.Destroy(movementPathMarker.gameObject);
			movementPathMarker = null;
		}
		if(movementProgressMarker != null)
		{
			GameObject.Destroy(movementProgressMarker.gameObject);
			movementProgressMarker = null;
		}
		if(movementTargetMarker != null)
		{
			GameObject.Destroy(movementTargetMarker.gameObject);
			movementTargetMarker = null;
		}
	}

	public int HarvestResources(string resource, int amount)
	{
		// TODO: Save harvested Resources, so that Resources do not magically respawn upon leaving and reentering the Encounter Map; saved Harvests can be reset when no Player was on the Tile for some Days

		int harvestedAmount = amount;
		if(resourceDictionary[resource] >= amount)
		{
			resourceDictionary[resource] -= amount;
		}
		else
		{
			harvestedAmount = resourceDictionary[resource];
			resourceDictionary[resource] = 0;
		}

		if(parentTile != null)
		{
			parentTile.resourceDictionary[resource] -= harvestedAmount;
			if(parentTile.resourceDictionary[resource] < 0)
			{
				parentTile.resourceDictionary[resource] = 0;
			}
		}

		panelManager.QueuePanelUpdate(this);

		UpdateResourceDisplay();

		return harvestedAmount;
	}

	public void UpdateResourceDisplay(string resourceFilter = null)
	{
		if(resourceFilter != null)
		{
			currentResourceFilter = resourceFilter;
		}

		if(fogOfWar != FogOfWar.Invisible)
		{
			// Order of Resource Parent Children should match Resources in Resource Array
			Transform resourceParent = transform.GetChild(2);
			float resourceMarkerSize = 1.0f;
			for(int i = 0; i < resourceParent.childCount - 1; ++i)
			{
				Transform resourceGroup = resourceParent.GetChild(i + 1);
				int nodeCount = Mathf.CeilToInt(((float)resourceDictionary[resourceTypes[i].goodName] / (float)resourceTypes[i].maxAmount) * resourceGroup.childCount);
				for(int j = 0; j < resourceGroup.childCount; ++j)
				{
					// Set the right Amount of Nodes active and disable the Rest
					resourceGroup.GetChild(j).gameObject.SetActive(j < nodeCount);
				}

				if(resourceTypes[i].goodName == currentResourceFilter)
				{
					resourceMarkerSize = (float)resourceDictionary[resourceTypes[i].goodName] / (float)resourceTypes[i].maxAmount;
				}
			}

			if(currentResourceFilter != string.Empty && town == null)
			{
				Transform resourceMarker = resourceParent.GetChild(0);
				resourceMarker.localScale = initialResourceMarkerSize * resourceMarkerSize;
				resourceMarker.gameObject.SetActive(true);
			}
			else
			{
				resourceParent.GetChild(0).gameObject.SetActive(false);
			}
		}
	}

	public bool IsForest()
	{
		return forest;
	}

	public Vector2Int GetPosition()
	{
		return position;
	}

	public float GetHeight()
	{
		return height;
	}

	public Transform GetTransform()
	{
		return transform;
	}

	public Town GetTown()
	{
		return town;
	}

	public Resource[] GetResourceTypes()
	{
		return resourceTypes;
	}

	public int GetResourceAmount(string resource)
	{
		return resourceDictionary[resource];
	}

	public FogOfWar GetFogOfWar()
	{
		return fogOfWar;
	}

	public int GetMaxVisionRange()
	{
		return maxVisionRange;
	}

	public void SetForest(bool forest)
	{
		this.forest = forest;
	}

	public void SetTown(Town town)
	{
		this.town = town;
	}

	public void SetFogOfWar(FogOfWar fogOfWar)
	{
		this.fogOfWar = fogOfWar;

		if(fogOfWar == FogOfWar.Invisible)
		{
			transform.GetChild(2).gameObject.SetActive(false);
			if(town != null)
			{
				transform.GetChild(3).gameObject.SetActive(false);
			}

			transform.GetChild(0).gameObject.SetActive(true);
			transform.GetChild(1).gameObject.SetActive(false);

			gameObject.GetComponent<Collider>().enabled = false;
		}
		else if(fogOfWar == FogOfWar.Partial)
		{
			// Only enable Resource Display when no Town is present
			if(town != null)
			{
				transform.GetChild(3).gameObject.SetActive(true);
			}
			else
			{
				transform.GetChild(2).gameObject.SetActive(true);
			}

			transform.GetChild(0).gameObject.SetActive(false);
			transform.GetChild(1).gameObject.SetActive(true);

			gameObject.GetComponent<Collider>().enabled = true;

			UpdateResourceDisplay();
		}
		else
		{
			// Only enable Resource Display when no Town is present
			if(town != null)
			{
				transform.GetChild(3).gameObject.SetActive(true);
			}
			else
			{
				transform.GetChild(2).gameObject.SetActive(true);
			}

			transform.GetChild(0).gameObject.SetActive(false);
			transform.GetChild(1).gameObject.SetActive(false);

			gameObject.GetComponent<Collider>().enabled = true;

			UpdateResourceDisplay();
		}
	}
}
