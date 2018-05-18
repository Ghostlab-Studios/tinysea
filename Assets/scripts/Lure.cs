using UnityEngine;
using System.Collections.Generic;

//[ExecuteInEditMode]
public class Lure : MonoBehaviour {

    public Vector3 lineOffset = Vector3.zero;
    public Vector3 lineOrigin = new Vector3(0, 50, 0);
    public float xRandomness = 50;

    LineRenderer line;
    List<SwimmingCreature> fishes;
    Vector3 origin = Vector3.zero;
    Vector3 target = Vector3.zero;

	// Use this for initialization
	void Start () {
        fishes = new List<SwimmingCreature>();
        Reset();
	}

    public void Reset()
    {
        line = GetComponent<LineRenderer>();
        origin = lineOrigin + new Vector3(Random.Range(-xRandomness, xRandomness), 0, transform.position.z);
        transform.position = origin;
        line.SetPosition(0, transform.position + lineOffset + new Vector3(0, 0, 1));
        line.SetPosition(1, origin + lineOffset);
    }

    public void addTarget(SwimmingCreature c) {
        fishes.Add(c);
        if (fishes.Count == 1)
            target = c.transform.position;
    }

    public bool amITheTarget(SwimmingCreature c)
    {
        if (fishes.Count > 0)
        {
            return c == fishes[0];
        }
        return false;
    }
	
	// Update is called once per frame
	void Update () {
        if (fishes.Count > 0)
        {
            SwimmingCreature t = fishes[0];
            if (t == null || !t.gameObject.activeSelf)
            {
                fishes.Remove(t);
                Reset();
                if (fishes.Count > 0)
                {
                    target = fishes[0].transform.position;
                    //fish faster if we've got a lot of fish we need to get through
                    if (fishes.Count > 1)
                    {
                        float timeSquish = 1 / Mathf.Sqrt(fishes.Count);
                        timeSquish = Mathf.Max(0.1f, timeSquish);
                        fishes[0].multDeathTime(timeSquish);
                    }
                }
            }
            else if (t.getDeathRatio() <= .5f)
            {
                //pull up the fish
                float amount1 = 1 - t.getDeathRatio() * 2;
                transform.position = Vector3.Lerp(target, origin, amount1);
                //Vector3 lookDir = origin - target;
                //transform.rotation = Quaternion.LookRotation(new Vector3(lookDir.x, lookDir.y, 0).normalized);
                line.SetPosition(1, Vector3.Lerp(
                    new Vector3(target.x, origin.y, origin.z),
                    new Vector3(origin.x * 2, origin.y, origin.z), amount1) + lineOffset); 
            }
            else
            {
                target = fishes[0].transform.position;
                //go down to the fish
                float amount2 = 1 - (t.getDeathRatio() - .5f) * 2;
                transform.position = Vector3.Lerp(
                    new Vector3(target.x, origin.y, origin.z),
                    target, 
                    amount2);
                //transform.rotation = Quaternion.Euler(Vector3.zero);
                line.SetPosition(1, new Vector3(target.x, origin.y, origin.z) + lineOffset);
            }
            line.SetPosition(0, transform.position + lineOffset + new Vector3(0, 0, 1));
        }
	}
}
