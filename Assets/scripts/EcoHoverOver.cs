using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EcoHoverOver : MonoBehaviour {

    public List<CharacterManager> tier1_managers, tier2_managers, tier3_managers;
    public List<Sprite> tier1_sprites, tier2_sprites, tier3_sprites;
    public Text creatureText;
    public Text creatureText1;
    public Text creatureText2;
    Image[] creatureImages;
    List<CharacterManager> current_manager;

    private void Awake()
    {
        creatureImages = GetComponentsInChildren<Image>();
        current_manager = tier1_managers;
    }

    private void Update()
    {
        UpdateCreatureText(0);
        UpdateCreatureText(1);
        UpdateCreatureText(2);
    }

    public void update_tier(int tierNo)
    {
        switch (tierNo)
        {
            case 1:
                creatureImages = GetComponentsInChildren<Image>();
                current_manager = tier1_managers;

                for (int i = 0; i < 3; i++)
                {
                    creatureImages[i + 1].sprite = tier1_sprites[i];
                }
                break;

            case 2:
                creatureImages = GetComponentsInChildren<Image>();
                current_manager = tier2_managers;

                for (int i = 0; i < 3; i++)
                {
                    creatureImages[i + 1].sprite = tier2_sprites[i];
                }
                break;

            case 3:
                creatureImages = GetComponentsInChildren<Image>();
                current_manager = tier3_managers;

                for (int i = 0; i < 3; i++)
                {
                    creatureImages[i + 1].sprite = tier3_sprites[i];
                }
                break;
            default:
                Debug.Log("updating tier something went wrong");
                break;

        }
    }

    private void UpdateCreatureText(int index)
    {
        CharacterManager c = current_manager[index];
        
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
            default:
                Debug.Log("something went wrong witht the creature chart");
                break;
        }
    }
}
