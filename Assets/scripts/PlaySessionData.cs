using UnityEngine;
using System.Collections;

public class PlaySessionData
{
    //data stored per turn i.e each time next turn is pressed
    int turn;

    float temp; //temperature per turn
    float current_money, lastTurn_money;
    
    //money profit/loss
    float money_PL()
    {
        return current_money - lastTurn_money;
    }

    //total number of times bought and sold fish i.e clicked on the buttons buy and sell
    int total_buy, total_sell;

#region//total buys
    //fish bought tier 1
    float buy_cyplo_arctic, buy_hexapod_arctic, buy_yellow_arctic;
    float buy_cyplo, buy_hexapod, buy_yellow;
    float buy_cyplo_tropic, buy_hexapod_tropic, buy_yellow_tropic;

    //fish bought tier 2
    float buy_shelpik_arctic, buy_grabbler_arctic, buy_gelgi_arctic;
    float buy_shelpik, buy_grabbler, buy_gelgi;
    float buy_shelpik_tropic, buy_grabbler_tropic, buy_gelgi_tropic;

    //fish bought tier 3
    float buy_sploof_arctic, buy_rooda_arctic, buy_silu_artic;
    float buy_sploof, buy_rooda, buy_silu;
    float buy_sploof_tropic, buy_rooda_tropic, buy_silu_tropic;
    #endregion

#region //total sells
    //fish sold tier 1
    float sell_cyplo_arctic, sell_hexapod_arctic, sell_yellow_arctic;
    float sell_cyplo, sell_hexapod, sell_yellow;
    float sell_cyplo_tropic, sell_hexapod_tropic, sell_yellow_tropic;

    //fish sold tier 2
    float sell_shelpik_arctic, sell_grabbler_arctic, sell_gelgi_arctic;
    float sell_shelpik, sell_grabbler, sell_gelgi;
    float sell_shelpik_tropic, sell_grabbler_tropic, sell_gelgi_tropic;

    //fish sold tier 3
    float sell_sploof_arctic, sell_rooda_arctic, sell_silu_artic;
    float sell_sploof, sell_rooda, sell_silu;
    float sell_sploof_tropic, sell_rooda_tropic, sell_silu_tropic;
    #endregion

#region //total fish individual
    //total number of individual tier1 fish
    float cyplo_arctic, hexapod_arctic, yellow_arctic;
    float cyplo, hexapod, yellow;
    float cyplo_tropic, hexapod_tropic, yellow_tropic;

    //total number of individual tier2 fish
    float shelpik_arctic, grabbler_arctic, gelgi_arctic;
    float shelpik, grabbler, gelgi;
    float shelpik_tropic, grabbler_tropic, gelgi_tropic;

    //total number of individual tier3 fish
    float sploof_arctic, rooda_arctic, silu_artic;
    float sploof, rooda, silu;
    float sploof_tropic, rooda_tropic, silu_tropic;
#endregion

#region    //total fish per tier1 type
    float tier1_artic()
    {
        return cyplo_arctic + hexapod_arctic + yellow_arctic;
    }

    float tier1_mild()
    {
        return cyplo + hexapod + yellow;
    }

    float tier1_tropic()
    {
        return cyplo_tropic + hexapod_tropic + yellow_tropic;
    }
#endregion

#region//total fish per tier2 type
    float tier2_arctic()
    {
        return shelpik_arctic + grabbler_arctic + gelgi_arctic;
    }

    float tier2_mild()
    {
        return shelpik + grabbler + gelgi;
    }

    float tier2_tropic()
    {
        return shelpik_tropic + grabbler_tropic + gelgi;
    }
#endregion

#region//total fish per tier3 type
    float tier3_arctic()
    {
        return sploof_arctic + rooda_arctic + silu_artic;
    }

    float tier3_mild()
    {
        return sploof + rooda + silu;
    }

    float tier3_tropic()
    {
        return sploof_tropic + rooda_tropic + silu_tropic;
    }
#endregion

#region //total fish per each tier
    float tier1()
    {
        return tier1_artic() + tier1_mild() + tier1_tropic();
    }

    float tier2()
    {
        return tier2_arctic() + tier2_mild() + tier2_tropic();
    }

    float tier3()
    {
        return tier3_arctic() + tier3_mild() + tier3_tropic();
    }
#endregion 
}
