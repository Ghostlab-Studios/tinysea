using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EcoHoverOver : MonoBehaviour {

    public List<CharacterManager> managers;
    public float creatures;
    public Text creatureText;

    private void Update()
    {
        float temp = 0;
        foreach (CharacterManager c in managers)
        {
            temp += c.speciesAmount;
        }
        creatures = temp;
        creatureText.text = creatures.ToString();
        temp = 0;
    }
}
