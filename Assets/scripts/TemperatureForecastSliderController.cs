using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Temperature slider controller for the forecast slider.
/// </summary>
public class TemperatureForecastSliderController : MonoBehaviour {

    public Slider tempSlider;
    public float minX;
    public float maxX;

    private RectTransform sliderTransform;
    private float startingWidth;
    private float totalWidth;

    private void Start()
    {
        sliderTransform = tempSlider.GetComponent<RectTransform>();
        startingWidth = sliderTransform.sizeDelta.x;
        totalWidth = Mathf.Abs(minX) + Mathf.Abs(maxX);
    }

    /// <summary>
    /// Updates the slider width and x-position based on current forecast predictions.
    /// </summary>
    /// <param name="forecastLow">Forecast low for the current day.</param>
    /// <param name="forecastHigh">Forecast high for the current day.</param>
    public void UpdateSliderPositions(float forecastLow, float forecastHigh)
    {
        sliderTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 
                                                  startingWidth * ((forecastHigh - forecastLow) / 40));
        sliderTransform.localPosition = new Vector3((forecastLow + forecastHigh) / 2 / 40 * totalWidth + minX, 
                                                     sliderTransform.localPosition.y, 
                                                     sliderTransform.localPosition.z);
    }
}
