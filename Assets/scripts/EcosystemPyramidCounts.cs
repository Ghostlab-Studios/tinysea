using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EcosystemPyramidCounts : MonoBehaviour {
    public List<CharacterManager> tier1Organisms;
    public List<CharacterManager> tier2Organisms;
    public List<CharacterManager> tier3Organisms;
    public Text tier1Text;
    public Text tier2Text;
    public Text tier3Text;
	
	void Update () {
        UpdateText(tier1Text, tier1Organisms);
        UpdateText(tier2Text, tier2Organisms);
        UpdateText(tier3Text, tier3Organisms);
    }

    void UpdateText(Text text, List<CharacterManager> organisms)
    {
        int count = 0;
        foreach (CharacterManager organism in organisms)
        {
            count += (int)organism.speciesAmount;
        }
        text.text = count.ToString();
    }
}
