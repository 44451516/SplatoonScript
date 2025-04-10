using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.Automation;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.Hooks;
using ECommons.Logging;
using ECommons.MathHelpers;
using ECommons.Schedulers;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using Splatoon.SplatoonScripting;

namespace SplatoonScriptsOfficial.Duties.Endwalker
{
    internal class OMG_PLD_SC : SplatoonScript
    {
        public override HashSet<uint> ValidTerritories => new() { 1122 };
        public override Metadata? Metadata => new(46, "OMG骑士自动化脚本");



        long p1_全能之主读条Time = long.MaxValue;
        long p1_无敌_Time = long.MaxValue;

        long p2_开场_Time = long.MaxValue;
        long p2_开场_战逃反应_Time = long.MaxValue;


        long p2_刀光剑舞_铁壁_Time = long.MaxValue;
        long p2_刀光剑舞_圣盾阵_Time = long.MaxValue;
        long p2_刀光剑舞_圣光幕帘_Time = long.MaxValue;

        long p2_Scmbo_开启战逃反应_Time = long.MaxValue;
        long p2_Scmbo_开启大宝剑连击_Time = long.MaxValue;


        long P5_二运读条_Time = long.MaxValue;
        long P5_Scmbo_关闭爆发_Time = long.MaxValue;
        long P5_Scmbo_开启爆发_Time = long.MaxValue;

        long P5_3死刑_自动减伤 = long.MaxValue;
        long P5_4死刑_自动减伤 = long.MaxValue;
        long P5_转P6关闭盾姿 = long.MaxValue;

        long P6_Time = long.MaxValue;
        long P6_Scmbo_开启爆发_Time = long.MaxValue;

        long P6_宇宙射线_盾阵 = long.MaxValue;


        long P6_宇宙龙炎_铁壁 = long.MaxValue;
        long P6_宇宙龙炎_壁垒 = long.MaxValue;

        long P6_宇宙龙炎_盾阵 = long.MaxValue;
        long P6_宇宙龙炎_干预 = long.MaxValue;


        long P6_挡枪1_开启无敌 = long.MaxValue;
        long P6_挡枪1_开启干预 = long.MaxValue;
        long P6_幕帘_Time = long.MaxValue;

        long P6_挡枪2_开启预警 = long.MaxValue;
        long P6_挡枪2_开启盾阵 = long.MaxValue;
        long P6_挡枪2_关闭自动输出 = long.MaxValue;



        long P6_宇宙记忆_开启雪仇 = long.MaxValue;
        long P6_雪仇_Time = long.MaxValue;
        long P6_波动炮_开启雪仇 = long.MaxValue;
        long P6_流星_支援减伤_第一波 = long.MaxValue;
        long P6_流星_支援减伤_第二波 = long.MaxValue;

        long P6_极限技 = long.MaxValue;

        private int 波动炮次数 = 0;
        private int 魔数次数 = 0;

        private const string 战逃反应_actionName = "战逃反应";

        private const string 铁壁_actionName = "铁壁";
        private const string 壁垒_actionName = "壁垒";
        private const string 预警_actionName = "预警";
        private const string 无敌_actionName = "神圣领域";

        private const string 盾阵_actionName = "盾阵";
        private const string 干预2_actionName = "干预 <2>";
        private const string 干预7_actionName = "干预 <7>";
        private const string 干预8_actionName = "干预 <8>";
        private const string 圣光幕帘_actionName = "圣光幕帘";
        private const string 雪仇_actionName = "雪仇";

        private const string 钢铁信念_actionName = "钢铁信念";
        private const string 极限技_actionName = "极限技";


        private uint jobId = 19;
        private int 盾姿BUFFID = 79;


        public override void OnMessage(string Message)
        {
            var localPlaye = Svc.ClientState.LocalPlayer;
            if (localPlaye == null) return;
            if (localPlaye.ClassJob.Id != jobId)
            {
                return;
            }
            //全能之主读条

            if (Controller.Scene == 2)
            {
                
                if (Message.Contains("距离战斗开始还有10秒！"))
                {
                    if (Controller.GetConfig<Config>().ST)
                    {
                        关闭盾姿();
                    }
                    else
                    {
                        开启盾姿();
                    }
                }

                
                if (Message.Contains("(7695>31499)"))
                {
                    p1_全能之主读条Time = Environment.TickCount64;
                    p1_无敌_Time = p1_全能之主读条Time + 42 * 1000;
                    开启盾姿();
                    myPluginLog("全能之主读条");
                }
 
            }
            
            //P2
            if (Controller.Scene == 3)
            {
                //p2 剑击
                if (Message.Contains("(7635>31526)"))
                {
                    if (Controller.GetConfig<Config>().P2延后爆发)
                    {
                        // Chat.Instance.SendMessage("/scombo unset PLD_ST_AdvancedMode_FoF");
                        Chat.Instance.SendMessage("/scombo unset PLD_ST_AdvancedMode_HolySpirit");
                        Chat.Instance.SendMessage("/scombo unset PLD_ST_AdvancedMode_Confiteor");
                    }
                    p2_Scmbo_开启战逃反应_Time = Environment.TickCount64 + 10 * 1000;
                    p2_Scmbo_开启大宝剑连击_Time = Environment.TickCount64 + 16 * 1000;
                    p2_刀光剑舞_圣光幕帘_Time = Environment.TickCount64 + 30 * 1000;
                    myPluginLog("p2_剑击");
                }
                
                //p2 刀光剑舞接线
                if (Message.Contains("(7633>31539)"))
                {
                    p2_刀光剑舞_铁壁_Time = Environment.TickCount64 + 3 * 1000;
                    p2_刀光剑舞_圣盾阵_Time = Environment.TickCount64 + 5 * 1000;

                    useAction(预警_actionName, 20, 100);

                    myPluginLog("p2_刀光剑舞接线");
                }

            }
            
          
            //P5
            if (Controller.Scene == 6)
            {
                //P5二运读条
                if (Message.Contains("(12257>32788)"))
                {
                    P5_二运读条_Time = Environment.TickCount64 + 3 * 1000;
                    P5_Scmbo_关闭爆发_Time = Environment.TickCount64 + 10 * 1000;
                    if (Controller.GetConfig<Config>().P5_3死刑_自动减伤)
                    {
                        P5_3死刑_自动减伤 = Environment.TickCount64 + 67 * 1000;
                    }

                    P5_Scmbo_开启爆发_Time = Environment.TickCount64 + 100 * 1000;
                    myPluginLog("P5_二运读条_Time");
                }

            }


            //P6
            if (Controller.Scene == 7)
            {

                //宇宙记忆
                if (Message.Contains("(12256>31649)"))
                {

                    if (Controller.GetConfig<Config>().P6_宇宙记忆1LB)
                    {
                        P6_极限技 = Environment.TickCount64;
                    }

                    if (Controller.GetConfig<Config>().P6宇宙记忆雪仇)
                    {
                        P6_雪仇_Time = Environment.TickCount64 + 3 * 1000;
                    }
                    P6_宇宙射线_盾阵 = Environment.TickCount64 + 7 * 1000;
                }


                //宇宙龙炎 第一次读条6秒  第二次读条8秒
                if (Message.Contains("(12256>31654)"))
                {
                    P6_雪仇_Time = Environment.TickCount64 + 1 * 1000;

                    P6_宇宙龙炎_壁垒 = Environment.TickCount64 + 0;
                    P6_宇宙龙炎_铁壁 = Environment.TickCount64 + 2 * 1000;
                    P6_宇宙龙炎_干预 = Environment.TickCount64 + 4 * 1000 + 500;
                    P6_宇宙龙炎_盾阵 = Environment.TickCount64 + 5 * 1000 + 500;
                }


                //P6_波动炮 第一次读条11949
                if (Message.Contains("(12256>31657)"))
                {
                    波动炮次数++;
                    switch (波动炮次数)
                    {
                        case 1:
                        {
                            P6_雪仇_Time = Environment.TickCount64 + 1800;
                            P6_挡枪1_开启无敌 = Environment.TickCount64 + 6 * 1000;
                            if (Controller.GetConfig<Config>().P6幕帘_1挡枪)
                            {
                                P6_幕帘_Time = Environment.TickCount64 + 8 * 1000 ;
                            }
                            P6_挡枪1_开启干预 = Environment.TickCount64 + 7 * 1000;
                            break;
                        }

                        case 2:
                        {
                            P6_雪仇_Time = Environment.TickCount64 + 2 * 1000 - 300;


                            P6_挡枪2_开启预警 = Environment.TickCount64 + 2 * 1000;
                            P6_挡枪2_开启盾阵 = Environment.TickCount64 + 7 * 1000;
                            P6_挡枪2_关闭自动输出 = Environment.TickCount64 + 8 * 1000;
                            break;
                        }
                    }


                }

                //P6_宇宙流星
                if (Message.Contains("(12256>31664)"))
                {
                    P6_雪仇_Time = Environment.TickCount64 + 6 * 1000;

                    if (Controller.GetConfig<Config>().P6幕帘_陨石)
                    {
                        P6_幕帘_Time = Environment.TickCount64 + 4 * 1000 + 500;
                    }
                    
                    
                    P6_流星_支援减伤_第一波 = Environment.TickCount64 + 8 * 1000;
                    P6_流星_支援减伤_第二波 = Environment.TickCount64 + 25 * 1000;
                }



                //P6_魔数
                if (Message.Contains("(12256>31670)"))
                {
                    魔数次数++;
                    switch (魔数次数)
                    {
                        case 1:
                        {

                            if (Controller.GetConfig<Config>().P6_魔数1LB)
                            {
                                P6_极限技 = Environment.TickCount64;
                            }

                            break;
                        }

                        case 2:
                        {
                            if (Controller.GetConfig<Config>().P6_魔数2LB)
                            {
                                P6_极限技 = Environment.TickCount64;
                            }
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
                    p1_无敌_Time = long.MaxValue;
                }

                if (Environment.TickCount64 > p2_开场_战逃反应_Time)
                {
                    useAction(战逃反应_actionName, 10);
                    p2_开场_战逃反应_Time = long.MaxValue;
                    myPluginLog("p2_开场_战逃反应 战逃反应");
                }


                //延后3个gcd，开启战逃反应
                if (Environment.TickCount64 > p2_Scmbo_开启战逃反应_Time)
                {
                    Chat.Instance.SendMessage("/scombo set PLD_ST_AdvancedMode_FoF");
                    p2_Scmbo_开启战逃反应_Time = long.MaxValue;
                    myPluginLog("p2_Scmbo_开启战逃反应");
                }

                //延后3个gcd，开启战逃反应
                if (Environment.TickCount64 > p2_Scmbo_开启大宝剑连击_Time)
                {
                    Chat.Instance.SendMessage("/scombo set PLD_ST_AdvancedMode_Confiteor");
                    p2_Scmbo_开启大宝剑连击_Time = long.MaxValue;
                    myPluginLog("p2_Scmbo_开启大宝剑连击");
                }



                if (Environment.TickCount64 > p2_刀光剑舞_铁壁_Time)
                {
                    useAction(铁壁_actionName, 10);
                    p2_刀光剑舞_铁壁_Time = long.MaxValue;
                    myPluginLog(" p2_刀光剑舞_铁壁 铁壁");
                }




                if (Environment.TickCount64 > p2_刀光剑舞_圣盾阵_Time)
                {
                    useAction(盾阵_actionName, 10);
                    Chat.Instance.SendMessage("/scombo set PLD_ST_AdvancedMode_HolySpirit");
                    p2_刀光剑舞_圣盾阵_Time = long.MaxValue;
                    myPluginLog("p2_刀光剑舞_使用盾阵");
                }


                if (Environment.TickCount64 > p2_刀光剑舞_圣光幕帘_Time)
                {
                    useAction(圣光幕帘_actionName, 10);
                    p2_刀光剑舞_圣光幕帘_Time = long.MaxValue;
                    myPluginLog("P2_使用圣光幕帘");
                }


                if (Controller.Scene == 6)
                {
                    if (Environment.TickCount64 > P5_Scmbo_关闭爆发_Time)
                    {
                        disabledScombo();
                        P5_Scmbo_关闭爆发_Time = long.MaxValue;
                        myPluginLog("P5_关闭爆发_Time");
                    }


                    if (Environment.TickCount64 > P5_Scmbo_开启爆发_Time)
                    {
                        ResetScombo();
                        P5_Scmbo_开启爆发_Time = long.MaxValue;
                        myPluginLog("P5_开启爆发_Time");
                    }

                    if (Environment.TickCount64 > P5_3死刑_自动减伤)
                    {
                        useAction(铁壁_actionName, 20, 100);
                        useAction(预警_actionName, 20, 100);
                        Chat.Instance.SendMessage("/e P5_3死刑_自动减伤<se.1>");
                        P5_3死刑_自动减伤 = long.MaxValue;
                        myPluginLog("P5_3死刑_自动减伤");
                    }
                    
                    
                    if (Environment.TickCount64 > P5_转P6关闭盾姿)
                    {
                        关闭盾姿();
                        Chat.Instance.SendMessage("/e P5_转P6关闭盾姿<se.1>");
                        P5_转P6关闭盾姿 = long.MaxValue;
                        myPluginLog("P5_转P6关闭盾姿");
                    }
                    

                    if (Environment.TickCount64 > P5_4死刑_自动减伤)
                    {
                        useAction(铁壁_actionName, 20, 100);
                        // useAction(壁垒_actionName, 20, 100);
                        useAction(预警_actionName, 20, 100);
                        Chat.Instance.SendMessage("/e P5_4死刑_自动减伤<se.1>");
                        P5_4死刑_自动减伤 = long.MaxValue;
                        myPluginLog("P5_4死刑_自动减伤");
                    }

                }




                //P6
                if (Controller.Scene == 7)
                {
                    if (Environment.TickCount64 >= P6_Scmbo_开启爆发_Time)
                    {
                        ResetScombo();
                        P6_Scmbo_开启爆发_Time = long.MaxValue;
                        myPluginLog("P6_Scmbo_开启爆发_Time");
                    }

                    if (Environment.TickCount64 >= P6_雪仇_Time)
                    {
                        useAction(雪仇_actionName, 10, 200);

                        Chat.Instance.SendMessage("/e 雪仇");

                        myPluginLog("P6_雪仇");
                        P6_雪仇_Time = long.MaxValue;
                    }


                    if (Environment.TickCount64 >= P6_宇宙射线_盾阵)
                    {
                        useAction(盾阵_actionName, 20, 100);
                        Chat.Instance.SendMessage("/e 盾阵");
                        P6_宇宙射线_盾阵 = long.MaxValue;
                        myPluginLog("P6_宇宙射线_盾阵");
                    }


                    if (Environment.TickCount64 >= P6_宇宙龙炎_铁壁)
                    {
                        useAction(铁壁_actionName, 20, 100);
                        Chat.Instance.SendMessage("/e 铁壁<se.1>");
                        P6_宇宙龙炎_铁壁 = long.MaxValue;
                        myPluginLog("P6_苍穹龙炎1_开启铁壁");
                    }

                    if (Environment.TickCount64 >= P6_宇宙龙炎_壁垒)
                    {
                        useAction(壁垒_actionName, 20, 100);
                        Chat.Instance.SendMessage("/e 壁垒<se.1>");
                        P6_宇宙龙炎_壁垒 = long.MaxValue;
                        myPluginLog("P6_苍穹龙炎_开启壁垒");
                    }



                    if (Environment.TickCount64 >= P6_宇宙龙炎_盾阵)
                    {
                        useAction(盾阵_actionName, 17, 200);
                        Chat.Instance.SendMessage("/e 盾阵<se.1>");
                        P6_宇宙龙炎_盾阵 = long.MaxValue;
                        myPluginLog("P6_苍穹龙炎_开启盾阵");
                    }


                    if (Environment.TickCount64 >= P6_宇宙龙炎_干预)
                    {
                        useAction(干预2_actionName, 17, 200);
                        Chat.Instance.SendMessage("/e 干预<se.1>");
                        P6_宇宙龙炎_干预 = long.MaxValue;
                        myPluginLog("P6_苍穹龙炎_开启干预");
                    }


                    if (Environment.TickCount64 >= P6_挡枪1_开启无敌)
                    {
                        useAction(无敌_actionName, 20);
                        Chat.Instance.SendMessage("/e 神圣领域<se.1>");
                        P6_挡枪1_开启无敌 = long.MaxValue;
                        myPluginLog("P6_挡枪1_开启无敌");
                    }


                    if (Environment.TickCount64 >= P6_挡枪1_开启干预)
                    {
                        useAction(干预2_actionName, 20, 100);
                        Chat.Instance.SendMessage("/e 干预<se.1>");
                        P6_挡枪1_开启干预 = long.MaxValue;
                        myPluginLog("P6_挡枪1_开启干预");
                    }


                    if (Environment.TickCount64 >= P6_幕帘_Time)
                    {
                        useAction(圣光幕帘_actionName, 20, 100);
                        Chat.Instance.SendMessage("/e 圣光幕帘<se.1>");
                        P6_幕帘_Time = long.MaxValue;
                        myPluginLog("P6_幕帘");
                    }





                    if (Environment.TickCount64 >= P6_挡枪2_开启预警)
                    {
                        useAction(预警_actionName, 40, 100);
                        Chat.Instance.SendMessage("/e 预警<se.1>");
                        P6_挡枪2_开启预警 = long.MaxValue;
                        myPluginLog("P6_挡枪2_开启预警");
                    }

                    if (Environment.TickCount64 >= P6_挡枪2_开启盾阵)
                    {
                        useAction(盾阵_actionName, 20, 100);
                        Chat.Instance.SendMessage("/e 盾阵<se.1>");
                        P6_挡枪2_开启盾阵 = long.MaxValue;
                        myPluginLog("P6_挡枪2_开启盾阵");
                    }


                    if (Environment.TickCount64 > P6_挡枪2_关闭自动输出)
                    {
                        Chat.Instance.SendMessage("/scombo auto 0");
                        P6_挡枪2_关闭自动输出 = long.MaxValue;
                        myPluginLog("P6_挡枪2_关闭自动输出");
                    }


                    if (Environment.TickCount64 >= P6_流星_支援减伤_第一波)
                    {
                        useAction(干预8_actionName, 20, 100);
                        Chat.Instance.SendMessage($"/e {干预8_actionName}<se.1>");
                        myPluginLog($"P6_流星_支援减伤_第一波");
                        P6_流星_支援减伤_第一波 = long.MaxValue;
                    }



                    if (Environment.TickCount64 >= P6_流星_支援减伤_第二波)
                    {
                        useAction(干预7_actionName, 20, 100);
                        Chat.Instance.SendMessage($"/e {干预7_actionName}<se.1>");
                        myPluginLog($"P6_流星_支援减伤_第二波");
                        P6_流星_支援减伤_第二波 = long.MaxValue;
                    }


                    if (Environment.TickCount64 >= P6_极限技)
                    {
                        useAction(极限技_actionName, 10, 100);
                        Chat.Instance.SendMessage($"/e {极限技_actionName}<se.1>");
                        myPluginLog($"P6极限技");
                        P6_极限技 = long.MaxValue;
                    }


                }


            }
        }

        public override void OnDirectorUpdate(DirectorUpdateCategory category)
        {
            var localPlaye = Svc.ClientState.LocalPlayer;
            if (localPlaye == null) return;
            if (localPlaye.ClassJob.Id != jobId)
            {
                return;
            }

            if (category.EqualsAny(DirectorUpdateCategory.Commence, DirectorUpdateCategory.Recommence, DirectorUpdateCategory.Wipe))
            {
                Reset();
            }
        }


        private void 开启盾姿()
        {
     
            
            var localPlaye = Svc.ClientState.LocalPlayer;
            if (localPlaye == null) return;

            Task.Run(async delegate
            {
                for (int i = 0; i < 10; i++)
                {
                    Svc.Framework.RunOnFrameworkThread(() =>
                    {
                        var isBuff = localPlaye.StatusList.Any(status => status.StatusId == 盾姿BUFFID);
                        if (!isBuff)
                        {
                            Chat.Instance.SendMessage($"/ac {钢铁信念_actionName}");
                            myPluginLog("开启盾姿");
                        }
                    });
                    await Task.Delay(TimeSpan.FromMilliseconds(1000));
                }
            });

        }
        
        private void 关闭盾姿()
        {
            
            var localPlaye = Svc.ClientState.LocalPlayer;
            if (localPlaye == null) return;

            Task.Run(async delegate
            {
                for (int i = 0; i < 10; i++)
                {
                    Svc.Framework.RunOnFrameworkThread(() =>
                    {
                        var isBuff = localPlaye.StatusList.Any(status => status.StatusId == 盾姿BUFFID);
                        if (isBuff)
                        {
                            Chat.Instance.SendMessage($"/ac {钢铁信念_actionName}");
                            myPluginLog("关闭盾姿");
                        }
                    });
                    await Task.Delay(TimeSpan.FromMilliseconds(1000));
                }
            });
        }
        
        




        public override void OnMapEffect(uint position, ushort data1, ushort data2)
        {
            var localPlaye = Svc.ClientState.LocalPlayer;
            if (localPlaye == null) return;
            if (localPlaye.ClassJob.Id != jobId)
            {
                return;
            }



            if (Controller.InCombat == false)
                return;

            // if (Controller.Scene == 6)
            {
                
                if (position == 10 && data1 == 1 && data2 == 2)
                {
                    p2_开场_Time = Environment.TickCount64;
                    p2_开场_战逃反应_Time = p2_开场_Time + 2 * 1000;
                    useAction(铁壁_actionName, 20, 100);


                    myPluginLog("P2 开场");
                }
                
                if (position == 20 && data1 == 4 && data2 == 8)
                {
                    myPluginLog("进入P5");

                    if (Controller.GetConfig<Config>().P5_3死刑_自动减伤)
                    {
                        P5_3死刑_自动减伤 = Environment.TickCount64 + 183 * 1000;
                    }    
                    
                    if (Controller.GetConfig<Config>().ST)
                    {
                        P5_转P6关闭盾姿 = Environment.TickCount64 + 290 * 1000;
                    }

                    if (Controller.GetConfig<Config>().P5_4死刑_自动减伤)
                    {
                        P5_4死刑_自动减伤 = Environment.TickCount64 + 267 * 1000;
                    }
                }
            }

            if (Controller.Scene == 7)
            {
                myPluginLog("进入P6");
                if (position == 0 && data1 == 1 && data2 == 2)
                {
                    {
                        
                        if (Controller.GetConfig<Config>().ST)
                        {
                            关闭盾姿();
                        }

                        
                        //最近敌人
                        SendMessageLooP("/tenemy");

                        disabledScombo();
                        Chat.Instance.SendMessage("/scombo auto 9");

                        P6_Time = Environment.TickCount64;

                        P6_Scmbo_开启爆发_Time = Environment.TickCount64 + 7 * 1000 + 500;
                    }

                }
            }

        }



        void Reset()
        {



            p1_无敌_Time = long.MaxValue;
            p1_全能之主读条Time = long.MaxValue;


            p2_开场_Time = long.MaxValue;
            p2_开场_战逃反应_Time = long.MaxValue;
            p2_Scmbo_开启战逃反应_Time = long.MaxValue;
            p2_Scmbo_开启大宝剑连击_Time = long.MaxValue;

            p2_刀光剑舞_铁壁_Time = long.MaxValue;
            p2_刀光剑舞_圣盾阵_Time = long.MaxValue;
            p2_刀光剑舞_圣光幕帘_Time = long.MaxValue;


            P5_二运读条_Time = long.MaxValue;
            P5_Scmbo_关闭爆发_Time = long.MaxValue;

            P5_3死刑_自动减伤 = long.MaxValue;
            P5_4死刑_自动减伤 = long.MaxValue;
            P5_转P6关闭盾姿 = long.MaxValue;


            P5_Scmbo_开启爆发_Time = long.MaxValue;

            P6_Time = long.MaxValue;
            P6_Scmbo_开启爆发_Time = long.MaxValue;

            P6_宇宙射线_盾阵 = long.MaxValue;

            P6_宇宙龙炎_铁壁 = long.MaxValue;
            P6_宇宙龙炎_壁垒 = long.MaxValue;

            P6_宇宙龙炎_盾阵 = long.MaxValue;
            P6_宇宙龙炎_干预 = long.MaxValue;

            P6_挡枪1_开启无敌 = long.MaxValue;
            P6_挡枪1_开启干预 = long.MaxValue;
            P6_幕帘_Time = long.MaxValue;


            P6_挡枪2_开启预警 = long.MaxValue;
            P6_挡枪2_开启盾阵 = long.MaxValue;
            P6_挡枪2_关闭自动输出 = long.MaxValue;

            P6_宇宙记忆_开启雪仇 = long.MaxValue;

            P6_流星_支援减伤_第一波 = long.MaxValue;
            P6_流星_支援减伤_第二波 = long.MaxValue;

            P6_极限技 = long.MaxValue;

            P6_雪仇_Time = long.MaxValue;
            P6_波动炮_开启雪仇 = long.MaxValue;

            波动炮次数 = 0;
            魔数次数 = 0;

            ResetScombo();
        }



        void ResetScombo()
        {
            Chat.Instance.SendMessage("/scombo set PLD_ST_AdvancedMode_FoF");
            Chat.Instance.SendMessage("/scombo set PLD_ST_AdvancedMode_Requiescat");
            Chat.Instance.SendMessage("/scombo set PLD_ST_AdvancedMode_GoringBlade");
            Chat.Instance.SendMessage("/scombo set PLD_ST_AdvancedMode_Confiteor");
            Chat.Instance.SendMessage("/scombo set PLD_ST_AdvancedMode_HolySpirit");

            Chat.Instance.SendMessage("/scombo set PLD_AoE_AdvancedMode_FoF");
            Chat.Instance.SendMessage("/scombo set PLD_AoE_AdvancedMode_Requiescat");
        }
        void disabledScombo()
        {
            Chat.Instance.SendMessage("/scombo unset PLD_ST_AdvancedMode_FoF");
            Chat.Instance.SendMessage("/scombo unset PLD_ST_AdvancedMode_Requiescat");
            Chat.Instance.SendMessage("/scombo unset PLD_ST_AdvancedMode_GoringBlade");
            Chat.Instance.SendMessage("/scombo unset PLD_ST_AdvancedMode_Confiteor");
            Chat.Instance.SendMessage("/scombo unset PLD_AoE_AdvancedMode_FoF");
            Chat.Instance.SendMessage("/scombo unset PLD_AoE_AdvancedMode_Requiescat");
        }

        void myPluginLog(string text)
        {
            PluginLog.Information($"OMG_PLD_SC {text}");
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
            ImGui.Checkbox("ST", ref this.Controller.GetConfig<Config>().ST);
            ImGui.Checkbox("P2延后爆发", ref this.Controller.GetConfig<Config>().P2延后爆发);
            ImGui.Checkbox("P5_3死刑_自动减伤", ref this.Controller.GetConfig<Config>().P5_3死刑_自动减伤);
            ImGui.Checkbox("P5_4死刑_自动减伤", ref this.Controller.GetConfig<Config>().P5_4死刑_自动减伤);


            ImGui.Checkbox("P6宇宙记忆雪仇", ref this.Controller.GetConfig<Config>().P6宇宙记忆雪仇);
            ImGui.Checkbox("P6幕帘_1挡枪", ref this.Controller.GetConfig<Config>().P6幕帘_1挡枪);
            ImGui.Checkbox("P6幕帘_陨石", ref this.Controller.GetConfig<Config>().P6幕帘_陨石);

            ImGui.Checkbox("P6_宇宙记忆1LB", ref this.Controller.GetConfig<Config>().P6_宇宙记忆1LB);
            ImGui.Checkbox("P6_魔数1LB", ref this.Controller.GetConfig<Config>().P6_魔数1LB);
            ImGui.Checkbox("P6_魔数2LB", ref this.Controller.GetConfig<Config>().P6_魔数2LB);
        }

        public class Config : IEzConfig
        {
            public bool ST = true;
            public bool P2延后爆发 = true;
            public bool P5_3死刑_自动减伤 = false;
            public bool P5_4死刑_自动减伤 = false;


            public bool P6幕帘_1挡枪 = true;
            public bool P6幕帘_陨石 = true;
            public bool P6宇宙记忆雪仇 = true;

            public bool P6_宇宙记忆1LB = false;
            public bool P6_魔数1LB = false;
            public bool P6_魔数2LB = true;

        }


    }
}