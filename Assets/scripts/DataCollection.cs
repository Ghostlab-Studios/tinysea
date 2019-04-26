using UnityEngine;
using System.Collections;

public class DataCollection : MonoBehaviour
{
    [SerializeField]
    Shop shop_;

    PlaySessionData collect;


    string collect_path;
    bool collecting; //are we collecting data this session or not

    private void Start()
    {
        collect = new PlaySessionData();

        collect_path = Application.persistentDataPath + "/playSessionData/" + System.DateTime.Now +  ".csv";

        collecting = GetCollect("CollectData");

        if(collecting)
        {
            collect.lastTurn_money = 0;
            collect.total_buy = 0;
            collect.total_sell = 0;

            collect.species = new System.Collections.Generic.Dictionary<string, float>();
        }
    }

    private static bool GetCollect(string key)
    {
        return PlayerPrefs.GetInt(key) == 1;
    }

    public void collect_data()
    {
        if(collecting)
        {
            collect.day = (int)shop_.playerObj.temperature.currentDay;
            collect.temp = shop_.playerObj.temperature.temperature;
            collect.lastTurn_money = collect.current_money;
            collect.current_money = shop_.playerObj.moneys;

            foreach(CharacterManager c in shop_.playerObj.species)
            {
                collect.species.Add(c.uniqueName, c.speciesAmount);
            }
            write_csv();
        }
        return;
    }

    void write_csv()
    {

    }

}
