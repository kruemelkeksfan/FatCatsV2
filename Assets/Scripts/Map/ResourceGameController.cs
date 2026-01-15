using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class ResourceGameController : MonoBehaviour
{
	private static ResourceGameController currentResourceGame = null;

	[SerializeField] private int minResourceAmount = 10;
	[SerializeField] private float timeSpeedupFactor = 20.0f;
	[SerializeField] private float minTimeSpeedup = 1.0f;
	[SerializeField] private float maxTimeSpeedup = 200.0f;
	[SerializeField] private float resourceSpawnChance = 0.02f;
	[SerializeField] private float resourceSpeed = 5.0f;
	[SerializeField] private float paddleSpeed = 5.0f;
	[SerializeField] private float collectionAnimationSizeIncrease = 1.01f;
	[SerializeField] private float collectionAnimationMaxWidth = 2.0f;
	[SerializeField] private RectTransform gameArea = null;
	[SerializeField] private RectTransform paddle = null;
	[SerializeField] private AudioClip collectionAudio = null;
	private GoodManager goodManager = null;
	private TimeController timeController = null;
	private InfoController infoController = null;
	private AudioController audioController = null;
	private Resource[] resourceTypes = null;
	private float maxPaddleDisplacement = 0.0f;
	private float[] resourceProbabilities = null;
	private string collectedResource = null;
	private Tile tile = null;
	private Inventory playerInventory = null;
	private Queue<Tuple<string, RectTransform>> resourceIcons = null;

	public static void StopCurrentResourceGame()
	{
		currentResourceGame?.StopGame();
	}

	private void Start()
	{
		goodManager = GoodManager.GetInstance();
		timeController = TimeController.GetInstance();
		infoController = InfoController.GetInstance();
		audioController = AudioController.GetInstance();

		maxPaddleDisplacement = gameArea.sizeDelta.x * 0.5f;
	}

	private void Update()
	{
		if(collectedResource == null || tile == null || playerInventory == null)
		{
			return;
		}

		// Limit gameTimeScale between minTimeSpeedup and maxTimeSpeedup
		float gameTimeScale = Mathf.Min((minTimeSpeedup + timeController.GetCurrentTimeScaleIndex() * timeSpeedupFactor), maxTimeSpeedup) * Time.deltaTime;

		// Paddle Movement
		paddle.anchoredPosition += Vector2.right * Input.GetAxis("Horizontal") * paddleSpeed * gameTimeScale;
		if(paddle.anchoredPosition.x <= -maxPaddleDisplacement)
		{
			paddle.anchoredPosition = new Vector2(-maxPaddleDisplacement, paddle.anchoredPosition.y);
		}
		else if(paddle.anchoredPosition.x >= maxPaddleDisplacement)
		{
			paddle.anchoredPosition = new Vector2(maxPaddleDisplacement, paddle.anchoredPosition.y);
		}

		foreach(Tuple<string, RectTransform> resource in resourceIcons)
		{
			RectTransform resourceIcon = resource.Item2;
			if(resourceIcon.gameObject.activeSelf)
			{
				// Check if the Resource Icon reached the Bottom
				if(resourceIcon.anchoredPosition.y - resourceIcon.sizeDelta.y <= -gameArea.sizeDelta.y)
				{
					if(resource.Item1 == collectedResource
						&& resourceIcon.anchoredPosition.x >= paddle.anchoredPosition.x - paddle.sizeDelta.x * 0.5f
						&& resourceIcon.anchoredPosition.x <= paddle.anchoredPosition.x + paddle.sizeDelta.x * 0.5f)
					{
						// Collection Animation
						if(resourceIcon.localScale.x < collectionAnimationMaxWidth)
						{
							resourceIcon.localScale *= 1.0f + (collectionAnimationSizeIncrease * Time.deltaTime);
						}
						else
						{
							tile.HarvestResources(collectedResource, 1);
							playerInventory.DepositGood(new Good(goodManager.GetGoodData(collectedResource), 1.0f, 1.0f, playerInventory), 1);    // TODO: Add real Quality and perceived Quality based on Skills

							infoController.AddMessage("Harvested an extra " + collectedResource + "!", false, false);
							audioController.PlayAudio(collectionAudio);

							resourceIcon.gameObject.SetActive(false);
						}
					}
					else
					{
						resourceIcon.gameObject.SetActive(false);
					}
				}
				else
				{
					// Resource Movement
					resourceIcon.anchoredPosition += Vector2.down * resourceSpeed * gameTimeScale;
				}
			}
		}
		// Resource Icon Despawning
		while(resourceIcons.Count > 0 && !resourceIcons.Peek().Item2.gameObject.activeSelf)
		{
			GameObject.Destroy(resourceIcons.Dequeue().Item2.gameObject);
		}

		// Resource Spawning
		for(int i = 0; i < resourceTypes.Length; ++i)
		{
			if(UnityEngine.Random.value < resourceProbabilities[i] * resourceSpawnChance * gameTimeScale)
			{
				RectTransform spawnedResourceIcon = GameObject.Instantiate<Image>(resourceTypes[i].resourceIcon, Vector3.zero, Quaternion.identity, gameArea).GetComponent<RectTransform>();
				spawnedResourceIcon.anchoredPosition = new Vector2(UnityEngine.Random.Range(-gameArea.sizeDelta.x * 0.5f, gameArea.sizeDelta.x * 0.5f), UnityEngine.Random.Range(0.0f, -resourceSpeed * gameTimeScale));
				resourceIcons.Enqueue(new Tuple<string, RectTransform>(resourceTypes[i].goodName, spawnedResourceIcon));
			}
		}
	}

	public void StartGame(Resource[] resourceTypes, Dictionary<string, int> resourceAmounts, string collectedResource, Tile tile, Inventory playerInventory)
	{
		if(currentResourceGame != null)
		{
			if(currentResourceGame == this)
			{
				UpdateResourceProbabilities(resourceAmounts);
				return;
			}
			else
			{
				currentResourceGame.StopGame();
			}
		}
		currentResourceGame = this;

		this.resourceTypes = resourceTypes;
		this.collectedResource = collectedResource;
		this.tile = tile;
		this.playerInventory = playerInventory;

		if(resourceIcons == null)
		{
			resourceIcons = new Queue<Tuple<string, RectTransform>>();
		}

		UpdateResourceProbabilities(resourceAmounts);
	}

	public void StopGame()
	{
		collectedResource = null;
		tile = null;
		playerInventory = null;

		if(currentResourceGame == this)
		{
			currentResourceGame = null;
		}

		foreach(Tuple<string, RectTransform> resourceIcon in resourceIcons)
		{
			if(resourceIcon.Item2 != null)
			{
				GameObject.Destroy(resourceIcon.Item2.gameObject);
			}
		}
		resourceIcons.Clear();
	}

	public void UpdateResourceProbabilities(Dictionary<string, int> resourceAmounts)
	{
		resourceProbabilities = new float[resourceTypes.Length];
		for(int i = 0; i < resourceProbabilities.Length; ++i)
		{
			// Prevent scarce Resources from being overfarmed (total Amount dropping below 0) in Minigame
			if(resourceAmounts[resourceTypes[i].goodName] > minResourceAmount)
			{
				resourceProbabilities[i] = (float)resourceAmounts[resourceTypes[i].goodName] / (float)resourceTypes[i].maxAmount;
			}
			else
			{
				resourceProbabilities[i] = 0.0f;
			}
		}
	}
}
