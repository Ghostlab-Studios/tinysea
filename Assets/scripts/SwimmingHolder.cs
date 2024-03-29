﻿using UnityEngine;
using System.Collections.Generic;

public class SwimmingHolder : MonoBehaviour {
    public PlayerManager player;
    public List<SwimmingCreature> creatures;
    public List<GameObject> prefabs;
    public List<Lure> lures;
    //number of swimming creatures for each speices. make sure to update this
    public List<int> speciesNumbers;
    public float predatorTime = 1;

	// Use this for initialization
	void Start () {
	    creatures = new List<SwimmingCreature>();
        speciesNumbers = new List<int>();
        foreach(CharacterManager c in player.species) {
            speciesNumbers.Add(0);
        }
	}
	
	// Update is called once per frame
	void Update () {
        for (int i = 0; i < player.species.Count; i++)
        {
            int speciesAmount = Mathf.FloorToInt(player.species[i].speciesAmount);
            if (speciesAmount != speciesNumbers[i])
            {
                //birth new creatures
                if (speciesAmount > speciesNumbers[i])
                {
                    while (speciesNumbers[i] < speciesAmount)
                    {
                        //birth the creature using the right effect (bought or reproduced)
                        if (player.species[i].birthList.Count == 0)
                            AddCreature(i, CharacterManager.BirthCause.Reproduction);
                        else
                            AddCreature(i, player.species[i].birthList.Dequeue());
                        speciesNumbers[i]++;
                    }
                }
                else if (speciesAmount < speciesNumbers[i])
                {
                    //figure out the cause of death
                    CharacterManager man = player.species[i];

                    //remove them
                    while (speciesNumbers[i] > speciesAmount)
                    {
                        if (man.deathList.Count == 0)
                            RemoveCreature(i, CharacterManager.DeathCause.Starve);
                        else
                            //Debug.Log(man.deathList.Peek());
                            RemoveCreature(i, man.deathList.Dequeue());
                        speciesNumbers[i]--;
                    }
                }
            }
            //clear the queues to correct for any rounding errors
            player.species[i].deathList.Clear();
            player.species[i].birthList.Clear();
        }
	}

    void AddCreature(int cId, CharacterManager.BirthCause cause)
    {
        //if we're running in the editor, we can instantiate as prefabs. 
        //This lets us change variables in the prefab and see the effects
#if UNITY_EDITOR
        GameObject cObj = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefabs[cId]);
#else
        GameObject cObj = GameObject.Instantiate(prefabs[cId]);
#endif
        SwimmingCreature c = cObj.GetComponent<SwimmingCreature>();
        c.creatureFlock = creatures;
        c.id = cId;
        //move it out of the way so that it doesn't flicker before spawning.
        c.transform.position = c.transform.position + new Vector3(99999, 9999, 0);
        Vector3 fallbackPos = new Vector3(-10, -5, 0);
        switch (cause)
        {
            case CharacterManager.BirthCause.Bought:
                c.StartBuying();
                break;
            case CharacterManager.BirthCause.Reproduction:
                if (CreatureWithIDExists(cId))
                {
                    c.StartReproducing(player.reproducePart, findRandomCreatureOfID(cId).transform.position);
                    break;
                }
                else
                {
                    Debug.Log("Fallback pos called");
                    c.StartReproducing(player.reproducePart, fallbackPos);
                    break;
                }
        }
        creatures.Add(c);
    }

    public bool anyCreaturesBusy()
    {
        foreach (SwimmingCreature c in creatures)
        {
            if (c.isBusy)
            {
                return true;
            }
        }
        return false;
    }

    SwimmingCreature findRandomCreatureOfID(int cID)
    {
        List<SwimmingCreature> filteredC = new List<SwimmingCreature>();
        foreach(SwimmingCreature c in creatures) {
            if (c.id == cID && !c.isDying)
            {
                filteredC.Add(c);
            }
        }

            int randIndex = Random.Range(0, filteredC.Count);
            try
            {
                return (filteredC[randIndex]);
            }
            catch (System.ArgumentOutOfRangeException a)
            {
                Debug.Log(randIndex);
                bool hasFish = (filteredC.Count == 0);
                Debug.Log(hasFish);
                Debug.Log(a);
                //return filteredC[randIndex];
                return null;

            }
        //return (filteredC[randIndex]);
    }

    SwimmingCreature findRandomCreatureOfTier(int tier)
    {
        List<SwimmingCreature> filteredC = new List<SwimmingCreature>();
        foreach (SwimmingCreature c in creatures)
        {
            if (c.level == tier && !c.isDying)
            {
                filteredC.Add(c);
            }
        }
        int randIndex = Random.Range(0, filteredC.Count);
        try
        {
            return (filteredC[randIndex]);
        }
        catch (System.ArgumentOutOfRangeException a)
        {
            Debug.Log(randIndex);
            bool hasFish = (filteredC.Count == 0);
            Debug.Log(hasFish);
            Debug.Log(a);
            return filteredC[randIndex];
        }
    }

    SwimmingCreature findCreatureOfID(int cID)
    {
        int index = 0;
        while (index < creatures.Count)
        {
            if (creatures[index].id == cID && !creatures[index].isDying)
            {
                return (creatures[index]);
            }
            else
            {
                index++;
            }
        }
        return null;
    }

    void RemoveCreature(int cId, CharacterManager.DeathCause cause)
    {
        bool foundOne = false;
        int index = 0;
        int found = -1;
        while (index < creatures.Count && !foundOne)
        {
            if (creatures[index].id == cId && !creatures[index].isDying)
            {
                foundOne = true;
                found = index;
            }
            else
            {
                index++;
            }
        }

        if (foundOne)
        {
            SwimmingCreature c = creatures[found];
            //creatures.Remove(c);
            /*if(cause == null)
                cause = CharacterManager.DeathCause.Starve;*/
            switch(cause) {
                case CharacterManager.DeathCause.Sold:
                    c.startFishing(lures[Random.Range(0, lures.Count)]);
                    break;
                case CharacterManager.DeathCause.Hot:
                    c.startDying(player.tooHotPart);
                    break;
                case CharacterManager.DeathCause.Cold:
                    c.startDying(player.tooCoolPart);
                    break;
                case CharacterManager.DeathCause.Eaten:
                    try
                    {
                        // literally why
                        if (CreatureOfTierExists(c.level + 1))
                        {
                            SwimmingCreature predator = findRandomCreatureOfTier(c.level + 1);
                            float fasterHunting = Mathf.Max(1, predator.huntingFish.Count);
                            FishHunt hunt = new FishHunt(predator, c, predatorTime / fasterHunting);
                            predator.huntingFish.Add(hunt);
                            c.getEaten(hunt);
                            predator.startEating();
                            break;
                        }
                        else
                        {
                            Debug.Log("Predator did not exist");
                            c.startDying(player.starvedPart);
                            break;
                        }
                    } catch (System.ArgumentOutOfRangeException a)
                    {
                        Debug.Log(a);
                        c.startDying(player.starvedPart);
                        break;
                    }
                case CharacterManager.DeathCause.Starve:
                    c.startDying(player.starvedPart);
                    break;
                default:
                    //c.startDying(player.starvedPart);
                    c.startFishing(lures[Random.Range(0, lures.Count)]);
                    break;
            }
        }
    }

    // Preventative check for undesired behavior
    bool CreatureOfTierExists(int tier)
    {
        //Debug.Log("This was called");
        bool exists = false;
        
        while(!exists)
        {
            for (int i = 0; i < creatures.Count; i++)
            {
                if (creatures[i].level == tier)
                {
                    exists = true;
                }
            }
        }
        return exists;
    }

    // Preventative check for undesired behavior
    bool CreatureWithIDExists(int id)
    {
        //Debug.Log("This was called");
        bool exists = false;

        while (!exists)
        {
            for (int i = 0; i < creatures.Count; i++)
            {
                if (creatures[i].id == id)
                {
                    exists = true;
                }
            }
        }
        return exists;
    }
}
