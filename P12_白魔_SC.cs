using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.Hooks;
using ECommons.Logging;
using Splatoon.SplatoonScripting;

namespace SplatoonScriptsOfficial.Duties.Endwalker
{
    internal class P12_白魔_SC : SplatoonScript
    {
        public override HashSet<uint> ValidTerritories => new() { 1154 };
        public override Metadata? Metadata => new(2, "P12_白魔_SC");
        
        
        long DOT开启_Time = long.MaxValue;
        long 节制开启_Time = long.MaxValue;
        
        private const string 节制_ActionName = "节制";
        

        private uint JobID = 24;
        public override void OnMessage(string Message)
        {
            var localPlaye = Svc.ClientState.LocalPlayer;
            if (localPlaye == null) return;
            if (localPlaye.ClassJob.Id != JobID)
            {
                return;
            }
          
            if (Controller.Scene == 2)
            {
                
                if (Message.Contains("这只是神之力的冰山一角！"))
                {
                    myPluginLog($"关闭连击");
                    disabledScombo();
                    DOT开启_Time=Environment.TickCount64 + 60 * 1000 ;
                    节制开启_Time=Environment.TickCount64 + 40 * 1000 ;
                }
                
            }
        }

        public override void OnUpdate()
        {
            if (Environment.TickCount64 >= DOT开启_Time)
            {
                myPluginLog($"开启连击");
                ResetScombo();
                DOT开启_Time = long.MaxValue;
            } 
            
            if (Environment.TickCount64 >= 节制开启_Time)
            {
                myPluginLog($"自动翅膀");
                useAction(节制_ActionName, 30,200);
                节制开启_Time = long.MaxValue;
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


       




        


        void Reset()
        {
            DOT开启_Time = long.MaxValue;
            节制开启_Time = long.MaxValue;
            ResetScombo();
        }
        
        void ResetScombo()
        {
            Chat.Instance.SendMessage("/scombo set WHM_ST_MainCombo_DoT");
        }
        void disabledScombo()
        {
            Chat.Instance.SendMessage("/scombo unset WHM_ST_MainCombo_DoT");
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
            PluginLog.Information($"{nameof(P12_白魔_SC)} {text}");
        }


        

    }
}