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
    public float ctMaxC = 50.0f;

    public float getCurve(float temp)
    {
        // Lethal limits (convert Celsius to Kelvin for comparison)
        float ctMinK = ctMinC + 273.15f;
        float ctMaxK = ctMaxC + 273.15f;
        if (temp < ctMinK || temp > ctMaxK)
            return 0f;

        float performance = (Mathf.Exp(arrhenBreadth / optimalTemp - arrhenBreadth / temp) *
                (1 + Mathf.Exp(arrhenLower / optimalTemp - arrhenLower / lowerBound) +
                    Mathf.Exp(arrhenUpper / upperBound - arrhenUpper / optimalTemp))) /
                (1 + Mathf.Exp(arrhenLower / temp - arrhenLower / lowerBound) +
                    Mathf.Exp(arrhenUpper / upperBound - arrhenUpper / temp));

        return performance * pmax;
    }

    public float Curves(float temp)
    {
        // Lethal limits (convert Celsius to Kelvin for comparison)
        float ctMinK = ctMinC + 273.15f;
        float ctMaxK = ctMaxC + 273.15f;
        if (temp < ctMinK || temp > ctMaxK)
            return 0f;

        float performance = (Mathf.Exp(arrhenBreadth / optimalTemp - arrhenBreadth / temp) *
                (1 + Mathf.Exp(arrhenLower / optimalTemp - arrhenLower / lowerBound) +
                    Mathf.Exp(arrhenUpper / upperBound - arrhenUpper / optimalTemp))) /
                (1 + Mathf.Exp(arrhenLower / temp - arrhenLower / lowerBound) +
                    Mathf.Exp(arrhenUpper / upperBound - arrhenUpper / temp));

        if(performance > 1) {
           performance = 1;
        }
        return performance * pmax;
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
