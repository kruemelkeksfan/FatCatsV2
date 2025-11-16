using System;
using System.Collections.Generic;
using UnityEngine;

public enum NeedCategory { None, Building, Food, Clothing, Medicine, Weapon, Tool, Fuel };
public enum EquipmentCategory { None, Head, Torso, Legs, Feet, Vest, Jacket, Backpack, Melee, Bow, Gun, Axe, Pickaxe, FishingRod };

[Serializable]
public struct GoodData
{
	public string goodName;
	public NeedCategory needCategory;
	public EquipmentCategory equipmentCategory;
	[Tooltip("Handyness per Unit, naked Players Hands and Arms have a Bulk Capacity of 10.")]
	public int bulk;
	[Tooltip("How perishable this Good is.")]
	public float decayPerDay;
	public float perceivedQualityFactor;
	public string[] statNames;
	public int[] statValues;
}

[Serializable]
public class Good
{
	public GoodData goodData;
	public float quality;
	public float perceivedQuality;
	public Inventory owner;

	public Good(GoodData good, float quality, float perceivedQuality, Inventory owner)
	{
		this.goodData = good;
		this.quality = quality;
		this.perceivedQuality = perceivedQuality * good.perceivedQualityFactor;
		this.owner = owner;
	}
}

public class GoodManager : MonoBehaviour
{
	private static GoodManager instance = null;

	[SerializeField] private GoodData[] goodData = { };
	private Dictionary<string, GoodData> goodDataDictionary = null;

	public static GoodManager GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		goodDataDictionary = new Dictionary<string, GoodData>(goodData.Length);
		foreach(GoodData good in goodData)
		{
			goodDataDictionary.Add(good.goodName, good);
		}

		instance = this;
	}

	public GoodData GetGoodData(string goodName)
	{
		return goodDataDictionary[goodName];
	}

	public GoodData[] GetGoodData()
	{
		return goodData;
	}
}
