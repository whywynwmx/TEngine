using Sproto;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameLogic
{
    public class ActorModule : Singleton<ActorModule>
    {
        public long dbid;
        public string name;

        protected override void OnInit()
        {
            GameModule.Net.RegNetMsg<S2cProtocol.sc_enter_game>(onEnterGame);
        }

        protected override void OnRelease()
        {
        
        }

        private SprotoTypeBase onEnterGame(SprotoTypeBase _rsp)
        {
            var msg = _rsp as S2cSprotoType.sc_enter_game.request;
            dbid = msg.dbid;
            name = msg.name;
            
            return null;
        }
    }
}
