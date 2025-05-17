using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public struct Resource
{
	public string goodName;
	[Tooltip("Maximum Amount of Reserves of this Resource on this Tile.")]
	public int maxAmount;
	[Tooltip("Maximum Restocking of Resources on this Tile per Day.")]
	public float maxGrowthAmount;
	[Tooltip("Use Wood Amount to calculate Restocking Rate instead of the Amount of this Resource.")]
	public bool forestDependent;
	[Tooltip("The Tool Type necessary to harvest this Resource.")]
	public EquipmentCategory tool;
	[Tooltip("How much of this Resource can be harvested per Hour, disregarding Light, Weather and Skill.")]
	public float baseYieldPerHour;
	[Tooltip("Name of the Collection Building.")]
	public string buildingName;
}

public class Tile : PanelObject, IListener
{
	public enum FogOfWar { Visible, Partial, Invisible };

	[SerializeField] private Resource[] resourceTypes = { };
	[SerializeField] private RectTransform resourceEntryPrefab = null;
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
	private Town town = null;
	private bool forest = true;
	private Dictionary<Resource, int> resourceDictionary = null;
	private Resource woodReference = new Resource();
	private new Transform transform = null;
	private Transform movementPathMarker = null;
	private Transform movementProgressMarker = null;
	private Transform movementTargetMarker = null;
	private Vector3 movementDirection = Vector3.zero;
	private FogOfWar fogOfWar = FogOfWar.Visible;

	private void Awake()
	{
		transform = gameObject.GetComponent<Transform>();
	}

	protected override void Start()
	{
		base.Start();

		TimeController.GetInstance().AddDailyUpdateListener(this, TimeController.Order.Tile);
	}

	public void InitData(Vector2Int position, string biome, float height, bool forest, float[] resourceAmountFactor)
	{
		this.position = position;
		this.biome = biome;
		this.height = height;
		this.forest = forest;

		resourceDictionary = new Dictionary<Resource, int>(resourceTypes.Length);
		for(int i = 0; i < resourceTypes.Length; ++i)
		{
			int localMax = Mathf.FloorToInt(resourceAmountFactor[i] * resourceTypes[i].maxAmount);
			resourceTypes[i].baseYieldPerHour *= ((float)localMax) / ((float)resourceTypes[i].maxAmount);
			resourceTypes[i].maxAmount = localMax;
			resourceDictionary.Add(resourceTypes[i], localMax);

			if(i == 0)
			{
				woodReference = resourceTypes[i];
			}
		}

		SetFogOfWar(FogOfWar.Invisible);
	}

	public override void UpdatePanel(RectTransform panel, bool add = true)
	{
		base.UpdatePanel(panel);

		panel.GetChild(0).GetComponentInChildren<TMP_Text>().text = "(" + position.x + "|" + position.y + ") " + biome;

		RectTransform resourceParent = (RectTransform)panel.GetChild(1);

		int i = 0;
		foreach(Resource resourceType in resourceTypes)
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

			resourceEntry.GetChild(0).GetComponent<TMP_Text>().text = resourceType.goodName;
			resourceEntry.GetChild(1).GetComponent<TMP_Text>().text = resourceDictionary[resourceType].ToString();
			resourceEntry.GetChild(2).GetComponent<TMP_Text>().text = "/" + resourceType.maxAmount;
			Player player = gameObject.GetComponentInChildren<Player>();    // TODO: After Multiplayer Implementation check, if this is the local Player
			if(player != null)
			{
				Button collectButton = resourceEntry.GetChild(3).GetComponent<Button>();
				collectButton.gameObject.SetActive(true);

				collectButton.onClick.RemoveAllListeners();
				Resource localResourceType = resourceType;
				collectButton.onClick.AddListener(delegate
				{
					player.CollectResources(localResourceType, this, player.GetInventory());
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

		while(resourceParent.childCount > i)
		{
			Transform child = resourceParent.GetChild(i);
			child.SetParent(null, false);
			GameObject.Destroy(child.gameObject);
		}
	}

	public void Notify()
	{
		foreach(Resource resourceType in resourceTypes)
		{
			if(resourceType.maxAmount <= 0)
			{
				continue;
			}

			if(resourceType.forestDependent)
			{
				resourceDictionary[resourceType] = Mathf.Clamp(
					resourceDictionary[resourceType] + Mathf.RoundToInt(resourceType.maxGrowthAmount * ((float)resourceDictionary[woodReference] / (float)woodReference.maxAmount)),
					0, resourceType.maxAmount);
			}
			else
			{
				resourceDictionary[resourceType] = Mathf.Clamp(
					resourceDictionary[resourceType] + Mathf.RoundToInt(resourceType.maxGrowthAmount * ((float)resourceDictionary[resourceType] / (float)resourceType.maxAmount)),
					0, resourceType.maxAmount);
			}
		}
	}

	public float CalculateMovementCost(Tile sourceTile = null, float movementCostFactor = 1.0f)
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

	public int HarvestResources(Resource resource, int amount)
	{
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

		panelManager.QueuePanelUpdate(this);

		return harvestedAmount;
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

	public int GetResourceAmount(Resource resource)
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
			if(transform.childCount > 2)
			{
				transform.GetChild(2).gameObject.SetActive(false);
			}

			transform.GetChild(0).gameObject.SetActive(true);
			transform.GetChild(1).gameObject.SetActive(false);
		}
		else
		{
			if(transform.childCount > 2)
			{
				transform.GetChild(2).gameObject.SetActive(true);
			}

			if(fogOfWar == FogOfWar.Partial)
			{
				transform.GetChild(0).gameObject.SetActive(false);
				transform.GetChild(1).gameObject.SetActive(true);
			}
			else
			{
				transform.GetChild(0).gameObject.SetActive(false);
				transform.GetChild(1).gameObject.SetActive(false);
			}
		}
	}
}
