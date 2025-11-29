using Sproto;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using TEngine;
using Unity.Android.Types;
using UnityEngine;

namespace GameLogic
{
    enum Eliminate_OP_Result {
        NotCurPlayer = 1,
        InvalidPos = 2,
        NoEliminate = 3,
    }

    public class EliminateModule : Singleton<EliminateModule>
    {
        // 当前战局信息
        public int roomId;
        public int reward;
        public List<S2cSprotoType.eliminate_member_info> players;
        public int[] matchPlayers;
        public List<S2cSprotoType.Eliminate_Tile> map;

        // 当前操作步骤
        public int round;
        public int playeridx;
        public int roundTime;
        public int opCount;

        public bool playerChanged;
        public bool roundChanged;
        public bool opAddPlayed;

        public S2cSprotoType.Pos opP1 = new S2cSprotoType.Pos();
        public S2cSprotoType.Pos opP2 = new S2cSprotoType.Pos();
        public bool opIsCrossBomb;
        public int opSkill;
        public List<S2cSprotoType.Eliminate_Step> opStep = null;
        public int opDbid = 0;
        public int showOpChangeIdx = 0;
        public int playStep = 0;

        // Sproto.sc_eliminate_useskill_step_request 或者 Sproto.sc_eliminate_useskill_step_request
        public List<object>     opStack = new List<object>();

        protected override void OnInit()
        {
            GameModule.Net.RegNetMsg<S2cProtocol.sc_eliminate_map_info>(onMapInfo);
            GameModule.Net.RegNetMsg<S2cProtocol.sc_eliminate_map_start>(this.onMapStart);
            GameModule.Net.RegNetMsg<S2cProtocol.sc_eliminate_round_info>(this.onRoundInfo);
            GameModule.Net.RegNetMsg<S2cProtocol.sc_eliminate_op_ret>(this.onOpRet);
            GameModule.Net.RegNetMsg<S2cProtocol.sc_eliminate_op_step>(this.onOpStep);
            GameModule.Net.RegNetMsg<S2cProtocol.sc_eliminate_finish>(this.onGameEnd);
            GameModule.Net.RegNetMsg<S2cProtocol.sc_eliminate_useskill_ret>(this.onUseSkillRet);
            GameModule.Net.RegNetMsg<S2cProtocol.sc_eliminate_useskill_step>(this.onUseSkillStep);
            GameModule.Net.RegNetMsg<S2cProtocol.sc_eliminate_reconnect>(this.onReconnect);
            GameModule.Net.RegNetMsg<S2cProtocol.sc_eliminate_game_reconnected>(this.onReconnected);
            GameModule.Net.RegNetMsg<S2cProtocol.sc_eliminate_otherplayerExit>(this.onOtherPlayerExit);
            GameModule.Net.RegNetMsg<S2cProtocol.sc_eliminate_playerExit>(this.onExit);
        }

        protected override void OnRelease()
        {

        }

        private SprotoTypeBase onMapInfo(SprotoTypeBase rsp)
        {
            var response = rsp as S2cSprotoType.sc_eliminate_map_info.request;
            
            roomId = (int)response.roomid;
            reward = (int)response.reward;
            players = response.members;
            matchPlayers = new int[players.Count];
            for (int i = 0; i < players.Count; i++)
            {
                matchPlayers[i] = (int)players[i].dbid;
            }

            
            GameEvent.Send((int)EventDef.ELIMINATE_GAME_READY);
            return null;
        }

        private SprotoTypeBase onMapStart(SprotoTypeBase rsp)
        {
            var response = rsp as S2cSprotoType.sc_eliminate_map_start.request;
            map = response.map;
            round = (int)response.round;
            playeridx = (int)response.playeridx - 1;
            roundTime = (int)response.time;
            opCount = (int)response.opCount;

            opStack.Clear();
            opStep = null;
            playerChanged = true;
            roundChanged = false;
            opAddPlayed = false;

            GameEvent.Send((int)EventDef.ELIMINATE_GAME_START);
            //Open UI
            // if (response.practice)
            // {
            //     GameModule.UI.ShowUIAsync<EliminatePracticeUI>();
            // }
            // else
            // {
            //     GameModule.UI.ShowUIAsync<EliminateGameUI>();
            // }

            return null;
        }
        
        private SprotoTypeBase onRoundInfo(SprotoTypeBase rsp)
        {
            var response = rsp as S2cSprotoType.sc_eliminate_round_info.request;
            if (playeridx != (int)response.playeridx - 1)
            {
                playerChanged = true;
            }
            if (round != (int)response.round)
            {
                roundChanged = true;
            }

            round = (int)response.round;
            playeridx = (int)response.playeridx - 1;
            roundTime = (int)response.time;
            opCount = (int)response.opCount;
            if (response.matchPlayers != null)
            {
                matchPlayers = new int[response.matchPlayers.Count];
                for (int i = 0; i < response.matchPlayers.Count; i++)
                {
                    matchPlayers[i] = (int)response.matchPlayers[i];
                }
            }

            GameEvent.Send((int)EventDef.ELIMINATE_ROUND_CHANGE);  

            return null;
        }

        private SprotoTypeBase onOpRet(SprotoTypeBase rsp)
        {
            var response = rsp as S2cSprotoType.sc_eliminate_op_ret.request;
            if (response.ret != 0)
            {
                if (response.ret == (int)Eliminate_OP_Result.NoEliminate)
                {
                    GameEvent.Send<S2cSprotoType.Pos, S2cSprotoType.Pos>((int)EventDef.ELIMINATE_SIWTCH_FAILED, response.p1, response.p2);
                }
                else if (response.ret == (int)Eliminate_OP_Result.NotCurPlayer)
                {
                    GameEvent.Send((int)EventDef.ELIMINATE_NOT_MY_TURN);
                }
                else 
                {
                    //show tips
                }
            }
            return null;
        }

        private void setCurOpStep(S2cSprotoType.sc_eliminate_op_step.request rsp)
        {
            opP1.x = rsp.p1.x - 1;
            opP1.y = rsp.p1.y - 1;
            opP2.x = rsp.p2.x - 1;
            opP2.y = rsp.p2.y - 1;
            opDbid = (int)rsp.dbid;
            opStep = rsp.step;
            opCount = (int)rsp.opCount;
            roundTime = (int)rsp.roundTime;
            showOpChangeIdx = (int)rsp.changeOpCount;
            opAddPlayed = false;
            opIsCrossBomb = rsp.crossbomb;
        }

        private SprotoTypeBase onOpStep(SprotoTypeBase rsp)
        {
            var response = rsp as S2cSprotoType.sc_eliminate_op_step.request;
            if (opStep != null) 
            {
                opStack.Add(response);
            }
            else
            {
                setCurOpStep(response);
            }

            GameEvent.Send((int)EventDef.ELIMINATE_OP_STEP);
            return null;
        }

        private SprotoTypeBase onGameEnd(SprotoTypeBase rsp)
        {
            var response = rsp as S2cSprotoType.sc_eliminate_finish.request;
            GameEvent.Send<S2cSprotoType.sc_eliminate_finish.request>((int)EventDef.ELIMINATE_GAME_RESULT, response);
            return null;
        }

        private SprotoTypeBase onUseSkillRet(SprotoTypeBase rsp)
        {
            var response = rsp as S2cSprotoType.sc_eliminate_useskill_ret.request;
            if (response.ret != 0)
            {
                // show tips

                GameEvent.Send((int)EventDef.ELIMINATE_USESKILL_FAILED);
            }
            return null;
        }

        private void setCurSkillStep(S2cSprotoType.sc_eliminate_useskill_step.request rsp) {
            if (rsp.pos != null)
            {
                opP1.x = rsp.pos.x - 1;
                opP1.y = rsp.pos.y - 1;
            } 
            opDbid = (int)rsp.dbid;
            opStep = rsp.step;
            opCount = (int)rsp.opCount;
            opSkill = (int)rsp.skillid;
            roundTime = (int)rsp.roundTime;
            showOpChangeIdx = 0;
            opAddPlayed = false;
            opIsCrossBomb = false;
            if (rsp.map != null)
                map = rsp.map;
            foreach (var t in players) {
                if (t.dbid == rsp.dbid) {
                    t.skillcount = rsp.skillcount;
                    break;
                }
            }
        }

        private SprotoTypeBase onUseSkillStep(SprotoTypeBase rsp)
        {
            var response = rsp as S2cSprotoType.sc_eliminate_useskill_step.request;
            if (opStep != null) 
            {
                opStack.Add(response);
            }
            else
            {
                setCurSkillStep(response);
            }
            
            GameEvent.Send((int)EventDef.ELIMINATE_USESKILL_STEP);
            return null;
        }

        private SprotoTypeBase onReconnect(SprotoTypeBase rsp)
        {
            return null;
        }

        private SprotoTypeBase onReconnected(SprotoTypeBase rsp)
        {
            return null;
        }

        private SprotoTypeBase onOtherPlayerExit(SprotoTypeBase rsp)
        {
            return null;
        }

        private SprotoTypeBase onExit(SprotoTypeBase rsp)
        {
            return null;
        }


        public void SendSwitch(int p1x, int p1y, int p2x, int p2y, bool practice = false)
        {
            var req = new C2sSprotoType.cs_eliminate_switch.request();
            req.p1 = new C2sSprotoType.Pos();
            req.p2 = new C2sSprotoType.Pos();
            req.p1.x = p1x + 1;
            req.p1.y = p1y + 1;
            req.p2.x = p2x + 1;
            req.p2.y = p2y + 1;
            req.practice = practice;

            GameModule.Net.Send<C2sProtocol.cs_eliminate_switch>(req);
        }

        public void SendUseSkill(int skillId, int x = 0, int y = 0, bool practice = false)
        {
            var req = new C2sSprotoType.cs_eliminate_use_skill.request();
            req.id= skillId;
            req.pos = new C2sSprotoType.Pos();
            req.pos.x = x + 1;
            req.pos.y = y + 1;
            req.practice = practice;

            GameModule.Net.Send<C2sProtocol.cs_eliminate_use_skill>(req);
        }

        public void SendReconnect(int roomid)
        {
            var req = new C2sSprotoType.cs_eliminate_reconnect.request();
            req.roomid = roomid;

            GameModule.Net.Send<C2sProtocol.cs_eliminate_reconnect>(req);
        }

        public void SendcreatePractice()
        {
            var req = new C2sSprotoType.cs_eliminate_practice.request();
            GameModule.Net.Send<C2sProtocol.cs_eliminate_practice>(req);
        }

        public void SendExit(bool practice = false, int roomid = 0)
        {
            var req = new C2sSprotoType.cs_eliminate_exit.request();
            req.practice = practice;
            req.roomid = roomid;
            GameModule.Net.Send<C2sProtocol.cs_eliminate_exit>(req);
        }

        public S2cSprotoType.Eliminate_Tile GetBlock(int x, int y)
        {
            int idx = GetBlockIdx(x, y);
            return map[idx];
        }

        public int GetBlockIdx(int x, int y)
        {
            return y * EliminateConfig.MAP_WIDTH + x;
        }

        public S2cSprotoType.eliminate_member_info GetPlayerByDbid(int dbid)
        {
            if (dbid == 0)
                dbid = (int)GameModule.Actor.dbid;    //这里获取当前DBID
            foreach (var p in players)
            {
                if (p.dbid == dbid)
                {
                    return p;
                }
            }
            return null;
        }

        public int GetCurMatchPlayerDbid()
        {
            return matchPlayers[playeridx];
        }
    }
}