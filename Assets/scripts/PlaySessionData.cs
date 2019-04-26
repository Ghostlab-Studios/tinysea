using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlaySessionData
{
    //data stored per turn i.e each time next turn is pressed
    public int day { get; set; }

    public float temp { get; set; } //temperature per turn
    public float current_money { get; set; }
    public float lastTurn_money { get; set; }

    //money profit/loss
    public float profitLoss()
    {
        return current_money - lastTurn_money;
    }

    //total number of times bought and sold fish i.e clicked on the buttons buy and sell
    public int total_buy { get; set; }
    public int total_sell { get; set; }

#region //total fish per each tier
    public float tier1()
    {
        return tier1_arctic() + tier1_mild() + tier1_tropical();
    }

    public float tier2()
    {
        return tier2_arctic() + tier2_mild() + tier2_tropical();
    }

    public float tier3()
    {
        return tier3_arctic() + tier3_mild() + tier3_tropical();
    }
#endregion
    
#region    //total fish per tier1 type
    public float tier1_arctic()
    {
        return species["cyplo arctic"] + species["hexapod arctic"] + species["yelloo arctic"];
    }

    public float tier1_mild()
    {
        return species["cyplo"] + species["hexapod"] + species["yelloo"];
    }

    public float tier1_tropical()
    {
        return species["cyplo tropical"] + species["hexapod tropical"] + species["yelloo tropical"];
    }
#endregion

#region//total fish per tier2 type
    public float tier2_arctic()
    {
        return species["shelpik arctic"] + species["grabbler arctic"] + species["gelgi arctic"];
    }

    public float tier2_mild()
    {
        return species["shelpik"] + species["grabbler"] + species["gelgi"];
    }

    public float tier2_tropical()
    {
        return species["shelpik tropical"] + species["grabbler tropical"] + species["gelgi tropical"];
    }
#endregion

#region//total fish per tier3 type
    public float tier3_arctic()
    {
        return species["sploof arctic"] + species["rooda arctic"] + species["silu arctic"];
    }

    public float tier3_mild()
    {
        return species["sploof"] + species["rooda"] + species["silu"];
    }

    public float tier3_tropical()
    {
        return species["sploof tropical"] + species["rooda tropical"] + species["silu tropical"];
    }
#endregion

#region //total fish individual
    public Dictionary<string, float> species;
#endregion
}
