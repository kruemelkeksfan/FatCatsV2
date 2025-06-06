using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
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
	[SerializeField] private bool localPlayer = true;
	private Inventory inventory = null;
	private new Transform transform = null;
	private new Camera camera = null;
	private EventSystem eventSystem = null;
	private EncounterMapManager encounterMapManager = null;
	private List<Tile> path = null;
	private GoodManager goodManager = null;
	private TimeController timeController = null;
	private PanelManager panelManager = null;
	private InfoController infoController = null;
	private Action currentAction = new Action();
	private bool productive = false;
	private int pathIndex = 1;
	private Tuple<Building, int> workplace = null;
	private TMP_Text characterActionText = null;
	private RectTransform characterActionProgressBar = null;
	private Vector2 characterActionProgressBarPosition = Vector2.zero;
	private Vector2 characterActionProgressBarSize = Vector2.one;
	private Button tileInfoButton = null;
	private Button zoomButton = null;
	private bool inEncounter = false;
	private Tile currentTile = null;
	private Map currentMap = null;
	private EncounterMap encounterMap = null;

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
		tileInfoButton = characterPanel.GetChild(3).GetComponent<Button>();
		zoomButton = characterPanel.GetChild(4).GetComponent<Button>();
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
		characterPanel.GetChild(5).GetComponent<Button>().onClick.AddListener(delegate
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
						currentAction = new Action("Ready to move", 0.0, double.MaxValue, false, characterActionText, delegate
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

							float eta = 0.0f;
							float movementCostFactor = CalculateMovementCostFactor();
							for(int i = 1; i < path.Count; ++i)
							{
								eta += path[i].CalculateMovementCost(path[i - 1], movementCostFactor);
							}
							infoController.AddMessage("Movement ETA " + (eta * 24.0f).ToString("F2") + "h", false, false);
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
			endTime = currentAction.duration > 0.0 ? (currentAction.startTime + currentAction.duration) : time + 1.0; // Duration of infinite Actions like Idle is 0.0 and would lead to infinite Loops
			if(time >= endTime)
			{
				currentAction.endAction();
				if(currentAction.repeat)
				{
					currentAction.startTime = endTime;
				}
				else if(currentAction.actionName == "Moving")
				{
					currentAction = new Action("Ready to move", 0.0, double.MaxValue, false, characterActionText, delegate
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
					if(pathIndex < path.Count)
					{
						currentAction = new Action("Moving", (endTime < time) ? endTime : time,
							path[pathIndex].CalculateMovementCost(path[pathIndex - 1], CalculateMovementCostFactor()), false,
							characterActionText,
							delegate
							{
								SetPosition(path[pathIndex]);

								if(localPlayer)
								{
									panelManager.QueueAllPanelUpdate();
								}

								++pathIndex;
							});
						path[pathIndex - 1].MarkMovementProgress(path[pathIndex]);
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

				path[pathIndex - 1].UpdateMovementProgress((float)((time - currentAction.startTime) / currentAction.duration));
			}
		}
		while(endTime < time);

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
		int bulkCapacity = inventory.GetBulkCapacity();

		// First Part: Go multiple Times if you exceed your Bulk Capacity, int Division to floor Number implicitely
		float movementCostFactor = Mathf.Max(Mathf.Ceil((float)carryBulk / (float)bulkCapacity), 1.0f);
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
			pathIndex = 1;
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
		if(path != null && path[pathIndex].CalculateMovementCost(path[pathIndex - 1], CalculateMovementCostFactor()) < currentAction.duration - (timeController.GetTime() - currentAction.startTime))
		{
			// Start over Movement to apply lower Movement Cost
			ResetAction(false, false, true);
		}
	}

	private void ToggleEncounter()
	{
		inEncounter = !inEncounter;

		ResetAction(true, false, true);

		if(encounterMap != null)
		{
			encounterMapManager.ExitEncounterMap(this, encounterMap, currentTile);
			encounterMap = null;
		}

		TMP_Text zoomButtonText = zoomButton.GetComponentInChildren<TMP_Text>();
		if(inEncounter)
		{
			zoomButtonText.text = "Zoom Out";

			List<Player> players = new List<Player>(1);
			players.Add(this);
			encounterMap = encounterMapManager.EnterEncounterMap(players, currentTile.GetPosition()); // Side Effect: this Method also sets currentMap via SetPosition()
		}
		else
		{
			zoomButtonText.text = "Zoom In";

			currentMap = MapManager.GetInstance().GetMap();
		}
	}

	public bool IsLocalPlayer()
	{
		return localPlayer;
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

	public Tile GetCurrentTile()
	{
		return currentTile;
	}

	public void SetPosition(Tile tile, Map currentMap = null)
	{
		if(currentMap != null)
		{
			this.currentMap = currentMap;
		}

		transform.SetParent(tile.GetTransform(), false);
		this.currentMap.UpdateFogOfWar(tile);

		if(!inEncounter)
		{
			currentTile = tile;

			Town town = tile.GetTown();
			tileInfoButton.onClick.RemoveAllListeners();
			tileInfoButton.onClick.AddListener(delegate
			{
				panelManager.OpenPanel((town != null) ? town : tile);
			});
		}
	}
}
