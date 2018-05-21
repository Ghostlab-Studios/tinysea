using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CurveRenderer : MonoBehaviour {

    public ThermalCurve curve;

	// Use this for initialization
	void Start () {

    }
	
	// Update is called once per frame
	void Update () {
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
            line.SetPosition(i, new Vector3(i, curve.getCurve(i + 273) * 40));
        }
    }
}
