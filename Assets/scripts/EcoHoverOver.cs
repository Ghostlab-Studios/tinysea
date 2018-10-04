using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EcoHoverOver : MonoBehaviour {

    public List<CharacterManager> managers;
    public Text creatureText;
    public Text creatureText1;
    public Text creatureText2;
    public Text creatureText3;
    public Text creatureText4;
    public Text creatureText5;
    public Text creatureText6;
    public Text creatureText7;
    public Text creatureText8;

    private void Update()
    {
        UpdateCreatureText(0);
        UpdateCreatureText(1);
        UpdateCreatureText(2);
        UpdateCreatureText(3);
        UpdateCreatureText(4);
        UpdateCreatureText(5);
        UpdateCreatureText(6);
        UpdateCreatureText(7);
        UpdateCreatureText(8);
    }

    private void UpdateCreatureText(int index)
    {
        CharacterManager c = managers[index];
        
        switch (index)
        {
            case 0:
                creatureText.text = c.speciesAmount.ToString();
                break;
            case 1:
                creatureText1.text = c.speciesAmount.ToString();
                break;
            case 2:
                creatureText2.text = c.speciesAmount.ToString();
                break;
            case 3:
                creatureText3.text = c.speciesAmount.ToString();
                break;
            case 4:
                creatureText4.text = c.speciesAmount.ToString();
                break;
            case 5:
                creatureText5.text = c.speciesAmount.ToString();
                break;
            case 6:
                creatureText6.text = c.speciesAmount.ToString();
                break;
            case 7:
                creatureText7.text = c.speciesAmount.ToString();
                break;
            default:
                creatureText8.text = c.speciesAmount.ToString();
                break;
        }
    }
}
