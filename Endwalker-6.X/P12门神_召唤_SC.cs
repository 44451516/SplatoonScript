using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ECommons;
using ECommons.Automation;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.Hooks;
using ECommons.Logging;
using ImGuiNET;
using Splatoon.SplatoonScripting;

/*
UserConfig.DrawHorizontalRadioButton(SMN.Config.召唤顺序, "土风火", "按泰坦，迦楼罗，伊芙利特的顺序召唤", 1);
UserConfig.DrawHorizontalRadioButton(SMN.Config.召唤顺序, "土火风", "按迦楼罗，泰坦，伊芙利特的顺序召唤.", 2);
                
UserConfig.DrawHorizontalRadioButton(SMN.Config.召唤顺序, "风土火", "按迦楼罗，泰坦，伊芙利特的顺序召唤.", 3);
UserConfig.DrawHorizontalRadioButton(SMN.Config.召唤顺序, "风火土", "按迦楼罗，泰坦，伊芙利特的顺序召唤.", 4);
                
UserConfig.DrawHorizontalRadioButton(SMN.Config.召唤顺序, "火风土", "按迦楼罗，泰坦，伊芙利特的顺序召唤.", 5);
UserConfig.DrawHorizontalRadioButton(SMN.Config.召唤顺序, "火土风", "按迦楼罗，泰坦，伊芙利特的顺序召唤.", 6);
*/
namespace SplatoonScriptsOfficial.Duties.Endwalker
{
    internal class P12门神_召唤_SC : SplatoonScript
    {
        public override HashSet<uint> ValidTerritories => new() { 1154 };
        public override Metadata? Metadata => new(10, nameof(P12门神_召唤_SC));

        private uint JobID = 27;

        long P1_Time = long.MaxValue;
        long P2_Time = long.MaxValue;
        long P3_Time = long.MaxValue;
        long P4_Time = long.MaxValue;
        long P5_Time = long.MaxValue;
        long P6_Time = long.MaxValue;
        long P6_28_Time = long.MaxValue;
        long P7_Time = long.MaxValue;
        long P8_Time = long.MaxValue;
        long P9_Time = long.MaxValue;




        public override void OnMessage(string Message)
        {
            var localPlaye = Svc.ClientState.LocalPlayer;
            if (localPlaye == null) return;
            if (localPlaye.ClassJob.Id != JobID)
            {
                return;
            }

            if (Controller.Phase == 1)
            {

                if (Message.Contains("距离战斗开始还有5秒！"))
                {
                    //火风土
                    Chat.Instance.SendMessage("/scombo set_custom_int_value SMN_PrimalChoice 5");

                    P1_Time = Environment.TickCount64 + 60 * 1000;
                    P2_Time = Environment.TickCount64 + 60 * 2 * 1000;
                    P3_Time = Environment.TickCount64 + 60 * 3 * 1000;
                    P4_Time = Environment.TickCount64 + 60 * 4 * 1000;
                    P5_Time = Environment.TickCount64 + 60 * 5 * 1000 + 25 * 1000;
                    P6_Time = Environment.TickCount64 + 60 * 6 * 1000 + 25 * 1000;

                    //倒计时5秒 5*1000 6分28秒开启灼热之光
                    P6_28_Time = Environment.TickCount64 + 60 * 6 * 1000 + 5 * 1000 + 0 * 1000;


                    P7_Time = Environment.TickCount64 + 60 * 7 * 1000 + 25 * 1000;
                    P8_Time = Environment.TickCount64 + 60 * 8 * 1000 + 25 * 1000;
                    P9_Time = Environment.TickCount64 + 60 * 9 * 1000 + 25 * 1000;
                }
            }
        }

        public override void OnUpdate()
        {

            if (Controller.Phase == 1)
            {
                if (Environment.TickCount64 >= P1_Time)
                {
                    Chat.Instance.SendMessage("/scombo set_custom_int_value SMN_PrimalChoice 2");
                    myPluginLog($"土火风");
                    P1_Time = long.MaxValue;
                }


                if (Environment.TickCount64 >= P3_Time)
                {
                    Chat.Instance.SendMessage("/scombo set_custom_int_value SMN_PrimalChoice 1");
                    myPluginLog($"土风火");
                    P3_Time = long.MaxValue;
                }


                if (Environment.TickCount64 >= P4_Time)
                {
                    Chat.Instance.SendMessage("/scombo set_custom_int_value SMN_PrimalChoice 4");
                    myPluginLog($"风火土");
                    P4_Time = long.MaxValue;
                }


                if (Environment.TickCount64 >= P5_Time)
                {
                    Chat.Instance.SendMessage("/scombo set_custom_int_value SMN_PrimalChoice 5");
                    myPluginLog($"火风土");
                    Chat.Instance.SendMessage("/scombo unset SMN_SearingLight");
                    P5_Time = long.MaxValue;
                }


                if (Environment.TickCount64 >= P6_Time)
                {
                    Chat.Instance.SendMessage("/scombo set_custom_int_value SMN_PrimalChoice 5");
                    myPluginLog($"火风土");
                    P6_Time = long.MaxValue;
                }

                if (Environment.TickCount64 >= P6_28_Time)
                {
                    Chat.Instance.SendMessage("/scombo set SMN_SearingLight");
                    myPluginLog($"开启灼热之光");
                    P6_28_Time = long.MaxValue;
                }
            }





        }
        public override void OnDirectorUpdate(DirectorUpdateCategory category)
        {
            var localPlaye = Svc.ClientState.LocalPlayer;
            if (localPlaye == null) return;
            if (localPlaye.ClassJob.Id != JobID)
            {
                return;
            }

            if (category.EqualsAny(DirectorUpdateCategory.Commence, DirectorUpdateCategory.Recommence, DirectorUpdateCategory.Wipe))
            {
                Reset();
            }
        }



        public override void OnSettingsDraw()
        {
            ImGui.Checkbox("输出到聊天框", ref this.Controller.GetConfig<Config>().输出到聊天框);
        }

        public class Config : IEzConfig
        {
            public bool 输出到聊天框 = true;
        }

        void Reset()
        {
            P1_Time = long.MaxValue;
            P2_Time = long.MaxValue;
            P3_Time = long.MaxValue;
            P4_Time = long.MaxValue;
            P5_Time = long.MaxValue;
            P6_Time = long.MaxValue;
            P6_28_Time = long.MaxValue;
            P7_Time = long.MaxValue;
            P8_Time = long.MaxValue;
            P9_Time = long.MaxValue;
            ResetScombo();
        }

        void ResetScombo()
        {
            Chat.Instance.SendMessage("/scombo set_custom_int_value SMN_PrimalChoice 5");
            Chat.Instance.SendMessage("/scombo set SMN_SearingLight");
        }

        void disabledScombo()
        {
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

        void myPluginLog(string text)
        {
            PluginLog.Information($"{nameof(P12门神_召唤_SC)} {text}");
            if (Controller.GetConfig<Config>().输出到聊天框)
            {
                Chat.Instance.SendMessage($"/e {text}");
            }

        }
    }
}