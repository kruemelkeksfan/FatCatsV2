using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TimeController : MonoBehaviour
{
	public class Coroutine
	{
		public double timestamp = 0.0;
		public readonly IEnumerator<float> callback = null;
		public readonly bool isRealTime = false;

		public Coroutine(double timestamp, IEnumerator<float> callback, bool isRealTime)
		{
			this.timestamp = timestamp;
			this.callback = callback;
			this.isRealTime = isRealTime;
		}
	}

	// EXECUTION ORDER:
	// TC: Regenerate Tile Resources
	// IC: Good Decay
	// BC: Consume Resources for past Day
	// BC: Produce | Requirements: Tile Resources regenerated, Resources consumed
	// BC: Building Degradation | Requirements: Produce (Buildings should not degrade below their initial Quality before producing at least one Batch at max Quality)
	// BC: Construction Site Progress | Requirements: Building Degradation (Construction Site should not be deterioating before it just after potentially gaining Quality)
	// IC: Auto Trade Sell | Requirements: Produce (Products should not get stored 24h before being offered)
	// IC: Auto Trade Buy | Requirements: Auto Trade Sell (all possible Offers must be available at Marketplace)
	// BC: Fire unwanted Workers
	// BC: Check Liquidity/exclude open Positions if insufficient Funds | Requirements: Auto Trade Sell
	// BC: Hire/Fire based on Job Market | Requirements: Produce (you should not be able to hire productive Workers without paying them and onboarding them up to 24h), Fire unwanted Workers, Check Liquidity/exclude open Positions if insufficient Funds
	// BC: Subtract Worker Wage for next Day/fire unpaid Workers | Requirements: Auto Trade Sell, Hire/Fire based on Job Market
	// PC: Pay Workers for next Day | Requirements: Subtract Worker Wage for next Day from Player
	// PC: Population Consumption and Update | Requirements: Auto Trade Sell (produced Consumer Goods must be available), Pay Workers for next Day (Workers should receive Wage immediately upon Hire)

	public enum PriorityCategory { Tile = 0, GoodDecay = 1, Buildings = 2, AutoTradeSell = 3, AutoTradeBuy = 4, Workers = 5, Town = 6 };

	private static TimeController instance = null;

	[SerializeField] private float[] timeScales = { };
	[SerializeField] private Image[] timeButtons = { };
	[SerializeField] private Color activeTimeButtonColor = new Color();
	[SerializeField] private TMP_Text timeText = null;
	[SerializeField] private int daysPerYear = 400;
	private int currentTimeScaleIndex = 0;
	private double gameTime = 0.0;
	private float deltaTime = 0.0f;
	private float timeScale = 1.0f;
	private HashSet<Coroutine> gameTimeCoroutines = null;
	private HashSet<Coroutine> realTimeCoroutines = null;
	private double nextGameTimestamp = double.MaxValue;
	private double nextRealTimestamp = double.MaxValue;
	private List<Coroutine> iteratableCoroutines = null;
	private Queue<Action<double>>[] dailyUpdateListeners = null;
	private int lastDailyUpdate = 1;
	private Color? inactiveTimeButtonColor = null;
	private Map map = null;
	private int initialTotalSavings = -1;

	public static TimeController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		gameTimeCoroutines = new HashSet<Coroutine>();
		realTimeCoroutines = new HashSet<Coroutine>();
		iteratableCoroutines = new List<Coroutine>();
		int priorityCategoryCount = Enum.GetNames(typeof(PriorityCategory)).Length;
		dailyUpdateListeners = new Queue<Action<double>>[priorityCategoryCount];
		for(int i = 0; i < priorityCategoryCount; ++i)
		{
			dailyUpdateListeners[i] = new Queue<Action<double>>();
		}

		instance = this;
	}

	private void Start()
	{
		gameTime = 1.3334; // Start on Day 1, 8 a.m.
		SetTimeScale(0);
	}

	private void Update()
	{
		deltaTime = Time.deltaTime * timeScale;
		gameTime += deltaTime;

		if(gameTime >= nextGameTimestamp)
		{
			CallCoroutines(ref nextGameTimestamp, gameTime, gameTimeCoroutines);
		}
		float realTimeSinceStartup = Time.realtimeSinceStartup;
		if(realTimeSinceStartup >= nextRealTimestamp)
		{
			CallCoroutines(ref nextRealTimestamp, realTimeSinceStartup, realTimeCoroutines);
		}

		if(gameTime >= lastDailyUpdate + 1)
		{
			foreach(Queue<Action<double>> dailyUpdateListenerList in dailyUpdateListeners)
			{
				foreach(Action<double> dailyUpdateListener in dailyUpdateListenerList)
				{
					dailyUpdateListener(gameTime);
				}
			}

			// Debug Assertion to detect Money Sinks
			if(initialTotalSavings < 0)
			{
				map = MapManager.GetInstance().GetMap();
				initialTotalSavings = map.GetTotalSavings();
			}
			else
			{
				int totalSavings = map.GetTotalSavings();
				if(totalSavings != initialTotalSavings) // TODO: Replace with < after Implementation of Mint
				{
					Debug.LogWarning("Day " + gameTime + ": Total available Money changed from " + initialTotalSavings + " to " + totalSavings);
					initialTotalSavings = totalSavings;
				}
			}

			// Change Auto Trade buy Order, because not the same Person should be the first to pick Offers each Day
			// TODO: Test when AI exists
			dailyUpdateListeners[(int)PriorityCategory.AutoTradeBuy].Enqueue(dailyUpdateListeners[(int)PriorityCategory.AutoTradeBuy].Dequeue());

			++lastDailyUpdate;
		}

		timeText.text = BuildTimeString();
	}

	public Coroutine StartCoroutine(IEnumerator<float> callback, bool isRealTime)
	{
		if(callback.MoveNext())
		{
			if(isRealTime)
			{
				return StartCoroutine(ref nextRealTimestamp, Time.realtimeSinceStartup, realTimeCoroutines, callback, isRealTime);
			}
			else
			{
				return StartCoroutine(ref nextGameTimestamp, gameTime, gameTimeCoroutines, callback, isRealTime);
			}
		}

		return null;
	}

	public bool StopCoroutine(Coroutine coroutine)
	{
		if(coroutine.isRealTime)
		{
			return realTimeCoroutines.Remove(coroutine);
		}
		else
		{
			return gameTimeCoroutines.Remove(coroutine);
		}
	}

	private Coroutine StartCoroutine(ref double nextTimestamp, double time, HashSet<Coroutine> coroutines, IEnumerator<float> callback, bool isRealTime)
	{
		// If callback returns a Value <= 0.0, wait a minimum Amount of Time (== until next Frame)
		double newTimestamp = time + (callback.Current > 0.0 ? callback.Current : MathUtil.EPSILON);
		Coroutine coroutine = new Coroutine(newTimestamp, callback, isRealTime);
		coroutines.Add(coroutine);

		if(newTimestamp < nextTimestamp)
		{
			nextTimestamp = newTimestamp;
		}

		return coroutine;
	}

	private void CallCoroutines(ref double nextTimestamp, double time, HashSet<Coroutine> coroutines)
	{
		if(nextTimestamp <= time)
		{
			nextTimestamp = double.MaxValue;
		}
		iteratableCoroutines.Clear();
		// Copy beforehand and iterate over Copy to enable starting/stopping other Coroutines from within Coroutines without throwing ConcurrentModificationExceptions
		iteratableCoroutines.AddRange(coroutines);
		foreach(Coroutine coroutine in iteratableCoroutines)
		{
			if(time >= coroutine.timestamp)
			{
				if(coroutine.callback.MoveNext())
				{
					// If callback returns a Value <= 0.0, wait a minimum Amount of Time (== until next Frame)
					double newTimestamp = (coroutine.callback.Current > 0.0) ? (coroutine.timestamp + coroutine.callback.Current) : (time + MathUtil.EPSILON);
					coroutine.timestamp = newTimestamp;
				}
				else
				{
					// Defuse timestamp, so that it does not screw up nextTimestamp
					coroutine.timestamp = double.MaxValue;

					coroutine.callback.Dispose();
					coroutines.Remove(coroutine);
				}
			}

			if(coroutine.timestamp < nextTimestamp)
			{
				nextTimestamp = coroutine.timestamp;
			}
		}
	}

	public string BuildTimeString()
	{
		double clockTime = (gameTime % 1.0) * 24.0;
		int hour = (int)clockTime;
		int minute = (int)((clockTime % 1) * 60.0);
		return "Year " + GetCurrentYear() + ",  "
			+ "Day " + (((int)gameTime) % daysPerYear) + ",  "
			+ hour.ToString("00") + ":" + minute.ToString("00");
	}

	public void AddDailyUpdateListener(Action<double> listener, PriorityCategory prio)
	{
		dailyUpdateListeners[(int)prio].Enqueue(listener);
	}

	public int GetCurrentTimeScaleIndex()
	{
		return currentTimeScaleIndex;
	}

	public float GetTimeScale()
	{
		return timeScale;
	}

	public float GetDeltaTime()
	{
		return deltaTime;
	}

	public double GetTime()
	{
		return gameTime;
	}

	public int GetCurrentYear()
	{
		return ((int)gameTime) / daysPerYear;
	}

	public void SetTimeScale(int timeScaleIndex)
	{
		if(!inactiveTimeButtonColor.HasValue)
		{
			inactiveTimeButtonColor = timeButtons[0].color;
		}

		currentTimeScaleIndex = timeScaleIndex;
		timeScale = timeScales[timeScaleIndex];

		foreach(Image timeButton in timeButtons)
		{
			timeButton.color = inactiveTimeButtonColor.Value;
		}
		timeButtons[timeScaleIndex].color = activeTimeButtonColor;
	}

	public int GetDaysPerYear()
	{
		return daysPerYear;
	}
}
