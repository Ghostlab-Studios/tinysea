using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class creatureGraph : MonoBehaviour
{
    public GameObject tier1, tier2, tier3;
    public List<CharacterManager> manager_T1, manager_T2, manager_T3;
    public Color[] pieColors;
    public GameObject[] tierCreatures, seas;
    float[] values, value, check_v;
    Image[] pies;
    int current_tier;

    // Use this for initialization
    private void Awake()
    {
        pies = this.GetComponentsInChildren<Image>();
        current_tier = 1;
        value = new float[] {0,0,0};
        check_v = new float[] {0,0,0};
    }

    private void Update()
    {
        tier_updates(current_tier);

        if (value[0] != check_v[0] || value[1] != check_v[1] || value[2] != check_v[2])
        {
            values = new float[] { value[0], value[1], value[2] };
            makeGraph();
        }
    }

    public void control_tabs(GameObject tier)
    {
        tier1.SetActive(false);
        tier2.SetActive(false);
        tier3.SetActive(false);

        if(tier)
        {
            current_tier = int.Parse(tier.name);
            tier.SetActive(true);

            tierCreatures[0].GetComponent<EcoHoverOver>().update_tier(current_tier);
            tierCreatures[1].GetComponent<EcoHoverOver>().update_tier(current_tier);
            tierCreatures[2].GetComponent<EcoHoverOver>().update_tier(current_tier);
        }
    }

    void tier_updates(int tier)
    {
        int i = 0;
        check_v[0] = value[0];
        check_v[1] = value[1];
        check_v[2] = value[2];

        switch(tier)
        {
            case 1:
                value[0] = Mathf.FloorToInt(manager_T1[0].speciesAmount) + Mathf.FloorToInt(manager_T1[1].speciesAmount) + Mathf.FloorToInt(manager_T1[2].speciesAmount);
                value[1] = Mathf.FloorToInt(manager_T1[3].speciesAmount) + Mathf.FloorToInt(manager_T1[4].speciesAmount) + Mathf.FloorToInt(manager_T1[5].speciesAmount);
                value[2] = Mathf.FloorToInt(manager_T1[6].speciesAmount) + Mathf.FloorToInt(manager_T1[7].speciesAmount) + Mathf.FloorToInt(manager_T1[8].speciesAmount);
                break;
            case 2:
                value[0] = Mathf.FloorToInt(manager_T2[0].speciesAmount) + Mathf.FloorToInt(manager_T2[1].speciesAmount) + Mathf.FloorToInt(manager_T2[2].speciesAmount);
                value[1] = Mathf.FloorToInt(manager_T2[3].speciesAmount) + Mathf.FloorToInt(manager_T2[4].speciesAmount) + Mathf.FloorToInt(manager_T2[5].speciesAmount);
                value[2] = Mathf.FloorToInt(manager_T2[6].speciesAmount) + Mathf.FloorToInt(manager_T2[7].speciesAmount) + Mathf.FloorToInt(manager_T2[8].speciesAmount);
                break;
            case 3:
                value[0] = Mathf.FloorToInt(manager_T3[0].speciesAmount) + Mathf.FloorToInt(manager_T3[1].speciesAmount) + Mathf.FloorToInt(manager_T3[2].speciesAmount);
                value[1] = Mathf.FloorToInt(manager_T3[3].speciesAmount) + Mathf.FloorToInt(manager_T3[4].speciesAmount) + Mathf.FloorToInt(manager_T3[5].speciesAmount);
                value[2] = Mathf.FloorToInt(manager_T3[6].speciesAmount) + Mathf.FloorToInt(manager_T3[7].speciesAmount) + Mathf.FloorToInt(manager_T3[8].speciesAmount);
                break;
            default:
                Debug.Log("wrong tier info");
                break;
        }
    }

    void makeGraph()
    {
        float total = 0f;
        float zRot = 0f;

        for (int i = 0; i<values.Length; i++)
        {
            total += values[i];
        }

        for(int i = 0; i<values.Length; i++)
        {
            Image newPie = pies[i];

            if (values[i] == 0)
            {
                newPie.GetComponent<Image>().color = Color.clear;
                seas[i].GetComponent<Image>().enabled = false;
            }
            else
            {
                newPie.GetComponent<Image>().color = pieColors[i];
                newPie.GetComponent<Image>().fillAmount = values[i] / total;
                newPie.transform.rotation = Quaternion.Euler(new Vector3(0f, 0f, zRot));
                zRot -= newPie.GetComponent<Image>().fillAmount * 360f;

                float center_fill = (newPie.GetComponent<Image>().fillAmount*360f)/2;
                seas[i].GetComponent<Image>().enabled = true;
                seas[i].transform.rotation = Quaternion.Euler(new Vector3(0f, 0f, (zRot + center_fill)));
            }
        }
    }
}
