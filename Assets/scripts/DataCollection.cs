using UnityEngine;
using System.Collections;
using System.IO;
using System.Linq;
using System;
using System.Text;
using System.Reflection;

public class DataCollection : MonoBehaviour
{
    [SerializeField]
    Shop shop_;

    PlaySessionData collect;
    StringBuilder csv = new StringBuilder();

    string collect_path;
    bool collecting; //are we collecting data this session or not

    private void Start()
    {
#if UNITY_WEBGL
        Debug.Log("webgl dc");
        return; //no need to do anything here if this is a webgl version
#endif
        collect = new PlaySessionData();

        collect_path = Application.persistentDataPath + "/playSessionData/" + System.DateTime.Now.ToString("yyyy-MM-dd;HH-mm-ss") +  ".csv";

        collecting = GetCollect("CollectData");

        if(collecting)
        {
            collect.lastTurn_money = 0;
            collect.total_buy = 0;
            collect.total_sell = 0;

            collect.species = new System.Collections.Generic.Dictionary<string, float>();

            write_csv(true);
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

            collect.total_buy = shop_.buy_press;
            collect.total_sell = shop_.sell_press;

            foreach(CharacterManager c in shop_.playerObj.species)
            {
                collect.species.Add(c.uniqueName, c.speciesAmount);
            }
            write_csv(false);
        }

        //reset these var so they can restart count for new turn
        collect.species.Clear();
        shop_.buy_press = 0;
        shop_.sell_press = 0;
        return;
    }

    void write_csv(bool header)
    {
        string line = "";

        if(header) //file out the header for the log file
        {
            System.IO.Directory.CreateDirectory(Application.persistentDataPath + "/playSessionData");

            Type t = typeof(PlaySessionData);
            MethodInfo[] collectmethod = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            foreach(MethodInfo info in collectmethod)
            {
                string name = info.Name;
                if (info.Name.StartsWith("set_"))
                    continue;

                if (info.Name.StartsWith("get_"))
                {
                    name = info.Name.Substring(info.Name.IndexOf('_')+1, info.Name.Length - (info.Name.IndexOf('_') + 1));
                    if (name == "lastTurn_money")
                        continue;
                }
                
                line += name + ',';
            }
            foreach (CharacterManager c in shop_.playerObj.species)
                line += c.uniqueName + ',';

            csv.AppendLine(line);
            return;
        }

        line += collect.day.ToString() + ',' + collect.temp.ToString();
        line += ',' + collect.current_money.ToString() + ',' + collect.profitLoss().ToString() + ',' + collect.total_buy.ToString() + ',' + collect.total_sell.ToString();
        line += ',' + collect.tier1().ToString() + ',' + collect.tier2().ToString() + ',' + collect.tier3().ToString();
        line += ',' + collect.tier1_arctic().ToString() + ',' + collect.tier1_mild().ToString() + ',' + collect.tier1_tropical().ToString();
        line += ',' + collect.tier2_arctic().ToString() + ',' + collect.tier2_mild().ToString() + ',' + collect.tier2_tropical().ToString();
        line += ',' + collect.tier3_arctic().ToString() + ',' + collect.tier3_mild().ToString() + ',' + collect.tier3_tropical().ToString();

        foreach (CharacterManager c in shop_.playerObj.species)
            line += ',' + collect.species[c.uniqueName].ToString();

        csv.AppendLine(line);
        File.WriteAllText(collect_path, csv.ToString());

        Debug.Log("data log updated");
    }
}
