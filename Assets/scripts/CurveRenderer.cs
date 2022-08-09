using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CurveRenderer : MonoBehaviour {

    public ThermalCurve curve;

    private Gradient neutralGradient;
    private Gradient tropicalGradient;
    private Gradient arcticGradient;

	// Use this for initialization
	void Start () {
        Color curveRed = new Color32(178, 35, 35, 1);
        Color curveYellow = new Color32(213, 216, 30, 1);
        Color curveGreen = new Color32(64, 217, 36, 1);
        float alpha = 1.0f;

        neutralGradient = new Gradient();
        neutralGradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(curveRed, 0.0f),
                                     new GradientColorKey(curveYellow, 0.25f),
                                     new GradientColorKey(curveGreen, 0.5f),
                                     new GradientColorKey(curveYellow, 0.75f),
			                         new GradientColorKey(curveRed, 1.0f) },
	        new GradientAlphaKey[] { new GradientAlphaKey(alpha, 0.0f),
                                     new GradientAlphaKey(alpha, 1.0f) });

        tropicalGradient = new Gradient();
        tropicalGradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(curveRed, 0.0f),
                                     new GradientColorKey(curveYellow, 0.5f),
                                     new GradientColorKey(curveGreen, 0.92f),
                                     new GradientColorKey(curveYellow, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(alpha, 0.0f),
                                     new GradientAlphaKey(alpha, 1.0f) });

        arcticGradient = new Gradient();
        arcticGradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(curveYellow, 0.0f),
                                     new GradientColorKey(curveGreen, 0.08f),
                                     new GradientColorKey(curveYellow, 0.5f),
                                     new GradientColorKey(curveRed, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(alpha, 0.0f),
                                     new GradientAlphaKey(alpha, 1.0f) });

        updateChart();
    }

    public void UpdateCurve(ThermalCurve curve, int variant)
    {
        switch (variant)
        {
            case 0:
                GetComponent<LineRenderer>().colorGradient = neutralGradient;
                break;
            case 1:
                GetComponent<LineRenderer>().colorGradient = tropicalGradient;
                break;
            case 2:
                GetComponent<LineRenderer>().colorGradient = arcticGradient;
                break;
        }

        this.curve = curve;
        updateChart();
    }

    public void updateChart()
    {
        Vector3 pos = transform.position;
        LineRenderer line = GetComponent<LineRenderer>();
        //line.SetVertexCount(41);
        line.positionCount = 41;
        for (int i = 0; i <= 40; i++)
        {
            line.SetPosition(i, new Vector3(i, curve.getCurve(i + 273) * 15));
            //line.SetPosition(i, new Vector3(i, curve.Curves(i + 273) * 40));
            
        }
    }
}
