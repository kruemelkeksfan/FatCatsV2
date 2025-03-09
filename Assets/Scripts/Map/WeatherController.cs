using UnityEngine;

public class WeatherController : MonoBehaviour
{
	[SerializeField] private Transform sunTransform = null;
	[SerializeField] private float maxDayNightCycleSpeedup = 2.0f;
	[SerializeField] private MinMax noonTemperatureRange = new MinMax(10.0f, 30.0f);
	[SerializeField] private MinMax temperatureVariationRange = new MinMax(4.0f, 10.0f);
	[SerializeField] private float[] temperatureSampleTimes = { 0.0f, 0.175f, 0.3f, 0.425f, 0.55f };
	// Orientation: https://www.researchgate.net/figure/Relative-diurnal-pattern-template-for-temperature-distribution-used-to-calculate-hourly_fig3_280931041
	// or: https://www.researchgate.net/publication/367915621/figure/fig3/AS:11431281116808416@1675329598297/Diurnal-cycle-of-temperature-C-upper-panel-and-UHI-intensity-C-lower-panel.ppm
	[SerializeField] private float[] temperatureSampleValues = { 0.0f, 0.75f, 1.0f, 0.8f, 0.4f };
	private TimeController timeController = null;
	private float noonTemperature = 0.0f;
	private float temperatureVariation = 0.0f;

	private void Start()
	{
		timeController = TimeController.GetInstance();

		// Randomize Temperature
		// TODO: Randomize each Day
		// TODO: Seasonal Influence
		noonTemperature = Random.Range(noonTemperatureRange.min, noonTemperatureRange.max);
		temperatureVariation = Random.Range(temperatureVariationRange.min, temperatureVariationRange.max);
	}

	private void Update()
	{
		if(timeController.GetTimeScale() <= maxDayNightCycleSpeedup)
		{
			float sunTime = ((float)timeController.GetTime()) * Mathf.PI * 2.0f;
			sunTransform.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(sunTime), Mathf.Cos(sunTime), 1.0f));

			GetTemperature();
		}
	}

	private float GetTemperature()
	{
		// TODO: Buffer last Temperature Value, if necessary (bc Function is called multiple Times per Frame)

		float dayTime = (float)timeController.GetTime();
		dayTime -= Mathf.Floor(dayTime); // Convert Time to Daytime
		int nextLowerSampleIndex = 0;
		while(nextLowerSampleIndex < temperatureSampleTimes.Length - 1 && temperatureSampleTimes[nextLowerSampleIndex + 1] < dayTime)
		{
			++nextLowerSampleIndex;
		}
		float progress = (dayTime - temperatureSampleTimes[nextLowerSampleIndex])
			/ (((nextLowerSampleIndex + 1 >= temperatureSampleTimes.Length) ? 1.0f + temperatureSampleTimes[0] : temperatureSampleTimes[nextLowerSampleIndex + 1])
			- temperatureSampleTimes[nextLowerSampleIndex]);
		float dayTimeTemperatureFactor = Mathf.Lerp(temperatureSampleValues[nextLowerSampleIndex], temperatureSampleValues[(nextLowerSampleIndex + 1) % temperatureSampleValues.Length], progress);
		return noonTemperature + temperatureVariation * dayTimeTemperatureFactor;
	}
}
