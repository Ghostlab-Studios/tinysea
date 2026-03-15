using UnityEngine;
using System.Collections;

public class ThermalCurve : MonoBehaviour {

    public float interval = 0.1f;
    public float[] data;
    //public dataChart chart;
    //TODO: the datachart should have a slot for a thermalcurve, 
    //and also track the cursor, colors, etc.

    public float optimalTemp = 295.15f;
    public float arrhenBreadth = 4258;
    public float arrhenLower = 7457;
    public float arrhenUpper = 19664;
    public float lowerBound = 286;
    public float upperBound = 298;

    public float pmax = 1.0f;
    public float ctMinC = -5.0f;
    public float ctMaxC = 40.0f;

    private const float LETHAL_TRANSITION_WIDTH = 2.0f; // Smooth fade width in degrees (same delta in K)

    public float getCurve(float temp)
    {
        // Smooth lethal fade (convert Celsius to Kelvin for comparison)
        float ctMinK = ctMinC + 273.15f;
        float ctMaxK = ctMaxC + 273.15f;
        float halfRange = (ctMaxK - ctMinK) / 2f;
        float tw = Mathf.Min(LETHAL_TRANSITION_WIDTH, halfRange);

        float fadeFactor = 1f;
        if (temp <= ctMinK)
            fadeFactor = 0f;
        else if (temp < ctMinK + tw)
            fadeFactor = 0.5f * (1f + Mathf.Cos(Mathf.PI * (ctMinK + tw - temp) / tw));

        if (temp >= ctMaxK)
            fadeFactor = 0f;
        else if (temp > ctMaxK - tw)
            fadeFactor *= 0.5f * (1f + Mathf.Cos(Mathf.PI * (temp - (ctMaxK - tw)) / tw));

        if (fadeFactor <= 0f) return 0f;

        float performance = (Mathf.Exp(arrhenBreadth / optimalTemp - arrhenBreadth / temp) *
                (1 + Mathf.Exp(arrhenLower / optimalTemp - arrhenLower / lowerBound) +
                    Mathf.Exp(arrhenUpper / upperBound - arrhenUpper / optimalTemp))) /
                (1 + Mathf.Exp(arrhenLower / temp - arrhenLower / lowerBound) +
                    Mathf.Exp(arrhenUpper / upperBound - arrhenUpper / temp));

        return performance * fadeFactor * pmax;
    }

    public float Curves(float temp)
    {
        // Smooth lethal fade (convert Celsius to Kelvin for comparison)
        float ctMinK = ctMinC + 273.15f;
        float ctMaxK = ctMaxC + 273.15f;
        float halfRange = (ctMaxK - ctMinK) / 2f;
        float tw = Mathf.Min(LETHAL_TRANSITION_WIDTH, halfRange);

        float fadeFactor = 1f;
        if (temp <= ctMinK)
            fadeFactor = 0f;
        else if (temp < ctMinK + tw)
            fadeFactor = 0.5f * (1f + Mathf.Cos(Mathf.PI * (ctMinK + tw - temp) / tw));

        if (temp >= ctMaxK)
            fadeFactor = 0f;
        else if (temp > ctMaxK - tw)
            fadeFactor *= 0.5f * (1f + Mathf.Cos(Mathf.PI * (temp - (ctMaxK - tw)) / tw));

        if (fadeFactor <= 0f) return 0f;

        float performance = (Mathf.Exp(arrhenBreadth / optimalTemp - arrhenBreadth / temp) *
                (1 + Mathf.Exp(arrhenLower / optimalTemp - arrhenLower / lowerBound) +
                    Mathf.Exp(arrhenUpper / upperBound - arrhenUpper / optimalTemp))) /
                (1 + Mathf.Exp(arrhenLower / temp - arrhenLower / lowerBound) +
                    Mathf.Exp(arrhenUpper / upperBound - arrhenUpper / temp));

        if(performance > 1) {
           performance = 1;
        }
        return performance * fadeFactor * pmax;
    }

    void OnDrawGizmos()
    {
        for (int i = 0; i < 40; i++)
        {
            Gizmos.DrawLine(transform.position + new Vector3(i, getCurve(i + 273) * 40),
                transform.position + new Vector3(i + 1, (getCurve(i + 273 + 1) * 40) / 2));
        }
    }
}
