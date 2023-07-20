using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons;
using ECommons.Automation;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.Hooks;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using Splatoon.SplatoonScripting;
using Splatoon.Utils;

namespace SplatoonScriptsOfficial.Duties.Endwalker
{
    /*
     * 本脚本实现
     * P1 自动无敌、ST时候开启盾姿
     * P1->P2 自动铁壁、血乱、嗜血
     * P2 接线自动铁壁、暗影墙、黑盾
     * P5_1死刑_自动铁壁、暗影墙
     * P5_4死刑_自动铁壁、暗影墙
     * P5_3运延迟爆发
     * P6 自动减伤
     * P6 第一次魔数自动LB、点掉BUFF
     */
    internal class OMG_DK_SC : SplatoonScript
    {
        public override HashSet<uint> ValidTerritories => new() { 1122 };
        public override Metadata? Metadata => new(34, "OMG黑骑自动化脚本");



        long p1_全能之主读条Time = long.MaxValue;
        long p1_无敌_Time = long.MaxValue;

        long P2_开场_Time = long.MaxValue;
        long P2_开场_DK_SetCombo_Time = long.MaxValue;


        long P2_刀光剑舞_铁壁_Time = long.MaxValue;
        long P2_刀光剑舞_黑盾_Time = long.MaxValue;
        long P2_刀光剑舞_圣光幕帘_Time = long.MaxValue;
        long P2_盾连击_Time = long.MaxValue;


        long P3_Time = long.MaxValue;
        long P3_严重错误_Time = long.MaxValue;

        long P3_探测式波动炮读条_Time = long.MaxValue;

        long P4_Time = long.MaxValue;
        long P4_开启弗雷Time = long.MaxValue;



        long P2_Scmbo_开启战逃反应_Time = long.MaxValue;
        long P2_Scmbo_开启大宝剑连击_Time = long.MaxValue;


        long P5_二运读条_Time = long.MaxValue;
        long P5_关闭爆发_Time = long.MaxValue;
        long P5_开启爆发_Time = long.MaxValue;

        long P5_1死刑_自动减伤_time = long.MaxValue;
        long P5_4死刑_自动减伤_time = long.MaxValue;




        long P6_Time = long.MaxValue;


        long P6_Scmbo_开启爆发_Time = long.MaxValue;


        long P6_Scmbo_关闭腐秽大地 = long.MaxValue;
        long P6_Scmbo_开启腐秽大地 = long.MaxValue;

        long P6_宇宙射线_黑盾 = long.MaxValue;


        long P6_宇宙龙炎_铁壁 = long.MaxValue;
        long P6_宇宙龙炎_弃明投暗 = long.MaxValue;

        long P6_宇宙龙炎_黑盾 = long.MaxValue;




        long P6_挡枪1_暗黑布道 = long.MaxValue;
        long P6_陨石_暗黑布道 = long.MaxValue;



        long P6_挡枪1_无敌 = long.MaxValue;
        long P6_挡枪1_给搭档黑盾 = long.MaxValue;




        long P6_挡枪2_暗影墙 = long.MaxValue;
        long P6_挡枪2_至黑之夜 = long.MaxValue;
        long P6_挡枪2_奉献 = long.MaxValue;



        long P6_宇宙记忆_雪仇 = long.MaxValue;
        long P6_雪仇 = long.MaxValue;
        long P6_波动炮_雪仇 = long.MaxValue;


        long P6_极限技 = long.MaxValue;
        long P6_极限技取消buff = long.MaxValue;



        private int 波动炮次数 = 0;
        private int 魔数次数 = 0;

        private const string 嗜血_actionName = "嗜血";
        private const string 血乱_actionName = "血乱";

        private const string 铁壁_actionName = "铁壁";
        private const string 弃明投暗_actionName = "弃明投暗";
        private const string 暗影墙_actionName = "暗影墙";
        private const string 无敌_actionName = "行尸走肉";

        private const string 至黑之夜_actionName = "至黑之夜";
        private const string 至黑之夜_2_actionName = "至黑之夜 <2>";

        private const string 献奉_actionName = "献奉";
        private const string 献奉_2_actionName = "献奉 <2>";
        private const string 暗黑布道_actionName = "暗黑布道";
        private const string 雪仇_actionName = "雪仇";


        private const string 深恶痛觉_actionName = "深恶痛绝";
        private const string 掠影示现_actionName = "掠影示现";

        private const string 极限技_actionName = "极限技";




        private uint jobId = 32;


        public override void OnMessage(string Message)
        {
            var localPlaye = Svc.ClientState.LocalPlayer;
            if (localPlaye == null) return;
            if (localPlaye.ClassJob.Id != jobId)
            {
                return;
            }
            //全能之主读条
            if (Message.Contains("(7695>31499)"))
            {
                p1_全能之主读条Time = Environment.TickCount64;
                p1_无敌_Time = p1_全能之主读条Time + 38 * 1000;
                检查盾姿();
                myPluginLog("全能之主读条");
            }

            //原子射线读条
            if (Message.Contains("(7695>31480)"))
            {
                // p1_全能之主读条Time = Environment.TickCount64;
                // p1_无敌_Time = p1_全能之主读条Time + 42 * 1000;
                // myPluginLog("原子射线读条");
            }

            if (Message.Contains("我既是欧米茄，又是阿尔法…… 导入弱小人类的特征，来追寻真正的强大"))
            {
                P2_开场_Time = Environment.TickCount64;
                P2_开场_DK_SetCombo_Time = P2_开场_Time + 2 * 1000;
                useAction(铁壁_actionName, 10, 200);
                useAction(嗜血_actionName, 10, 200);
                useAction(血乱_actionName, 10, 200);
                myPluginLog("P2 开场");
            }



            //P2 剑击
            if (Message.Contains("(7635>31526)"))
            {
                P2_Scmbo_开启战逃反应_Time = Environment.TickCount64 + 10 * 1000;
                P2_Scmbo_开启大宝剑连击_Time = Environment.TickCount64 + 24 * 1000;
                myPluginLog("P2_剑击");
            }


            //P2防火墙消失
            if (Message.Contains("7635>31551"))
            {
                P2_刀光剑舞_圣光幕帘_Time = Environment.TickCount64 + 8 * 1000;
                myPluginLog("P2_防护程序F状态效果消失了");
            }

            //P2 刀光剑舞接线
            if (Message.Contains("(7633>31539)"))
            {
                P2_刀光剑舞_铁壁_Time = Environment.TickCount64 + 5 * 1000;
                P2_刀光剑舞_黑盾_Time = Environment.TickCount64 + 3 * 1000;
                useAction(暗影墙_actionName, 20, 100);
                myPluginLog("P2_刀光剑舞接线");
            }

            //P2 盾连击
            if (Message.Contains("(7633>31527)"))
            {
                P2_盾连击_Time = Environment.TickCount64;
                Chat.Instance.SendMessage("/scombo unset DRK_LivingShadow");
                Chat.Instance.SendMessage("/scombo unset DRK_Shadowbringer");
                myPluginLog("P2_盾连击");
            }

            //P3 开场
            if (Message.Contains("(最终解析……大幅变更战略战术逻辑…… 同意改变基本形态构造)"))
            {
                P3_Time = Environment.TickCount64;
                Chat.Instance.SendMessage("/scombo set DRK_LivingShadow");
                Chat.Instance.SendMessage("/scombo set DRK_Shadowbringer");
                myPluginLog("P2_盾连击");
            }

            //P3 严重错误 hw闭庭
            if (Message.Contains("(7636>31588)"))
            {
                P3_严重错误_Time = Environment.TickCount64;
                Chat.Instance.SendMessage("/scombo unset DRK_LivingShadow");
                myPluginLog("P3_严重错误_Time");
            }

            //P3 测式波动炮
            if (Message.Contains("(7636>31596)"))
            {
                P3_探测式波动炮读条_Time = Environment.TickCount64;
                Chat.Instance.SendMessage("/scombo auto 0");
                myPluginLog("P3_测式波动炮_Time");
            }

            //P4
            if (Message.Contains("哔——系统强制重启…… 即将以强制解除限制的状态重新启动"))
            {
                P4_Time = Environment.TickCount64;
                P4_开启弗雷Time = Environment.TickCount64 + 28 * 1000;
                myPluginLog("P4_Time");
            }


            //P5二运读条
            if (Message.Contains("(12257>32788)"))
            {
                P5_二运读条_Time = Environment.TickCount64 + 3 * 1000;
                P5_关闭爆发_Time = Environment.TickCount64 + 10 * 1000;

                myPluginLog("P5_二运读条_Time");
            }

            //三运读条
            if (Message.Contains("(12257>32789)"))
            {
                P5_开启爆发_Time = Environment.TickCount64 + 8 * 1000;
                if (Controller.GetConfig<Config>().P5_4死刑_自动减伤)
                {
                    P5_4死刑_自动减伤_time = Environment.TickCount64 + 61 * 1000;
                }
                myPluginLog("P5_三运读条_Time");
            }

            if (Controller.Scene == 7)
            {

                //宇宙记忆
                //05秒读条 14秒判定
                if (Message.Contains("(12256>31649)"))
                {
                    if (Controller.GetConfig<Config>().P6宇宙记忆雪仇)
                    {
                        P6_雪仇 = Environment.TickCount64 + 3 * 1000;
                    }
                    P6_宇宙射线_黑盾 = Environment.TickCount64 + 11 * 1000;
                }


                //宇宙龙炎
                //第一次为8秒 35秒读条 44秒判断
                //第二次为读条时间为7.5秒
                if (Message.Contains("(12256>31654)"))
                {
                    P6_雪仇 = Environment.TickCount64 + 0;
                    P6_宇宙龙炎_弃明投暗 = Environment.TickCount64 + 5 * 1000;
                    P6_宇宙龙炎_铁壁 = Environment.TickCount64 + 4 * 1000;
                    P6_宇宙龙炎_黑盾 = Environment.TickCount64 + 2 * 1000;
                }


                //P6_波动炮
                if (Message.Contains("(12256>31657)"))
                {
                    波动炮次数++;
                    switch (波动炮次数)
                    {
                        //读条时间为10.8秒
                        case 1:
                        {
                            P6_挡枪1_无敌 = Environment.TickCount64 + 6 * 1000;
                            P6_挡枪1_给搭档黑盾 = Environment.TickCount64 + 9 * 1000 + 500;
                            P6_雪仇 = Environment.TickCount64 + 1800;
                            break;
                        }

                        ////读条时间为11.5秒
                        case 2:
                        {
                            P6_雪仇 = Environment.TickCount64 + 2 * 1000 - 300;
                            P6_挡枪2_奉献 = Environment.TickCount64 + 4 * 1000;
                            P6_挡枪2_暗影墙 = Environment.TickCount64 + 7 * 1000;
                            P6_挡枪2_至黑之夜 = Environment.TickCount64 + 7 * 1000;
                            break;
                        }
                    }


                }

                //P6_宇宙流星
                if (Message.Contains("(12256>31664)"))
                {
                    P6_雪仇 = Environment.TickCount64 + 9 * 1000;
                }

                //P6_魔数
                if (Message.Contains("(12256>31670)"))
                {
                    魔数次数++;
                    switch (魔数次数)
                    {
                        //读条时间为10.8秒
                        case 1:
                        {

                            P6_极限技 = Environment.TickCount64;
                            P6_极限技取消buff = Environment.TickCount64 + 6 * 1000;

                            break;
                        }

                        ////读条时间为11.5秒
                        case 2:
                        {
                            break;
                        }
                    }
                }


            }

        }
        public override void OnUpdate()
        {
            
            var localPlaye = Svc.ClientState.LocalPlayer;
            if (localPlaye == null) return;

            if (localPlaye.ClassJob.Id == jobId)
            {
                if (Environment.TickCount64 >= p1_无敌_Time)
                {
                    useAction(无敌_actionName, 10);
                    disabledScombo();
                    p1_无敌_Time = long.MaxValue;
                }



                if (Environment.TickCount64 > P2_开场_DK_SetCombo_Time)
                {
                    ResetScombo();
                    P2_开场_DK_SetCombo_Time = long.MaxValue;
                    myPluginLog("P2_开场_DK_SetCombo");
                }


                //延后3个gcd，开启战逃反应
                if (Environment.TickCount64 > P2_Scmbo_开启大宝剑连击_Time)
                {
                    P2_Scmbo_开启大宝剑连击_Time = long.MaxValue;
                    myPluginLog($"{nameof(P2_Scmbo_开启大宝剑连击_Time)}");
                }



                if (Environment.TickCount64 > P2_刀光剑舞_铁壁_Time)
                {
                    useAction(铁壁_actionName, 20, 100);
                    P2_刀光剑舞_铁壁_Time = long.MaxValue;
                    myPluginLog($"{nameof(P2_刀光剑舞_铁壁_Time)}");
                }




                if (Environment.TickCount64 > P2_刀光剑舞_黑盾_Time)
                {
                    P2_刀光剑舞_黑盾_Time = long.MaxValue;
                    useAction(至黑之夜_actionName, 20, 100);
                    myPluginLog($"{nameof(P2_刀光剑舞_黑盾_Time)}");
                }

                if (Environment.TickCount64 > P4_开启弗雷Time)
                {
                    P4_开启弗雷Time = long.MaxValue;
                    Chat.Instance.SendMessage("/scombo set DRK_LivingShadow");
                    myPluginLog($"{nameof(P4_开启弗雷Time)}");
                }


                if (Controller.Scene == 6)
                {
                    if (Environment.TickCount64 > P5_关闭爆发_Time)
                    {
                        disabledScombo();
                        Chat.Instance.SendMessage("/scombo set DRK_ManaOvercap");
                        Chat.Instance.SendMessage("/scombo set DRK_BloodGaugeOvercap");
                        P5_关闭爆发_Time = long.MaxValue;

                        myPluginLog($"{nameof(P5_关闭爆发_Time)}");
                    }


                    if (Environment.TickCount64 > P5_开启爆发_Time)
                    {
                        ResetScombo();
                        P5_开启爆发_Time = long.MaxValue;

                        myPluginLog($"{nameof(P5_开启爆发_Time)}");
                        
                    }

                    
                    if (Environment.TickCount64 >= P5_1死刑_自动减伤_time)
                    {
                        useAction(铁壁_actionName, 10, 200);
                        useAction(暗影墙_actionName, 10, 200);
                        myPluginLog(nameof(P5_1死刑_自动减伤_time));

                        P5_1死刑_自动减伤_time = long.MaxValue;
                    }

                    if (Environment.TickCount64 >= P5_4死刑_自动减伤_time)
                    {
                        useAction(铁壁_actionName, 10, 200);
                        useAction(暗影墙_actionName, 10, 200);
                        myPluginLog(nameof(P5_4死刑_自动减伤_time));
                        P5_4死刑_自动减伤_time = long.MaxValue;
                    }
                }




                //P6
                if (Controller.Scene == 7)
                {

                    if (Environment.TickCount64 >= P6_Scmbo_开启爆发_Time)
                    {
                        ResetScombo();
                        P6_Scmbo_开启爆发_Time = long.MaxValue;

                        //自动输出
                        Chat.Instance.SendMessage("/scombo auto 3632");

                        myPluginLog("P6_Scmbo_开启爆发_Time");
                    }

                    if (Environment.TickCount64 >= P6_Scmbo_开启腐秽大地)
                    {
                        Chat.Instance.SendMessage("/scombo set DRK_SaltedEarth");

                        myPluginLog("P6_Scmbo_开启腐秽大地");

                        Chat.Instance.SendMessage("/e P6_Scmbo_开启腐秽大地");

                        P6_Scmbo_开启腐秽大地 = Environment.TickCount64 + 120 * 1000;
                        P6_Scmbo_关闭腐秽大地 = Environment.TickCount64 + 30 * 1000;
                    }

                    if (Environment.TickCount64 >= P6_Scmbo_关闭腐秽大地)
                    {
                        Chat.Instance.SendMessage("/scombo unset DRK_SaltedEarth");

                        Chat.Instance.SendMessage("/e P6_Scmbo_关闭腐秽大地");

                        myPluginLog("P6_Scmbo_关闭腐秽大地");

                        P6_Scmbo_关闭腐秽大地 = long.MaxValue;
                    }



                    if (Environment.TickCount64 >= P6_雪仇)
                    {
                        useAction(雪仇_actionName, 10, 200);
                        myPluginLog(nameof(P6_雪仇));
                        P6_雪仇 = long.MaxValue;
                    }


                    if (Environment.TickCount64 >= P6_宇宙射线_黑盾)
                    {
                        useAction(至黑之夜_actionName, 20, 100);
                        
                        Chat.Instance.SendMessage($"/e {nameof(至黑之夜_actionName)}");
                        P6_宇宙射线_黑盾 = long.MaxValue;
                        myPluginLog(nameof(P6_宇宙射线_黑盾));
                    }


                    if (Environment.TickCount64 >= P6_宇宙龙炎_铁壁)
                    {
                        useAction(铁壁_actionName, 20, 100);
                        Chat.Instance.SendMessage($"/e {铁壁_actionName}<se.1>");
                        P6_宇宙龙炎_铁壁 = long.MaxValue;
                        myPluginLog($"{nameof(P6_宇宙龙炎_铁壁)}");
                    }

                    if (Environment.TickCount64 >= P6_宇宙龙炎_弃明投暗)
                    {
                        useAction(弃明投暗_actionName, 20, 100);
                        Chat.Instance.SendMessage($"/e {弃明投暗_actionName}<se.1>");
                        P6_宇宙龙炎_弃明投暗 = long.MaxValue;
                        myPluginLog($"{nameof(P6_宇宙龙炎_弃明投暗)}");
                    }



                    if (Environment.TickCount64 >= P6_宇宙龙炎_黑盾)
                    {
                        if (Controller.GetConfig<Config>().P6_交换减伤)
                        {
                            useAction(至黑之夜_2_actionName, 20, 100);
                            Chat.Instance.SendMessage($"/e {至黑之夜_2_actionName} <se.1>");
                        }
                        else
                        {
                            useAction(至黑之夜_actionName, 20, 100);
                            Chat.Instance.SendMessage($"/e {至黑之夜_actionName}<se.1>");
                        }


                        P6_宇宙龙炎_黑盾 = long.MaxValue;
                        myPluginLog($"{nameof(P6_宇宙龙炎_黑盾)}");
                    }


                    if (Environment.TickCount64 >= P6_挡枪1_无敌)
                    {
                        if (Controller.GetConfig<Config>().P6_1挡无敌_2档减伤)
                        {
                            useAction(无敌_actionName, 20);
                            Chat.Instance.SendMessage($"/e {无敌_actionName}<se.1>");
                            myPluginLog(nameof(P6_挡枪1_无敌));
                        }
                        else
                        {
                            useAction(暗影墙_actionName, 40, 100);
                            Chat.Instance.SendMessage($"/e {暗影墙_actionName}<se.1>");
                            myPluginLog($"P6_挡枪1_开启_暗影墙");
                        }
                        P6_挡枪1_无敌 = long.MaxValue;
                    }


                    if (Environment.TickCount64 >= P6_挡枪1_给搭档黑盾)
                    {

                        if (Controller.GetConfig<Config>().P6_1挡无敌_2档减伤)
                        {
                            useAction(至黑之夜_2_actionName, 20, 100);
                            Chat.Instance.SendMessage($"/e {至黑之夜_2_actionName}<se.1>");
                            myPluginLog(nameof(P6_挡枪1_给搭档黑盾));
                        }
                        else
                        {
                            useAction(至黑之夜_actionName, 20, 100);
                            Chat.Instance.SendMessage($"/e {至黑之夜_actionName}<se.1>");
                            myPluginLog("P6_挡枪1_开启_自己黑盾");
                        }
                        P6_挡枪1_给搭档黑盾 = long.MaxValue;
                    }


                    if (Controller.GetConfig<Config>().P6_暗黑布道_挡枪1_陨石)
                    {
                        if (Environment.TickCount64 >= P6_挡枪1_暗黑布道)
                        {
                            useAction(暗黑布道_actionName, 20, 100);
                            Chat.Instance.SendMessage($"/e {暗黑布道_actionName}<se.1>");
                            P6_挡枪1_暗黑布道 = long.MaxValue;
                            myPluginLog($"{nameof(P6_挡枪1_暗黑布道)}");
                        }

                        if (Environment.TickCount64 >= P6_陨石_暗黑布道)
                        {
                            useAction(暗黑布道_actionName, 20, 100);
                            Chat.Instance.SendMessage($"/e {暗黑布道_actionName}<se.1>");
                            P6_陨石_暗黑布道 = long.MaxValue;
                            myPluginLog($"{nameof(P6_陨石_暗黑布道)}");
                        }
                    }


                    if (Environment.TickCount64 >= P6_挡枪2_暗影墙)
                    {

                        if (Controller.GetConfig<Config>().P6_1挡无敌_2档减伤)
                        {
                            useAction(暗影墙_actionName, 40, 100);
                            Chat.Instance.SendMessage($"/e {暗影墙_actionName}<se.1>");
                            myPluginLog($"{nameof(P6_挡枪2_暗影墙)}");
                        }
                        else
                        {
                            useAction(无敌_actionName, 20);
                            Chat.Instance.SendMessage($"/e {无敌_actionName}<se.1>");
                            myPluginLog("P6_挡枪2_开启无敌");
                        }
                        P6_挡枪2_暗影墙 = long.MaxValue;
                    }


                    if (Environment.TickCount64 >= P6_挡枪2_至黑之夜)
                    {
                        if (Controller.GetConfig<Config>().P6_1挡无敌_2档减伤)
                        {
                            useAction(至黑之夜_actionName, 20, 100);
                            Chat.Instance.SendMessage($"/e {至黑之夜_actionName}<se.1>");
                            myPluginLog($"{nameof(P6_挡枪2_至黑之夜)}");
                        }
                        else
                        {
                            useAction(至黑之夜_2_actionName, 20, 100);
                            Chat.Instance.SendMessage($"/e {至黑之夜_2_actionName}<se.1>");
                            myPluginLog("P6_挡枪2_给搭档黑盾");
                        }

                        P6_挡枪2_至黑之夜 = long.MaxValue;

                    }


                    if (Environment.TickCount64 >= P6_挡枪2_奉献)
                    {
                        if (Controller.GetConfig<Config>().P6_1挡无敌_2档减伤)
                        {
                            useAction(献奉_actionName, 20, 100);
                            Chat.Instance.SendMessage($"/e {献奉_actionName}<se.1>");
                            myPluginLog($"P6_挡枪2_给自己献奉");
                        }
                        else
                        {
                            useAction(献奉_2_actionName, 10, 333);
                            Chat.Instance.SendMessage($"/e {献奉_2_actionName}<se.1>");
                            myPluginLog("P6_挡枪2_给搭档献奉");
                        }
                        P6_挡枪2_奉献 = long.MaxValue;

                    }


                    if (Environment.TickCount64 >= P6_极限技)
                    {
                        useAction(极限技_actionName, 10, 100);
                        Chat.Instance.SendMessage($"/e {极限技_actionName}<se.1>");
                        myPluginLog($"P6极限技");
                        P6_极限技 = long.MaxValue;
                    }


                    if (Environment.TickCount64 >= P6_极限技取消buff)
                    {
                        Chat.Instance.SendMessage("/statusoff 暗黑之力");
                        myPluginLog($"P6_极限技取消buff");
                        P6_极限技取消buff = long.MaxValue;
                    }
                }




            }

        }
        private void 检查盾姿()
        {
            var localPlaye = Svc.ClientState.LocalPlayer;
            if (localPlaye == null) return;

            var isBuff = localPlaye.StatusList.Any(status => status.StatusId == 743);

            if (!isBuff)
            {
                useAction(深恶痛觉_actionName, 9, 100);
            }

        }


        public override void OnMapEffect(uint position, ushort data1, ushort data2)
        {
            var localPlaye = Svc.ClientState.LocalPlayer;
            if (localPlaye == null) return;
            if (localPlaye.ClassJob.Id != jobId)
            {
                return;
            }
            
            if (Controller.InCombat==false)
                return;


                // if (Controller.Scene == 6)
            long tickCount64 = Environment.TickCount64;
            {
                if (position == 20 && data1 == 4 && data2 == 8)
                {
                    myPluginLog("进入P5");
                    
                    
                    if (Controller.GetConfig<Config>().P5_1死刑_自动减伤)
                    {
                        P5_1死刑_自动减伤_time = tickCount64 + 14 * 1000;
                    }

                }
            }


            if (Controller.Scene == 7)
            {

                if (position == 0 && data1 == 1 && data2 == 2)
                {
                    myPluginLog("进入P6");

                    useAction(掠影示现_actionName);

                    //最近敌人
                    SendMessageLooP("/tenemy");

                    disabledScombo();
                    P6_Time = tickCount64;
                    P6_Scmbo_开启爆发_Time = tickCount64 + 7 * 1000 + 500;
                    P6_挡枪1_暗黑布道 = tickCount64 + 75 * 1000;
                    P6_陨石_暗黑布道 = tickCount64 + 176 * 1000;


                    P6_Scmbo_关闭腐秽大地 = tickCount64 + 30 * 1000;
                    P6_Scmbo_开启腐秽大地 = tickCount64 + 127 * 1000;

                }
            }

        }




        public override void OnDirectorUpdate(DirectorUpdateCategory category)
        {
            if (category.EqualsAny(DirectorUpdateCategory.Commence, DirectorUpdateCategory.Recommence, DirectorUpdateCategory.Wipe))
            {
                Reset();
            }
        }

        void Reset()
        {
            p1_无敌_Time = long.MaxValue;
            p1_全能之主读条Time = long.MaxValue;


            P2_开场_Time = long.MaxValue;
            P2_开场_DK_SetCombo_Time = long.MaxValue;
            P2_Scmbo_开启战逃反应_Time = long.MaxValue;
            P2_Scmbo_开启大宝剑连击_Time = long.MaxValue;

            P2_刀光剑舞_铁壁_Time = long.MaxValue;
            P2_刀光剑舞_黑盾_Time = long.MaxValue;
            P2_刀光剑舞_圣光幕帘_Time = long.MaxValue;


            P2_盾连击_Time = long.MaxValue;
            P3_Time = long.MaxValue;
            P3_严重错误_Time = long.MaxValue;

            P3_探测式波动炮读条_Time = long.MaxValue;

            P4_Time = long.MaxValue;
            P4_开启弗雷Time = long.MaxValue;


            P5_二运读条_Time = long.MaxValue;
            P5_关闭爆发_Time = long.MaxValue;
            P5_开启爆发_Time = long.MaxValue;

            P5_1死刑_自动减伤_time = long.MaxValue;
            P5_4死刑_自动减伤_time = long.MaxValue;



            P6_Time = long.MaxValue;

            P6_Scmbo_开启爆发_Time = long.MaxValue;
            P6_Scmbo_关闭腐秽大地 = long.MaxValue;
            P6_Scmbo_开启腐秽大地 = long.MaxValue;


            P6_宇宙射线_黑盾 = long.MaxValue;

            P6_宇宙龙炎_铁壁 = long.MaxValue;
            P6_宇宙龙炎_弃明投暗 = long.MaxValue;

            P6_宇宙龙炎_黑盾 = long.MaxValue;

            P6_挡枪1_无敌 = long.MaxValue;
            P6_挡枪1_给搭档黑盾 = long.MaxValue;





            P6_挡枪2_暗影墙 = long.MaxValue;
            P6_挡枪2_至黑之夜 = long.MaxValue;
            P6_挡枪2_奉献 = long.MaxValue;

            P6_宇宙记忆_雪仇 = long.MaxValue;
            P6_雪仇 = long.MaxValue;
            P6_波动炮_雪仇 = long.MaxValue;


            P6_挡枪1_暗黑布道 = long.MaxValue;
            P6_陨石_暗黑布道 = long.MaxValue;

            P6_极限技 = long.MaxValue;
            P6_极限技取消buff = long.MaxValue;

            波动炮次数 = 0;
            魔数次数 = 0;


            ResetScombo();
        }



        void ResetScombo()
        {
            // Chat.Instance.SendMessage("/scombo set DRK_Overcap");
            Chat.Instance.SendMessage("/scombo set DRK_MainComboBuffs_Group");
            Chat.Instance.SendMessage("/scombo set DRK_BloodGaugeOvercap");
            Chat.Instance.SendMessage("/scombo set DRK_ManaOvercap");
            Chat.Instance.SendMessage("/scombo set DRK_MainComboCDs_Group");
            Chat.Instance.SendMessage("/scombo set DRK_LivingShadow");
            Chat.Instance.SendMessage("/scombo set DRK_Shadowbringer");
            Chat.Instance.SendMessage("/scombo set DRK_SaltedEarth");
        }

        void disabledScombo()
        {
            // Chat.Instance.SendMessage("/scombo unset DRK_Overcap");
            Chat.Instance.SendMessage("/scombo unset DRK_MainComboBuffs_Group");
            Chat.Instance.SendMessage("/scombo unset DRK_BloodGaugeOvercap");
            Chat.Instance.SendMessage("/scombo unset DRK_ManaOvercap");
            Chat.Instance.SendMessage("/scombo unset DRK_MainComboCDs_Group");
            Chat.Instance.SendMessage("/scombo unset DRK_LivingShadow");
            Chat.Instance.SendMessage("/scombo unset DRK_Shadowbringer");
            Chat.Instance.SendMessage("/scombo unset DRK_SaltedEarth");
        }

        void myPluginLog(string text)
        {
            PluginLog.Information($"{nameof(OMG_DK_SC)} {text}");
        }

        private void SendMessageLooP(string message, int count = 10, int delay = 100)
        {
            Task.Run(async delegate
            {

                for (int i = 0; i < count; i++)
                {
                    Svc.Framework.RunOnFrameworkThread(() => { Chat.Instance.SendMessage($"{message}"); });
                    await Task.Delay(TimeSpan.FromMilliseconds(delay));
                }
            });

        }
        
        
        private void useAction(string actionName, int count = 10, int delay = 100)
        {
            Task.Run(async delegate
            {

                for (int i = 0; i < count; i++)
                {
                    Svc.Framework.RunOnFrameworkThread(() => { Chat.Instance.SendMessage($"/ac {actionName}"); });
                    await Task.Delay(TimeSpan.FromMilliseconds(delay));
                }
            });

        }


        public override void OnSettingsDraw()
        {

            ImGui.Checkbox("P5_1死刑_自动减伤", ref this.Controller.GetConfig<Config>().P5_1死刑_自动减伤);
            ImGui.Checkbox("P5_4死刑_自动减伤", ref this.Controller.GetConfig<Config>().P5_4死刑_自动减伤);

            ImGui.Checkbox("P6宇宙记忆雪仇", ref this.Controller.GetConfig<Config>().P6宇宙记忆雪仇);
            ImGui.Checkbox("P6_暗黑布道_挡枪1_陨石", ref this.Controller.GetConfig<Config>().P6_暗黑布道_挡枪1_陨石);
            ImGui.Checkbox("P6_交换减伤", ref this.Controller.GetConfig<Config>().P6_交换减伤);
            ImGui.TextColored(Colors.Yellow.ToVector4(), "不勾选为1档减伤_2挡无敌[自动给搭档减伤]");
            ImGui.Checkbox("P6_1挡无敌_2挡减伤", ref this.Controller.GetConfig<Config>().P6_1挡无敌_2档减伤);
        }


        public class Config : IEzConfig
        {
            public bool P5_1死刑_自动减伤 = true;
            public bool P5_4死刑_自动减伤 = true;

            public bool P6宇宙记忆雪仇 = true;
            public bool P6_暗黑布道_挡枪1_陨石 = true;
            public bool P6_交换减伤 = true;
            public bool P6_1挡无敌_2档减伤 = true;
        }
    }
}
