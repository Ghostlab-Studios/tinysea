using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class load : MonoBehaviour
{
    public GameObject collect_toggle;
    public Dropdown load_game;
    public PlayerManager playerObj;
    Dropdown.OptionData[] load_data;
    string save_path;
    private void Awake()
    {
//can't load this way in webgl mode so turn it off
#if UNITY_WEBGL
        load_game.gameObject.SetActive(false);
        collect_toggle.SetActive(false);
#else
        if (load_game != null)
        {
            load_game.ClearOptions();
            load_data = new Dropdown.OptionData[4];
            load_data[0] = new Dropdown.OptionData();
            load_data[0].text = "New Game";
            load_game.options.Add(load_data[0]);
            load_game.GetComponentInChildren<Text>().text = load_data[0].text;
            for (int i = 1; i < 4; i++)
            {
                save_path = Application.persistentDataPath + "/Saves/Save_" + i + ".txt";
                if (File.Exists(save_path))
                {
                    load_data[i] = new Dropdown.OptionData();
                    load_data[i].text = "Save " + i;
                    load_game.options.Add(load_data[i]);
                }
            }
        }
#endif
    }

    private void Start()
    {
        if (playerObj != null)
        {
            load_thisGame();
        }
    }

    public void load_info()
    {
        if (load_game.GetComponentInChildren<Text>().text.Contains("Save"))
        {
            string i = load_game.GetComponentInChildren<Text>().text.Remove(0, 5);
            PlayerPrefs.SetInt("save", int.Parse(i));
        }
        else
            PlayerPrefs.SetInt("save", 0);
    }

    void load_thisGame()
    {
        int load = PlayerPrefs.GetInt("save");

        if (load == 0)
            return;

        string save_file = Application.persistentDataPath + "/Saves/Save_" + load + ".txt";

        string[] file_loaded = File.ReadAllLines(save_file);

        for(int i = 1; i<file_loaded.Length; i++)
        {
            if (i == 1)
            {
                int index = file_loaded[i].IndexOf(" ");
                int temp = int.Parse(file_loaded[i].Substring(0, index));
                playerObj.temperature.temperature = temp;
            }
            else if(i == 2)
            {
                int index = file_loaded[i].IndexOf(" ");
                int money = int.Parse(file_loaded[i].Substring(0, index));
                playerObj.moneys = money;
            }
            else
            {
                int index = file_loaded[i].IndexOf(" ");
                int fish = int.Parse(file_loaded[i].Substring(0, index));
                playerObj.BuyCreatures((i - 3), fish, false);
            }
        }
    }

    public void SetCollect(bool collect)
    {
        if (collect)
            PlayerPrefs.SetInt("CollectData", 1);
        else
            PlayerPrefs.SetInt("CollectData", 0);
    }

    public void hover_on(GameObject hov)
    {
        hov.GetComponent<Image>().color = Color.black;
    }

    public void hover_off(GameObject hov)
    {
        hov.GetComponent<Image>().color = Color.white;
    }
}
