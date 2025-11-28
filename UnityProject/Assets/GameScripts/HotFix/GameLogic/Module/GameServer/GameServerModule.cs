using Sproto;
using System;
using System.Collections;
using System.Net.Sockets;
using UnityEngine;

namespace GameLogic {
    public class GameServerModule : Singleton<GameServerModule>
    {
        public double serverTime => _serverTime;
        private double _serverTime = 0;

        public Int64 serverOpenDay => _serverOpenDay;
        private Int64 _serverOpenDay = 0; 

        protected override void OnInit()
        {
            GameModule.Net.RegNetMsg<S2cProtocol.sc_base_game_time>(OnServerTime);
        }

        protected override void OnRelease()
        {

        }



        SprotoTypeBase OnServerTime(SprotoTypeBase rpcReq)
        {
            S2cSprotoType.sc_base_game_time.request req = (S2cSprotoType.sc_base_game_time.request)rpcReq;
            _serverTime = req.time;
            _serverOpenDay = req.serverRunDay;
            return null;
        }

    }
}