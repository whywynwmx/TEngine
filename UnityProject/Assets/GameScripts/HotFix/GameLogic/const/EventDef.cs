using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameLogic
{
    public enum EventDef 
    {
        None = 0,

        //Elimate Module
        ELIMINATE_GAME_READY,
        ELIMINATE_GAME_START,
        ELIMINATE_ROUND_CHANGE,
        ELIMINATE_SIWTCH_FAILED,
        ELIMINATE_NOT_MY_TURN,
        ELIMINATE_OP_STEP,
        ELIMINATE_GAME_RESULT,
        ELIMINATE_USESKILL_STEP,
        ELIMINATE_OTHERPLAYER_EXIT,
        ELIMINATE_USESKILL_FAILED,
    }
}
