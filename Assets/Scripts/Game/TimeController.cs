﻿using System;
using System.Collections;
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

	// Order:
	// Tile: Regenerate Resources
	// Inventory: Item Decay, AutoTrade Offers
	// Town: Produce Items, Consume Items, Manage Workers
	public enum Order { Tile = 0, Inventory = 1, Town = 2};

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
	private List<IListener>[] dailyUpdateListeners = null;
	private int lastDailyUpdate = 1;
	private Color? inactiveTimeButtonColor = null;

	public static TimeController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		gameTimeCoroutines = new HashSet<Coroutine>();
		realTimeCoroutines = new HashSet<Coroutine>();
		iteratableCoroutines = new List<Coroutine>();
		dailyUpdateListeners = new List<IListener>[] {new List<IListener>(), new List<IListener>(), new List<IListener>()};

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
			foreach(List<IListener> dailyUpdateListenerList in dailyUpdateListeners)
			{
				foreach(IListener dailyUpdateListener in dailyUpdateListenerList)
				{
					dailyUpdateListener.Notify();
				}
			}

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
		int hour = (int) clockTime;
		int minute = (int) ((clockTime % 1) * 60.0);
		return "Year " + (((int) gameTime) / daysPerYear) + ",  "
			+ "Day " + (((int) gameTime) % daysPerYear) + ",  "
			+ hour.ToString("00") + ":" + minute.ToString("00");
	}

	public void AddDailyUpdateListener(IListener listener, Order prio)
	{
		dailyUpdateListeners[(int) prio].Add(listener);
	}

	public bool IsScaled()
	{
		return currentTimeScaleIndex != 0;
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
}
