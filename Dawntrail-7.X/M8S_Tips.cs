using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ECommons;
using ECommons.Automation;
using ECommons.Configuration;
using ECommons.Hooks;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using Splatoon.SplatoonScripting;

namespace SplatoonScriptsOfficial.Duties.Dawntrail
{
    internal class M8S_Tips : SplatoonScript
    {
        public override HashSet<uint> ValidTerritories => new() { 1263 };

        public override Metadata? Metadata => new(11, "M8S门神钢铁还是扇形");

        private bool isInit = false;


        //小于P0马上输出，否则延后输出
        long P0_Time = long.MaxValue;
        long P1_Time = long.MaxValue;
        long P2_Time = long.MaxValue;
        long P3_Time = long.MaxValue;
        string 待输出的文本 = string.Empty;


        public override void OnMessage(string Message)
        {
            if (Message.Contains("Battle commencing in 5 seconds!"))
            {
                P0_Time = Environment.TickCount64 + 60 * 4 * 1000 + 1 * 1000;
                P1_Time = Environment.TickCount64 + 60 * 5 * 1000 - 4 * 1000;
                P2_Time = Environment.TickCount64 + 60 * 6 * 1000 + 26 * 1000;
                P3_Time = Environment.TickCount64 + 60 * 7 * 1000 + 50 * 1000;

                if (Controller.GetConfig<Config>().Debug)
                {
                    Chat.Instance.SendMessage("/e Debug 文本开始倒计时");
                    PluginLog.LogError($"我的日志啊{DateTime.Now}-开场倒计时");
                }
            }

            if (Message.Contains("(13843>43312)"))
            {
                待输出的文本 = Controller.GetConfig<Config>().扇形文本;
                即使输出();
                if (Controller.GetConfig<Config>().Debug)
                {
                    Chat.Instance.SendMessage("/e Debug" + 待输出的文本);
                }

            }


            if (Message.Contains("(13843>43313)"))
            {
                
                待输出的文本 = Controller.GetConfig<Config>().钢铁文本;
                即使输出();
                if (Controller.GetConfig<Config>().Debug)
                {
                    Chat.Instance.SendMessage("/e Debug" + 待输出的文本);
                }

            }

        }

        private void 即使输出()
        {
            if (Controller.GetConfig<Config>().是否输出到小队)
            {
                Chat.Instance.SendMessage("/p " + 待输出的文本);
            }
            else
            {
                Chat.Instance.SendMessage("/e " + 待输出的文本);
            }

        }



        public override void OnDirectorUpdate(DirectorUpdateCategory category)
        {

            if (category.EqualsAny(DirectorUpdateCategory.Commence, DirectorUpdateCategory.Recommence, DirectorUpdateCategory.Wipe))
            {
                Reset();
            }
        }

        public override void OnUpdate()
        {
            if (!isInit && Countdown.TimeRemaining() <= 4)
            {
                init();
            }
        }

        private void init()
        {
            if (Controller.GetConfig<Config>().Debug)
            {
                Chat.Instance.SendMessage("/e Debug 内存开始倒计时");
                PluginLog.LogError($"我的日志啊{DateTime.Now}-开场倒计时");
            }

            P0_Time = Environment.TickCount64 + 60 * 4 * 1000 + 1 * 1000;
            P1_Time = Environment.TickCount64 + 60 * 5 * 1000 - 4 * 1000;
            P2_Time = Environment.TickCount64 + 60 * 6 * 1000 + 26 * 1000;
            P3_Time = Environment.TickCount64 + 60 * 7 * 1000 + 50 * 1000;

            isInit = true;
        }

        public override void OnCombatEnd()
        {
            Reset();
        }


        void Reset()
        {
            isInit = false;
            P0_Time = long.MaxValue;
            P1_Time = long.MaxValue;
            P2_Time = long.MaxValue;
            P3_Time = long.MaxValue;

            待输出的文本 = String.Empty;
        }



        public override void OnSettingsDraw()
        {
            ImGui.Checkbox("Debug", ref this.Controller.GetConfig<Config>().Debug);
            ImGui.Checkbox("是否输出到小队", ref this.Controller.GetConfig<Config>().是否输出到小队);
            ImGui.InputText("分散文本", ref this.Controller.GetConfig<Config>().扇形文本, 100);
            ImGui.InputText("分摊文本", ref this.Controller.GetConfig<Config>().钢铁文本, 100);

            if (Controller.GetConfig<Config>().Debug)
            {
                ImGui.Text("DEBUG");
                ImGui.Text("当前时间" + Environment.TickCount64);
                ImGui.Text("P1时间" + P1_Time);
                ImGui.Text("P2时间" + P2_Time);
                ImGui.Text("P3时间" + P3_Time);
                ImGui.Text("待输出文本" + 待输出的文本);
            }

        }

        public class Config : IEzConfig
        {
            public bool 是否输出到小队 = false;
            public bool Debug = false;
            public string 扇形文本 = "Extraplanar";
            public string 钢铁文本 = "Revolutionary";
        }

        [StructLayout(LayoutKind.Explicit)]
        public unsafe struct Countdown
        {
            [FieldOffset(0x28)]
            public float Timer;

            [FieldOffset(0x38)]
            public byte Active;

            [FieldOffset(0x3C)]
            public uint Initiator;

            public static unsafe Countdown* Instance
                => (Countdown*)Framework.Instance()->GetUIModule()->GetAgentModule()->GetAgentByInternalId
                (
                    AgentId.CountDownSettingDialog
                );

            public static float? TimeRemaining()
            {
                var inst = Instance;
                return inst->Active != 0 ? inst->Timer : null;
            }
        }
    }
}