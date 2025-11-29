using System;
using System.Collections.Generic;

namespace GameLogic
{
    public static class EliminateConfig
    {
        public static readonly int[] PLAYER_COUNT = new int[] { 2, 3, 4 };
        public static readonly int[] COSTS = new int[] { 100, 1000, 10000 };

        public const int ELIMINATE_MIN = 3;
        public const int ELIMINATE_MAX = 5;

        public const int ENTER_COND_NUM = 2;

        public const int MAX_PLAYER_ITEM = 4;
        public const int MAX_ROUND = 5;

        public const int MAP_WIDTH = 7;
        public const int MAP_HEIGHT = 7;

        public const int CHESS_WIDTH = 95;
        public const int CHESS_HEIGHT = 95;

        public const int COUNT_DOWN = 5;

        public const int OP_COUNT_ONE_ROUND = 2; // 每回合操作数
        public const int ROUND_TIME = 60;

        public const double REWARD_FEE = 0.05;

        public const int SWITCH_TIME = 163;    // 执行旋转0.3s
        public const int BOMB_TIME = 480;      // 爆炸
        public const int DROP_TIME = 219;      // 爆炸后下落
        public const int HOLD_TIME = 170;      // 交换后保持
        public const int SWITH_HOLD_TIME = 125; // 随换后保持
        public const int SKILL_KILL_EFF_TIME = 400; // 技能消除时间
        public const int BOMB_PLAY_TIME = 160; // 爆炸播放时间
        public const int GENERATE_BOME_TIME = 60; // 爆炸上出时间

        public const double SCORE_SCROLL_MIN_TIME = 500.0 / 1000.0; // 分数滚动时间
        public const double SCORE_SCROLL_MAX_TIME = 600.0 / 1000.0; // 分数滚动时间

        public const int ELIMINATE_FLY_TIME = 670;
        public const int ELIMINATE_FLY_EFF_LENGTH = 625;

        public const int HELP_TIPS_START_TIME = 10 * 1000;
        public const int HELP_TIPS_INTERVAL_TIME = 5 * 1000;

        // 分数从大到小
        public static readonly List<object[]> BETTER_SCORE = new List<object[]>
        {
            new object[] { 15, "ui_eff_stunning" },
            new object[] { 9, "ui_eff_highly" },
            new object[] { 6, "ui_eff_perfect" }
        };

        public const string INVITE_ROOM_URL = "http://119.29.235.150/xxl/invite.html";

        public static readonly int[] Club_Room_GameCount = new int[] { 2, 4, 8, 12 };

        public static int CalcClubRoomCard(int gamecount)
        {
            return (int)Math.Ceiling((double)gamecount / 2);
        }
    }
}