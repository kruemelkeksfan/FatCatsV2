using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
	private enum Direction { Center = 0, West = 1, NorthWest = 2, NorthEast = 3, East = 4, SouthEast = 5, SouthWest = 6 };

	private struct Action
	{
		public delegate void EndAction();

		public string actionName;
		public double startTime;
		public double duration;
		public bool repeat;
		public EndAction endAction;

		public Action(string actionname, double startTime, double duration, bool repeat, TMP_Text characterActionText, EndAction endAction)
		{
			this.actionName = actionname;
			this.startTime = startTime;
			this.duration = duration;
			this.repeat = repeat;
			this.endAction = endAction;

			characterActionText.text = actionname;
		}
	}

	private static Player instance = null;

	[SerializeField] private string playerName = "PlayerNameHereOneDay";
	[SerializeField] private int startingMoney = 100;
	private Inventory inventory = null;
	private new Transform transform = null;
	private new Camera camera = null;
	private EventSystem eventSystem = null;
	private EncounterMapManager encounterMapManager = null;
	private List<Tile> path = null;
	private int pathIndex = 0;
	private float movementCostFactor = 1.0f;
	private Vector2[] encounterTransferPoints = null;
	private Vector2 encounterPosition = Vector2.zero; // Runs from (-0.5|-0.5) to (0.5|0.5)
	private Vector2 encounterStartPosition = Vector2.zero;
	private Vector2 encounterTargetPosition = Vector2.zero;
	private Vector2 nextEncounterStartPosition = Vector2.zero;
	private GoodManager goodManager = null;
	private TimeController timeController = null;
	private PanelManager panelManager = null;
	private InfoController infoController = null;
	private Action currentAction = new Action();
	private bool productive = false;
	private Tuple<Building, int> workplace = null;
	private TMP_Text characterActionText = null;
	private RectTransform characterActionProgressBar = null;
	private Vector2 characterActionProgressBarPosition = Vector2.zero;
	private Vector2 characterActionProgressBarSize = Vector2.one;
	private Button zoomButton = null;
	private bool inEncounter = false;
	private Tile currentWorldTile = null;
	private Map currentMap = null;

	public static Player GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		inventory = gameObject.GetComponent<Inventory>();
		transform = gameObject.GetComponent<Transform>();

		instance = this;
	}

	private void Start()
	{
		camera = Camera.main;
		eventSystem = EventSystem.current;
		goodManager = GoodManager.GetInstance();
		timeController = TimeController.GetInstance();
		panelManager = PanelManager.GetInstance();
		infoController = InfoController.GetInstance();
		encounterMapManager = EncounterMapManager.GetInstance();

		// Calculate Encounter Entry and Exit Points
		// Use a Circle for precise Locations, so that the Distances between the Transfer Points are realistic for every Direction
		encounterTransferPoints = new Vector2[7];
		encounterTransferPoints[0] = Vector2.zero;
		for(int i = 0; i < 6; ++i)
		{
			encounterTransferPoints[i + 1] = new Vector2(-Mathf.Cos(i * 0.3333f * Mathf.PI), Mathf.Sin(i * 0.3333f * Mathf.PI)) * 0.5f;
		}
		// Set those 2 Points manually to avoid Floating Point Errors
		encounterTransferPoints[1] = new Vector2(-0.5f, 0.0f);
		encounterTransferPoints[4] = new Vector2(0.5f, 0.0f);

		// Init Inventory
		inventory.SetPlayer(this, false);
		inventory.ChangeMoney(startingMoney);

		RectTransform characterPanel = panelManager.GetCharacterPanel();

		// Character Action UI
		Transform characterActionTransform = characterPanel.GetChild(2);
		characterActionText = characterActionTransform.GetChild(2).GetComponent<TMP_Text>();
		characterActionProgressBar = characterActionTransform.GetChild(0).GetComponent<RectTransform>();
		characterActionProgressBarPosition = characterActionProgressBar.anchoredPosition;
		characterActionProgressBarSize = characterActionProgressBar.sizeDelta;

		// Position Buttons
		zoomButton = characterPanel.GetChild(3).GetComponent<Button>();
		zoomButton.onClick.AddListener(delegate
		{
			ToggleEncounter();
		});

		// Spawn Town Inventories
		currentMap = MapManager.GetInstance().GetMap();
		foreach(Town town in currentMap.towns)
		{
			town.gameObject.GetComponent<BuildingController>().AddPlayerWarehouseInventory(this);
		}

		// Inventory Button
		characterPanel.GetChild(4).GetComponent<Button>().onClick.AddListener(delegate
		{
			panelManager.OpenPanel(inventory);
		});

		//inventory.DepositGood(new Good(GoodManager.GetInstance().GetGoodData("Fruits"), 1.0f, 1.0f, inventory), 2000);
		//inventory.DepositGood(new Good(GoodManager.GetInstance().GetGoodData("Wood"), 1.0f, 1.0f, inventory), 10);
		//inventory.DepositGood(new Good(GoodManager.GetInstance().GetGoodData("Stone"), 1.0f, 1.0f, inventory), 20);
		//inventory.DepositGood(new Good(GoodManager.GetInstance().GetGoodData("Twigs"), 0.5f, 0.5f, inventory), 500);

		/*for(int i = 0; i < 100; ++i)
		{
			inventory.DepositGood(new Good(GoodManager.GetInstance().GetGoodData("Fruits"), 1.0f, 0.1f * i, inventory), 1);
		}*/
		/*for(int i = 0; i < 10; i += 2)
		{
			inventory.DepositGood(new Good(GoodManager.GetInstance().GetGoodData("Twigs"), 1.0f, 0.1f * i, inventory), 100);
		}*/
		/*for(int i = 0; i < 10; i += 2)
		{
			inventory.DepositGood(new Good(GoodManager.GetInstance().GetGoodData("Flax"), 1.0f, 0.1f * i, inventory), 50);
		}*/

		ResetAction(true, false, true);
	}

	private void Update()
	{
		if(Input.GetMouseButtonDown(1) && !eventSystem.IsPointerOverGameObject())
		{
			Ray ray = camera.ScreenPointToRay(Input.mousePosition);
			RaycastHit hit;
			if(Physics.Raycast(ray, out hit, 10000.0f))
			{
				Tile targetTile = hit.collider.gameObject.GetComponentInParent<Tile>();
				if(targetTile != null)
				{
					Tile startTile = transform.parent.GetComponent<Tile>();
					List<Tile> newPath = MathUtil.FindPath(currentMap, startTile, targetTile);
					if(newPath != null)
					{
						ResetAction(true, false, false);
						currentAction = new Action("Ready to move", 0.0, 0.0, false, characterActionText, delegate
						{
						});
						path = newPath;

						if(path.Count > 1)
						{
							startTile.GetComponentInChildren<Market>()?.PlayerExit(inventory);

							foreach(Tile tile in path)
							{
								tile.MarkMovementPath();
							}
							startTile.MarkMovementProgress(path[1]);
							targetTile.MarkMovementTarget();
						}
					}
					else
					{
						infoController.AddMessage("Could not find a Path to this Target :/", true, true);
					}
				}
			}
		}

		double time = timeController.GetTime();
		double endTime = 0.0;
		do
		{
			endTime = (currentAction.duration > 0.0) ? (currentAction.startTime + currentAction.duration) : time;
			if(currentAction.duration > 0.0 && time >= endTime)
			{
				currentAction.endAction();
				if(currentAction.repeat)
				{
					currentAction.startTime = endTime;
				}
				else if(currentAction.actionName == "Moving")
				{
					currentAction = new Action("Ready to move", 0.0, 0.0, false, characterActionText, delegate
					{
					});
				}
				else
				{
					ResetAction(false, false, true);
					break;
				}
			}

			if(path != null)
			{
				if(currentAction.actionName == "Ready to move")
				{
					if(pathIndex < path.Count - 1)
					{
						movementCostFactor = CalculateMovementCostFactor();
						float tileMovementCost = 0.0f;
						if(!inEncounter)
						{
							Vector2Int tilePosition = path[pathIndex].GetPosition();
							Vector2Int tileStep = path[pathIndex + 1].GetPosition() - tilePosition;
							Direction exitDirection = Direction.Center;
							Direction nextEntryDirection = Direction.Center;
							if(tileStep.y > 0)
							{
								if((tilePosition.y % 2 == 0 && tileStep.x == 0) || tileStep.x > 0)
								{
									exitDirection = Direction.NorthEast;
									nextEntryDirection = Direction.SouthWest;
								}
								else
								{
									exitDirection = Direction.NorthWest;
									nextEntryDirection = Direction.SouthEast;
								}
							}
							else if(tileStep.y < 0)
							{
								if((tilePosition.y % 2 == 0 && tileStep.x == 0) || tileStep.x > 0)
								{
									exitDirection = Direction.SouthEast;
									nextEntryDirection = Direction.NorthWest;
								}
								else
								{
									exitDirection = Direction.SouthWest;
									nextEntryDirection = Direction.NorthEast;
								}
							}
							else
							{
								if(tileStep.x < 0)
								{
									exitDirection = Direction.West;
									nextEntryDirection = Direction.East;
								}
								else
								{
									exitDirection = Direction.East;
									nextEntryDirection = Direction.West;
								}
							}
							encounterStartPosition = encounterPosition;
							encounterTargetPosition = encounterTransferPoints[(int)exitDirection];
							nextEncounterStartPosition = encounterTransferPoints[(int)nextEntryDirection];

							tileMovementCost = path[pathIndex].CalculateMovementCost(((pathIndex >= 1) ? path[pathIndex - 1] : null), movementCostFactor, encounterPosition, encounterTargetPosition);
						}
						else
						{
							tileMovementCost = path[pathIndex].CalculateMovementCost(((pathIndex >= 1) ? path[pathIndex - 1] : null), movementCostFactor);
						}

						currentAction = new Action("Moving", endTime, tileMovementCost, false, characterActionText,
							delegate
							{
								SetPosition(path[pathIndex + 1]);

								panelManager.QueueAllPanelUpdate();

								++pathIndex;
							});
						path[pathIndex].MarkMovementProgress(path[pathIndex + 1]);

						// Print ETA here, because we calculate the Movement Cost for the first Tile here already
						if(pathIndex == 0)
						{
							// Special Treatment for first Tile Step, because it might be a lot shorter
							float eta = tileMovementCost;
							for(int i = 1; i < path.Count - 1; ++i)
							{
								eta += path[i].CalculateMovementCost(path[i - 1], movementCostFactor);
							}
							infoController.AddMessage("Movement ETA " + (eta * 24.0f).ToString("F2") + "h", false, false);
						}
					}
					else
					{
						if(path.Count > 1)
						{
							Town targetTown = path[path.Count - 1].GetTown();
							if(targetTown != null)
							{
								infoController.AddMessage("You arrived at " + targetTown.GetTownName(), false, false);
							}
							else
							{
								Vector2Int targetPosition = path[path.Count - 1].GetPosition();
								infoController.AddMessage("You arrived at (" + targetPosition.x + "|" + targetPosition.y + ")", false, false);
							}
						}

						ResetAction(true, false, true);
						break;
					}
				}

				float movementProgress = (float)((time - currentAction.startTime) / currentAction.duration);
				path[pathIndex].UpdateMovementProgress(movementProgress);

				if(!inEncounter)
				{
					encounterPosition = Vector2.Lerp(encounterStartPosition, encounterTargetPosition, movementProgress);
				}
			}
		}
		while(currentAction.duration > 0.0 && endTime < time);

		if(currentAction.duration > 0.0)
		{
			characterActionProgressBar.anchoredPosition = characterActionProgressBarPosition;
			characterActionProgressBar.sizeDelta = new Vector2(characterActionProgressBarSize.x * (float)((time - currentAction.startTime) / currentAction.duration), characterActionProgressBarSize.y);
		}
		else
		{
			characterActionProgressBar.anchoredPosition = new Vector2(
					characterActionProgressBarPosition.x
					+ characterActionProgressBarSize.x * 0.4f
					+ Mathf.Sin(Time.realtimeSinceStartup * 2.0f) * characterActionProgressBarSize.x * 0.4f,
					characterActionProgressBar.anchoredPosition.y);
			characterActionProgressBar.sizeDelta = new Vector2(characterActionProgressBarSize.x * 0.2f, characterActionProgressBarSize.y);
		}
	}

	public float CalculateMovementCostFactor()
	{
		int carryBulk = inventory.GetBulk();

		// First Part: Go multiple Times if you exceed your Bulk Capacity, int Division to floor Number implicitely
		float movementCostFactor = Mathf.Max(Mathf.Ceil((float)carryBulk / (float)inventory.GetBulkCapacity()), 1.0f);
		// Second Part: Linear Penalty for more Bulk, Slope of 0.01 means half Speed at 100 Bulk, can be reduced with Load Reduction Stat of Carrying Gear
		movementCostFactor += (carryBulk * 0.01f);

		return movementCostFactor;
	}

	public void ResetAction(bool resetPath, bool performEndAction, bool setIdle)
	{
		if(resetPath && path != null)
		{
			foreach(Tile tile in path)
			{
				tile.UnsetMovementMarkers();
			}

			path = null;
			pathIndex = 0;
		}

		if(workplace != null)
		{
			workplace.Item1.jobs[workplace.Item2].playerWorkers.Remove(this);
			workplace = null;
		}

		if(performEndAction)
		{
			currentAction.endAction();
		}

		if(setIdle)
		{
			currentAction = new Action("Idle", 0.0, 0.0, false, characterActionText, delegate
			{
			});

			if(path == null)
			{
				timeController.SetTimeScale(0);
			}
		}
	}

	public void CollectResources(Resource resource, Tile tile, Inventory collector)
	{
		ResetAction(true, false, false);

		// TODO: Check if necessary tool is equipped (resource.tool)
		// TODO: Tools need to increase harvestYield, bc harvestYield for trees is way too low right now
		// 0.04167 == 1h
		currentAction = new Action("Collecting " + resource.goodName, timeController.GetTime(), 0.04167, true, characterActionText, delegate
		{
			Good collectedGood = new Good(goodManager.GetGoodData(resource.goodName), 1.0f, 1.0f, collector);   // TODO: Add real Quality and perceived Quality based on Skills

			int availableResourceAmount = tile.GetResourceAmount(resource);

			float baseHoursPerYield = 1 / resource.baseYieldPerHour;
			float time = 0.0f;
			int harvestedResourceAmount = 0;
			while(time < 1.0f)
			{
				float hoursPerYield = baseHoursPerYield / (((float)availableResourceAmount) / ((float)resource.maxAmount));

				if(time + hoursPerYield <= 1.0f
					|| (time + hoursPerYield > 1.0f && UnityEngine.Random.value < ((1.0f - time) / hoursPerYield)))
				{
					++harvestedResourceAmount;
					--availableResourceAmount;
				}

				time += hoursPerYield;
			}

			int finalHarvestedResourceAmount = tile.HarvestResources(resource, harvestedResourceAmount);
			infoController.AddMessage("Harvested " + finalHarvestedResourceAmount + " " + resource.goodName, false, false);
			inventory.DepositGood(collectedGood, finalHarvestedResourceAmount);

			// Print Exhaustion Message here instead of in Tile.cs, because Player already has InfoController and we can better manage Message Order (Exhaustion Message should be printed after Collection message)
			if(finalHarvestedResourceAmount >= availableResourceAmount)
			{
				InfoController.GetInstance().AddMessage(resource.goodName + " exhausted here!", false, true);
			}
		});
	}

	public void WageLabour(Building building, int jobId, BuildingController buildingController)
	{
		ResetAction(true, false, false);
		double time = timeController.GetTime();
		currentAction = new Action("Working as " + building.jobs[jobId].jobName, time, (System.Math.Ceiling(time) + 0.0001 - time) + 0.0001, true, characterActionText, delegate
		{
			productive = true;

			if(building.underConstruction)
			{
				ConstructionSite constructionSite = buildingController.GetConstructionSite(building);

				panelManager.QueuePanelUpdate(buildingController); // Necessary to update ETAs for Building Completion

				currentAction.duration = constructionSite.enoughMaterial ? constructionSite.GetTimeLeft() : 1.0;
			}
			else
			{
				currentAction.duration = 1.0;
			}
		});
		workplace = new Tuple<Building, int>(building, jobId);
		productive = false; // Productivity will only be enabled after 1 full Cycle
	}

	public void RestartPathIfCheaper()
	{
		if(path != null && CalculateMovementCostFactor() < movementCostFactor)
		{
			// Start over Movement to apply lower Movement Cost
			ResetAction(false, false, true);
		}
	}

	private void ToggleEncounter()
	{
		ResetAction(true, false, true);

		inEncounter = !inEncounter;

		TMP_Text zoomButtonText = zoomButton.GetComponentInChildren<TMP_Text>();
		if(!inEncounter)
		{
			zoomButtonText.text = "Zoom In";

			encounterMapManager.ExitEncounterMap(this, (EncounterMap)currentMap, currentWorldTile);
		}
		else
		{
			zoomButtonText.text = "Zoom Out";

			List<Player> players = new List<Player>(1);
			players.Add(this);
			encounterMapManager.EnterEncounterMap(players, currentWorldTile.GetPosition()); // Side Effect: this Method also sets currentMap via SetPosition()
		}
	}

	public bool IsLocalPlayer()
	{
		// Remote Players get their own Class
		return true;
	}

	public bool IsProductive()
	{
		return productive;
	}

	public string GetPlayerName()
	{
		return playerName;
	}

	public Inventory GetInventory()
	{
		return inventory;
	}

	public Tile GetCurrentWorldTile()
	{
		return currentWorldTile;
	}

	public Map GetCurrentMap()
	{
		return currentMap;
	}

	public Vector2 GetEncounterPosition()
	{
		return encounterPosition;
	}

	public void SetPosition(Tile tile, Map currentMap = null)
	{
		if(currentMap != null)
		{
			this.currentMap = currentMap;
		}

		transform.SetParent(tile.GetTransform(), false);
		this.currentMap.UpdateFogOfWar(tile);

		// Only update Encounter Position during Movement, not during Zooming in and out
		if(currentMap == null)
		{
			if(!inEncounter)
			{
				currentWorldTile = tile;

				Town town = tile.GetTown();
				zoomButton.gameObject.SetActive(tile.GetTown() == null); // Do not display Zoom in Button on Town Tiles

				encounterPosition = nextEncounterStartPosition;
			}
			else
			{
				// TODO: Implement Button to zoom out and change Overworld Map Tile when reaching Map Edge (Maybe below Zoom Out Button?)

				Vector2Int tilePosition = tile.GetPosition();
				Vector2Int mapSize = this.currentMap.GetMapSize();
				// mapSize - 1 because we want to utilize the full encounterPosition Range from -0.5 to +0.5, without -1 we would only get to +0.4999
				encounterPosition = new Vector2(Mathf.Clamp01((float)tilePosition.x / (mapSize.x - 1.0f)) - 0.5f, Mathf.Clamp01((float)tilePosition.y / (mapSize.y - 1.0f)) - 0.5f);
			}
		}
	}
}
