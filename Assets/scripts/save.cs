using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class save : MonoBehaviour
{
    public GameObject[] save_button, time_text;
    public PlayerManager playerObj;
    public GameObject overWrite;
    string savePath, current;

    public void Awake()
    {

#if UNITY_WEBGL
        //can't save this way in webgl mode so turn it off
#else
        foreach (GameObject save in save_button)
        {
            current = save.name.Remove(0, 5);
            string dir = Application.persistentDataPath + "/Saves/";

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            savePath = dir + save.name + ".txt";

            if (File.Exists(savePath))
            {
                update_save_text();
                update_time();
            }
        }
#endif
    }

    public void click_save(GameObject button)
    {
        current = button.name.Remove(0, 5);
        savePath = Application.persistentDataPath + "/Saves/" + button.name + ".txt";
        save_game();
    }

    public void click_delete(GameObject button)
    {
        current = button.name.Remove(0,7);
        savePath = Application.persistentDataPath + "/Saves/Save_" + current + ".txt";
        delete_save(savePath);
    }

    public void overWrite_confirm(bool confirm)
    {
        write_file(confirm);
        overWrite.SetActive(false);
    }

    private void save_game()
    {
        //if files doesn't exist make it
        if (!File.Exists(savePath))
        {
            write_file(true);
        }
        else
        {
            overWrite.SetActive(true);
        }
    }

    private void write_file(bool writeOver)
    {
        if(!writeOver)
            File.WriteAllText(savePath, "moneyfirst\nfish_no_forward\n");
        else
        {
            /*playerobjs: following stuff to save
             * number of turns = temprature// temperature.temperature or temperature.currentday
             * moneys
             * species and their total number 
             */

            StringBuilder save_file = new StringBuilder();
            save_file.AppendLine(System.DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
            save_file.AppendLine(playerObj.temperature.temperature.ToString() + " " + "temp");
            save_file.AppendLine(playerObj.moneys.ToString() + " " + "money");
            foreach (CharacterManager c in playerObj.species)
            {
                save_file.AppendLine(c.speciesAmount.ToString() + " " + c.uniqueName);
            }

            File.WriteAllText(savePath, save_file.ToString());
            update_save_text();
            update_time();
        }
    }

    private void delete_save(string path)
    {
        File.Delete(path);
        save_button[int.Parse(current) - 1].GetComponentInChildren<Text>().text = "Slot " + current;
        time_text[int.Parse(current) - 1].GetComponent<Text>().text = ".............Empty Slot...........";
    }

    private void update_save_text()
    {
        save_button[int.Parse(current) - 1].GetComponentInChildren<Text>().text = "SAVE " + current;
    }

    private void update_time()
    {
        StreamReader readFile = new StreamReader(savePath);
        time_text[int.Parse(current) - 1].GetComponent<Text>().text = readFile.ReadLine();
        readFile.Close();
    }
}
