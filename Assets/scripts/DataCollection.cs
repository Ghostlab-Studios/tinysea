using UnityEngine;
using System.Collections;

public class DataCollection : MonoBehaviour
{
    PlaySessionData collect;

    string collect_path;
    bool collecting; //are we collecting data this session or not

    private void Start()
    {
        collect_path = Application.persistentDataPath + "/playSessionData/" + System.DateTime.Now +  ".csv";

        collecting = GetBool("CollectData");
    }

    private static bool GetBool(string key)
    {
        return PlayerPrefs.GetInt(key) == 1;
    }

    public void collect_data()
    {
        if(collecting)
        {

        }
        return;
    }

    void write_csv()
    {

    }

}
