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

namespace SplatoonScriptsOfficial.Duties.Endwalker
{
    internal class DSR_DNC_SC : SplatoonScript
    {
        public override HashSet<uint> ValidTerritories => new() { 968 };
        public override Metadata? Metadata => new(4, nameof(DSR_DNC_SC));

        private uint JobID = 38;

      
        long P7_开启120_Time = long.MaxValue;
       
        
        public override void OnMessage(string Message)
        {
            var localPlaye = Svc.ClientState.LocalPlayer;
            if (localPlaye == null) return;
            if (localPlaye.ClassJob.RowId != JobID)
            {
                return;
            }

            //P6 十字火关闭120
            if (Controller.Scene == 2)
            {
                if (Message.Contains("(3458>27974)"))
                {
                    myPluginLog($"P6 十字火关闭120");
                    关闭120爆发();
                }
  
            }
           
            if (Controller.Scene == 11)
            {
                if (Message.Contains("龙威骑神托尔丹发动了异史终结。"))
                {
                    关闭120爆发();
                }  
            }
          


            if (Controller.Scene == 8)
            {
                if (Message.Contains("龙威骑神托尔丹正在发动十亿核爆剑。"))
                {
                    P7_开启120_Time = Environment.TickCount64 + 10 * 1000;
                }  
            }
    
        }

        public override void OnUpdate()
        {
            if (Environment.TickCount64 >= P7_开启120_Time)
            {
                ResetScombo();
                myPluginLog($"P7开启120");
                P7_开启120_Time = long.MaxValue;
            }
            

        }
        public override void OnDirectorUpdate(DirectorUpdateCategory category)
        {
            var localPlaye = Svc.ClientState.LocalPlayer;
            if (localPlaye == null) return;
            if (localPlaye.ClassJob.RowId != JobID)
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
            P7_开启120_Time = long.MaxValue;
            ResetScombo();
        }

        void ResetScombo()
        {
            Chat.Instance.SendMessage("/scombo set DNC_DT_Simple_SS");
            Chat.Instance.SendMessage("/scombo set DNC_DT_Simple_TS");
            Chat.Instance.SendMessage("/scombo set DNC_DT_Simple_Flourish");
            Chat.Instance.SendMessage("/scombo set DNC_DT_Simple_Devilment");
        }

        
        void 关闭120爆发()
        {
            Chat.Instance.SendMessage("/scombo unset DNC_DT_Simple_TS");
            Chat.Instance.SendMessage("/scombo unset DNC_DT_Simple_Devilment");
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
            PluginLog.Information($"{nameof(DSR_DNC_SC)} {text}");
            if (Controller.GetConfig<Config>().输出到聊天框)
            {
                Chat.Instance.SendMessage($"/e {text}");
            }

        }
    }
}