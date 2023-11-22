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
    internal class P10_召唤_SC : SplatoonScript
    {
        public override HashSet<uint> ValidTerritories => new() { 1150 };
        // public override HashSet<uint> ValidTerritories => new() { 949 };
        public override Metadata? Metadata => new(3, "P10_召唤_SC");

        private uint JobID = 27;

        long P1_Time = long.MaxValue;
        long P2_Time = long.MaxValue;
        long P3_Time = long.MaxValue;
        long P4_Time = long.MaxValue;
        long P5_Time = long.MaxValue;
        long P6_Time = long.MaxValue;
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

            if (Message.Contains("距离战斗开始还有10秒！"))
            {
                //土火风
                Chat.Instance.SendMessage("/scombo set_custom_int_value SMN_PrimalChoice 2");

                P1_Time = Environment.TickCount64 + 60 * 1000;
                P2_Time = Environment.TickCount64 + 60 * 2 * 1000;
                P3_Time = Environment.TickCount64 + 60 * 3 * 1000;
                P4_Time = Environment.TickCount64 + 60 * 4 * 1000;
                P5_Time = Environment.TickCount64 + 60 * 5 * 1000;
                P6_Time = Environment.TickCount64 + 60 * 6 * 1000;
                P7_Time = Environment.TickCount64 + 60 * 7 * 1000;
                P8_Time = Environment.TickCount64 + 60 * 8 * 1000;
                P9_Time = Environment.TickCount64 + 60 * 9 * 1000;
            }
        }

        public override void OnUpdate()
        {
            if (Environment.TickCount64 >= P1_Time)
            {
                Chat.Instance.SendMessage("/scombo set_custom_int_value SMN_PrimalChoice 1");
                myPluginLog($"土风火P1");
                P1_Time = long.MaxValue;
            }



            if (Environment.TickCount64 >= P4_Time)
            {
                Chat.Instance.SendMessage("/scombo set_custom_int_value SMN_PrimalChoice 2");
                myPluginLog($"土火风P4");
                P4_Time = long.MaxValue;
            }
            
            if (Environment.TickCount64 >= P6_Time)
            {
                Chat.Instance.SendMessage("/scombo set_custom_int_value SMN_PrimalChoice 5");
                myPluginLog($"火风土P6");
                P6_Time = long.MaxValue;
            }

            if (Environment.TickCount64 >= P7_Time)
            {
                Chat.Instance.SendMessage("/scombo set_custom_int_value SMN_PrimalChoice 1");
                myPluginLog($"土风火P7");
                P7_Time = long.MaxValue;
            }
            
            if (Environment.TickCount64 >= P9_Time)
            {
                Chat.Instance.SendMessage("/scombo set_custom_int_value SMN_PrimalChoice 5");
                myPluginLog($"火风土P9");
                P9_Time = long.MaxValue;
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
            P7_Time = long.MaxValue;
            P8_Time = long.MaxValue;
            P9_Time = long.MaxValue;
            ResetScombo();
        }

        void ResetScombo()
        {
            Chat.Instance.SendMessage("/scombo set_custom_int_value SMN_PrimalChoice 2");
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
            PluginLog.Information($"{nameof(P10_召唤_SC)} {text}");
            if (Controller.GetConfig<Config>().输出到聊天框)
            {
                Chat.Instance.SendMessage($"/e {text}");
            }

        }
    }
}