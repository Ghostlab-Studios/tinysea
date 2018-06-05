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

	// Use this for initialization
	void Start () {

	}

    public float getCurve(float temp)
    {
        //float oT = optimalTemp + 275.15f;

        float performance = Mathf.Exp(arrhenBreadth / optimalTemp - arrhenBreadth / temp) *
                (1 + Mathf.Exp(arrhenLower / optimalTemp - arrhenLower / lowerBound) +
                    Mathf.Exp(arrhenUpper / upperBound - arrhenUpper / optimalTemp)) /
                (1 + Mathf.Exp(arrhenLower / temp - arrhenLower / lowerBound) +
                    Mathf.Exp(arrhenUpper / upperBound - arrhenUpper / temp));
        /*if(performance > 1) {
            performance = 1;
        }*/

        return performance;
    }
	
	// Update is called once per frame
	void Update () {
	    
	}

    void OnDrawGizmos()
    {
        for (int i = 0; i < 40; i++)
        {
            Gizmos.DrawLine(transform.position + new Vector3(i, getCurve(i + 273) * 40),
                transform.position + new Vector3(i + 1, getCurve(i + 273 + 1) * 40));
        }
    }
}
