using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Colors;
using ECommons;
using ECommons.Automation;
using ECommons.ChatMethods;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using ECommons.Hooks.ActionEffectTypes;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using ECommons.MathHelpers;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;
using Splatoon.SplatoonScripting;
using Splatoon.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Status = Lumina.Excel.Sheets.Status;

namespace SplatoonScriptsOfficial.Duties.Dawntrail.Dancing_Mad;

public class P4_Debuff_Reminder : SplatoonScript<P4_Debuff_Reminder.Config>
{
    public override Metadata Metadata { get; } = new(15, "P4汉化魔改版");
    public override HashSet<uint>? ValidTerritories { get; } = [1363];

    private List<string> VfxLie = ["vfx/common/eff/z3oy_stlp6_c0c.avfx", "vfx/common/eff/z3oy_stlp4_c0c.avfx"];
    private List<string> VfxTruth = ["vfx/common/eff/z3oy_stlp7_c0c.avfx", "vfx/common/eff/z3oy_stlp5_c0c.avfx"];
    private record struct StatusInfo(uint objectId, uint statusId);
    private record struct EchoReminder(string Text, uint PlayerId, uint StatusId, string JobName, long TriggerAtMs, float Threshold);
    private List<StatusInfo> FakeStatuses = [];
    private List<EchoReminder> PendingEchoReminders = [];
    private Dictionary<string, long> ScheduledEchoReminderChecks = [];
    private const long EchoReminderBatchWindowMs = 1500;
    private const int EchoReminderRetryDelayMs = 500;
    private const float EchoReminderRemainingWindow = 1.5f;

    private const uint TargetIconCommand = 34;
    private const uint MarkerFakeIce = 0x02A3;
    private const uint MarkerTrueIce = 0x02A4;
    private const uint MarkerFakeThunder = 0x02A5;
    private const uint MarkerTrueThunder = 0x02A6;
    private const uint CastNoFloodFakePurpleBlue = 0xC3A1;
    private const uint CastNoFloodFakeBluePurple = 0xC3A2;
    private const uint CastNoFloodTruePurpleBlue = 0xC392;
    private const uint CastNoFloodTrueBluePurple = 0xC393;

    public class Debuffs
    {
        /// <summary>
        /// becomes Move
        /// </summary>
        public static uint[] DebuffDontMove = [5546, 1072, 1384, 2657, 3793, 3802, 4144];
        /// <summary>
        /// becomes Look at person
        /// </summary>
        public static uint[] DebuffLookAway = [5543, 452];
        /// <summary>
        /// becomes Spread
        /// </summary>
        public static uint[] DebuffStack = [1023, 5545, 2142];
        /// <summary>
        /// becomes Stack
        /// </summary>
        public static uint[] DebuffSpread = [587, 3799, 5544];
        /// <summary>
        /// becomes Donut
        /// </summary>
        public static uint[] DebuffFireSpread = [1600, 5547];
        /// <summary>
        /// becomes Fire Spread
        /// </summary>
        public static uint[] DebuffDonut = [1601, 5548];
        /// <summary>
        /// must pass mechanics
        /// </summary>
        public static uint DebuffLive = 454;
        /// <summary>
        /// must fail mechanics
        /// </summary>
        public static uint[] DebuffDie = [1382, 5464];
        /// <summary>
        /// Life wound: true = blue, false = purple
        /// </summary>
        public static uint[] DebuffWhitewould = [4887, 5541];
        /// <summary>
        /// Death wound: true = purple, false = blue
        /// </summary>
        public static uint[] DebuffBlackwound = [4888, 5542];
        /// <summary>
        /// No-flood life wound.
        /// </summary>
        public static uint[] NoFloodLifeWound = [1317, 0x15A5, 4887];
        /// <summary>
        /// No-flood death wound.
        /// </summary>
        public static uint[] NoFloodDeathWound = [1318, 0x15A6, 4888];
    }

    private record struct P4MagicStorage(bool? FakeIce, bool? FakeThunder);
    private bool? P4FakeIce = null;
    private bool? P4FakeThunder = null;
    private P4MagicStorage? P4StoredMagic = null;
    private string? P4MagicReleaseText = null;
    private uint? P4CurrentNoFloodCastId = null;
    private Dictionary<uint, bool> IsTruth = [];
    private record struct NoFloodPartyInfo(uint? ColorStatusId, bool? ColorIsFake, uint? LifeStatusId, bool? LifeIsFake, uint JobId);
    private Dictionary<uint, NoFloodPartyInfo> P4NoFloodPartyInfo = [];
    private HashSet<string> P4NoFloodReportedDirections = [];

    private void DebugEcho(string text)
    {
        if(!C.OutputDebugEcho) return;
        PluginLog.Information($"[P4DBG] {text}");
    }

    private string ChatHintCommand => C.ChatOutputChannel switch
    {
        1 => "/p",
        2 => string.IsNullOrWhiteSpace(C.CustomChatPrefix) ? "/e" : C.CustomChatPrefix.Trim(),
        _ => "/e",
    };

    private bool SendChatHint(string text)
    {
        if(!C.OutputTimedEchoReminder)
        {
            DebugEcho($"聊天提示跳过：开关关闭 text={text}");
            return false;
        }

        Chat.Instance.ExecuteCommand($"{ChatHintCommand} {text}");
        return true;
    }

    private static string Hex(uint value) => $"0x{value:X}";
    private static string MagicBool(bool? value) => value.HasValue ? (value.Value ? "假" : "真") : "null";
    private string MagicState() => $"当前 冰={MagicBool(P4FakeIce)} 雷={MagicBool(P4FakeThunder)} / 储存 冰={MagicBool(P4StoredMagic?.FakeIce)} 雷={MagicBool(P4StoredMagic?.FakeThunder)}";
    private string PlayerName(uint objectId)
    {
        if(objectId.TryGetPlayer(out var pc)) return $"{GetChineseJobName(pc.ClassJob.RowId)}:{pc.Name}";
        return Hex(objectId);
    }
    public List<uint> DebuffList
    {
        get
        {
            if(field == null)
            {
                field = [];
                foreach(var x in typeof(Debuffs).GetFields().Select(x => x.GetValue(null)!))
                {
                    if(x is uint u)
                    {
                        field.Add(u);
                    }
                    if(x is uint[] u2)
                    {
                        field.Add(u2);
                    }
                }
            }
            return field;
        }
    }

    public override void OnSetup()
    {
    }

    private static long NowMs() => Environment.TickCount64;

    private void ScheduleEchoReminderCheck(string text, int delayMs)
    {
        delayMs = Math.Max(0, delayMs);
        var checkAt = NowMs() + delayMs;
        if(ScheduledEchoReminderChecks.TryGetValue(text, out var scheduledAt) && scheduledAt <= checkAt)
        {
            DebugEcho($"定时聊天提示已有更早检查 text={text} nextIn={(scheduledAt - NowMs()) / 1000f:F1}s");
            return;
        }

        ScheduledEchoReminderChecks[text] = checkAt;
        Controller.Schedule(() => PrintEchoReminder(text, checkAt), delayMs);
        DebugEcho($"安排定时聊天提示 text={text} nextIn={delayMs / 1000f:F1}s checkAt={checkAt}");
    }

    private void ScheduleNextEchoReminderCheck(string text)
    {
        var now = NowMs();
        var next = PendingEchoReminders
            .Where(x => x.Text == text)
            .OrderBy(x => x.TriggerAtMs)
            .FirstOrDefault();
        if(next.Text == null)
        {
            DebugEcho($"定时聊天提示没有后续Buff text={text}");
            return;
        }

        var delayMs = next.TriggerAtMs <= now + EchoReminderBatchWindowMs
            ? EchoReminderRetryDelayMs
            : Math.Max(0, (int)(next.TriggerAtMs - now));
        ScheduleEchoReminderCheck(text, delayMs);
    }

    private bool TryGetEchoReminderRemaining(EchoReminder reminder, out float remainingTime)
    {
        remainingTime = 0;
        if(!reminder.PlayerId.TryGetPlayer(out var pc)) return false;
        foreach(var status in pc.StatusList)
        {
            if(status.StatusId != reminder.StatusId) continue;
            remainingTime = status.RemainingTime;
            return true;
        }
        return false;
    }

    private void PrintEchoReminder(string text, long expectedCheckAt)
    {
        var now = NowMs();
        if(!ScheduledEchoReminderChecks.TryGetValue(text, out var scheduledAt) || scheduledAt != expectedCheckAt)
        {
            DebugEcho($"定时聊天提示跳过：过期检查 text={text} expected={expectedCheckAt} current={scheduledAt}");
            return;
        }
        ScheduledEchoReminderChecks.Remove(text);

        DebugEcho($"定时聊天提示触发 text={text} pending={PendingEchoReminders.Count} now={now}");
        if(!C.OutputTimedEchoReminder)
        {
            DebugEcho($"定时聊天提示跳过：开关关闭 text={text}");
            return;
        }

        PendingEchoReminders.RemoveAll(x => !TryGetEchoReminderRemaining(x, out _));
        var dueReminders = PendingEchoReminders
            .Where(x => x.Text == text)
            .Select(x => (Reminder: x, HasRemaining: TryGetEchoReminderRemaining(x, out var remaining), Remaining: remaining))
            .Where(x => x.HasRemaining
                && x.Reminder.TriggerAtMs <= now + EchoReminderBatchWindowMs
                && x.Remaining <= x.Reminder.Threshold + EchoReminderRemainingWindow)
            .ToList();

        if(dueReminders.Count == 0)
        {
            var next = PendingEchoReminders
                .Where(x => x.Text == text)
                .OrderBy(x => x.TriggerAtMs)
                .FirstOrDefault();
            if(next.Text == null)
            {
                DebugEcho($"定时聊天提示跳过：已无有效Buff text={text}");
            }
            else
            {
                DebugEcho($"定时聊天提示跳过：没有到时间的Buff text={text} nextIn={(next.TriggerAtMs - now) / 1000f:F1}s，重试");
                ScheduleNextEchoReminderCheck(text);
            }
            return;
        }

        var partyOrder = Controller.GetPartyMembers()
            .Select((x, index) => (x.ObjectId, index))
            .ToDictionary(x => x.ObjectId, x => x.index);
        var jobs = dueReminders
            .OrderBy(x => partyOrder.TryGetValue(x.Reminder.PlayerId, out var index) ? index : int.MaxValue)
            .ThenBy(x => x.Reminder.JobName)
            .Select(x => x.Reminder.JobName)
            .Distinct()
            .Print("、");
        if(jobs == "")
        {
            DebugEcho($"定时聊天提示跳过：职业列表为空 text={text}");
            ScheduleNextEchoReminderCheck(text);
            return;
        }

        if(SendChatHint($"{text} 【{jobs}】"))
        {
            DebugEcho($"定时聊天提示输出 {ChatHintCommand} {text} 【{jobs}】");
            foreach(var reminder in dueReminders.Select(x => x.Reminder))
            {
                PendingEchoReminders.Remove(reminder);
            }
        }
        ScheduleNextEchoReminderCheck(text);
    }

    private void AddEchoReminder(uint playerId, uint statusId, bool isFake, float remainingTime)
    {
        if(!C.OutputTimedEchoReminder)
        {
            DebugEcho($"加入定时聊天提示跳过：开关关闭 player={Hex(playerId)} status={statusId}");
            return;
        }
        if(!playerId.TryGetPlayer(out var pc))
        {
            DebugEcho($"加入定时聊天提示跳过：找不到玩家 player={Hex(playerId)} status={statusId}");
            return;
        }
        if(!TryGetEchoReminderInfo(playerId, statusId, isFake, out var text, out var threshold, out var enabled))
        {
            DebugEcho($"加入定时聊天提示跳过：无文本 {PlayerName(playerId)} status={statusId} isFake={isFake}");
            return;
        }
        if(!enabled)
        {
            DebugEcho($"加入定时聊天提示跳过：分类关闭 {text} {PlayerName(playerId)} status={statusId}");
            return;
        }

        var delayMs = Math.Max(0, (int)((remainingTime - threshold) * 1000f));
        var triggerAt = NowMs() + delayMs;
        PendingEchoReminders.RemoveAll(x => x.PlayerId == playerId && x.StatusId == statusId);
        PendingEchoReminders.Add(new(text, playerId, statusId, GetChineseJobName(pc.ClassJob.RowId), triggerAt, threshold));
        DebugEcho($"加入定时聊天提示 {text} {PlayerName(playerId)} status={statusId} remain={remainingTime:F1} threshold={threshold:F1} delay={delayMs / 1000f:F1}s triggerAt={triggerAt}");
        ScheduleEchoReminderCheck(text, delayMs);
    }

    private bool TryGetEchoReminderInfo(uint playerId, uint statusId, bool isFake, out string text, out float threshold, out bool enabled)
    {
        text = "";
        threshold = C.TimedEchoReminderTH;
        enabled = true;
        if(statusId.EqualsAny(Debuffs.DebuffLookAway))
        {
            text = isFake ? "看向" : "背对";
            threshold = C.LookDontlookTH;
            enabled = C.OutputLookDontlookReminder;
        }
        else if(statusId.EqualsAny(Debuffs.DebuffStack))
        {
            text = isFake ? "散开" : "分摊";
            threshold = C.StackSpreadTH;
            enabled = C.OutputStackSpreadReminder;
        }
        else if(statusId.EqualsAny(Debuffs.DebuffSpread))
        {
            text = isFake ? "分摊" : "散开";
            threshold = C.StackSpreadTH;
            enabled = C.OutputStackSpreadReminder;
        }
        else if(statusId.EqualsAny(Debuffs.DebuffDontMove))
        {
            text = isFake ? "移动" : "禁止移动";
            threshold = C.MoveDontmoveTH;
            enabled = C.OutputMoveDontmoveReminder;
        }
        else if(statusId.EqualsAny(Debuffs.DebuffDonut))
        {
            text = isFake ? "放置范围" : "放置月环";
            threshold = C.DonutAOETH;
            enabled = C.OutputDonutAoeReminder;
        }
        else if(statusId.EqualsAny(Debuffs.DebuffFireSpread))
        {
            text = isFake ? "放置月环" : "放置范围";
            threshold = C.DonutAOETH;
            enabled = C.OutputDonutAoeReminder;
        }
        return text != "";
    }

    private string GetChineseJobName(uint jobId) => jobId switch
    {
        19 => "骑士",
        20 => "武僧",
        21 => "战士",
        22 => "龙骑士",
        23 => "吟游诗人",
        24 => "白魔法师",
        25 => "黑魔法师",
        27 => "召唤师",
        28 => "学者",
        30 => "忍者",
        31 => "机工士",
        32 => "暗黑骑士",
        33 => "占星术士",
        34 => "武士",
        35 => "赤魔法师",
        36 => "青魔法师",
        37 => "绝枪战士",
        38 => "舞者",
        39 => "钐镰客",
        40 => "贤者",
        41 => "蝰蛇剑士",
        42 => "绘灵法师",
        _ => $"职业{jobId}",
    };

    public override void OnReset()
    {
        IsTruth.Clear();
        FakeStatuses.Clear();
        PendingEchoReminders.Clear();
        ScheduledEchoReminderChecks.Clear();
        P4FakeIce = null;
        P4FakeThunder = null;
        P4StoredMagic = null;
        P4MagicReleaseText = null;
        P4CurrentNoFloodCastId = null;
        P4NoFloodPartyInfo.Clear();
        P4NoFloodReportedDirections.Clear();
    }

    public override void OnVFXSpawn(uint target, string vfxPath)
    {
        if(target.GetObject()?.DataId.EqualsAny<uint>(19510, 19507) == true)
        {
            if(VfxTruth.Contains(vfxPath))
            {
                IsTruth[target] = true;
                DebugEcho($"VFX 真 target={Hex(target)} path={vfxPath}");
            }
            else if(VfxLie.Contains(vfxPath))
            {
                IsTruth[target] = false;
                DebugEcho($"VFX 假 target={Hex(target)} path={vfxPath}");
            }
        }
    }

    public bool IsLie = false;

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if(set.Action != null && set.Source?.ObjectId.EqualsAny(IsTruth.Keys) == true)
        {
            IsLie = !IsTruth[set.Source.ObjectId];
            DebugEcho($"ActionEffect action={set.Action.Value.RowId} source={Hex(set.Source.ObjectId)} IsTruth={IsTruth[set.Source.ObjectId]} IsLie={IsLie}");
        }
    }

    public override void OnActorControl(uint sourceId, uint command, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, uint p7, uint p8, ulong targetId, byte replaying)
    {
        if(command == TargetIconCommand || command == 502 || TryNormalizeP4MagicMarker(p1, out _))
        {
            DebugEcho($"ActorControl src={Hex(sourceId)} cmd={command} p1={Hex(p1)} p2={Hex(p2)} p3={Hex(p3)} p4={Hex(p4)} p5={Hex(p5)} p6={Hex(p6)} p7={Hex(p7)} p8={Hex(p8)} target=0x{targetId:X} replay={replaying}");
        }

        if(command == TargetIconCommand)
        {
            ApplyP4MagicMarker(p1);
        }
        else if(command == 502)
        {
            ApplyP4MagicMarker(p1);
        }
    }

    private void ApplyP4MagicMarker(uint markerId)
    {
        if(!TryNormalizeP4MagicMarker(markerId, out var marker))
        {
            DebugEcho($"头标忽略 raw={Hex(markerId)} norm={Hex(markerId & 0xFFFF)}");
            return;
        }

        if(marker == MarkerFakeIce || marker == MarkerTrueIce)
        {
            P4FakeIce = marker == MarkerFakeIce;
            DebugEcho($"头标 冰 raw={Hex(markerId)} norm={Hex(marker)} => {MagicBool(P4FakeIce)}；{MagicState()}");
            if(P4StoredMagic != null && P4StoredMagic.Value.FakeIce == null)
            {
                P4StoredMagic = P4StoredMagic.Value with { FakeIce = P4FakeIce };
                DebugEcho($"写入储存冰={MagicBool(P4StoredMagic.Value.FakeIce)}；{MagicState()}");
            }
        }
        else if(marker == MarkerFakeThunder || marker == MarkerTrueThunder)
        {
            P4FakeThunder = marker == MarkerFakeThunder;
            DebugEcho($"头标 雷 raw={Hex(markerId)} norm={Hex(marker)} => {MagicBool(P4FakeThunder)}；{MagicState()}");
            if(P4StoredMagic != null && P4StoredMagic.Value.FakeThunder == null)
            {
                P4StoredMagic = P4StoredMagic.Value with { FakeThunder = P4FakeThunder };
                DebugEcho($"写入储存雷={MagicBool(P4StoredMagic.Value.FakeThunder)}；{MagicState()}");
            }
        }
    }

    private bool TryNormalizeP4MagicMarker(uint markerId, out uint marker)
    {
        marker = markerId & 0xFFFF;
        return marker is >= MarkerFakeIce and <= MarkerTrueThunder;
    }

    public override void OnGainBuffEffect(uint sourceId, FFXIVClientStructs.FFXIV.Client.Game.Status Status)
    {
        if(DebuffList.Contains(Status.StatusId) && sourceId.TryGetPlayer(out var pc))
        {
            var statusId = (uint)Status.StatusId;
            if(statusId.EqualsAny([.. Debuffs.NoFloodLifeWound, .. Debuffs.NoFloodDeathWound, Debuffs.DebuffLive, .. Debuffs.DebuffDie]))
            {
                var info = P4NoFloodPartyInfo.TryGetValue(sourceId, out var existing) ? existing : new NoFloodPartyInfo();
                if(statusId.EqualsAny([.. Debuffs.NoFloodLifeWound, .. Debuffs.NoFloodDeathWound]))
                {
                    info.ColorStatusId = statusId;
                    info.ColorIsFake = IsLie;
                }
                else
                {
                    info.LifeStatusId = statusId;
                    info.LifeIsFake = IsLie;
                }
                info.JobId = pc.ClassJob.RowId;
                P4NoFloodPartyInfo[sourceId] = info;
                DebugEcho($"记录无之泛滥 {PlayerName(sourceId)} color={info.ColorStatusId}:{info.ColorIsFake} life={info.LifeStatusId}:{info.LifeIsFake}");
            }

            var isFake = IsLie;
            DebugEcho($"GainBuff {PlayerName(sourceId)} status={Status.StatusId} remain={Status.RemainingTime:F1} IsLie={IsLie} textFake={isFake}");
            if(IsLie)
            {
                FakeStatuses.Add(new(sourceId, Status.StatusId));
                DebugEcho($"记录假Buff {PlayerName(sourceId)} status={Status.StatusId} fakeCount={FakeStatuses.Count}");
            }
            AddEchoReminder(sourceId, Status.StatusId, isFake, Status.RemainingTime);
            if(pc.AddressEquals(BasePlayer))
            {
                if((Debuffs.DebuffSpread.Contains(Status.StatusId) && !IsLie) || (Debuffs.DebuffStack.Contains(Status.StatusId) && IsLie))
                {
                    if(Status.RemainingTime > 60f)
                    {
                        if(C.UseSelfmark && C.MarkingParamLongSpread != 0)
                        {
                            if(GenericHelpers.IsScreenReady() && EzThrottler.Throttle("Chat", 1000))
                            {
                                var cmd = $"/marking {TextCommandParam.Get(C.MarkingParamLongSpread).Param.GetText()} <me>";
                                UseCommand(cmd);
                            }
                        }
                        if(C.OutputInChat)
                        {
                            ChatPrinter.Orange("长散开点名在你身上！");
                        }
                    }
                    else
                    {
                        if(C.UseSelfmark && C.MarkingParamShortSpread != 0)
                        {
                            if(GenericHelpers.IsScreenReady() && EzThrottler.Throttle("Chat", 1000))
                            {
                                var cmd = $"/marking {TextCommandParam.Get(C.MarkingParamShortSpread).Param.GetText()} <me>";
                                UseCommand(cmd);
                            }
                        }
                        if(C.OutputInChat)
                        {
                            ChatPrinter.Orange("短散开点名在你身上！");
                        }
                    }
                }

                if(Debuffs.DebuffLookAway.Contains(Status.StatusId))
                {
                    if(Status.RemainingTime > 65f)
                    {
                        if(C.OutputInChat)
                        {
                            ChatPrinter.Red(IsLie ? "长视线点名在你身上：看向" : "长视线点名在你身上：背对");
                        }
                    }
                    else
                    {
                        if(C.OutputInChat)
                        {
                            ChatPrinter.Red(IsLie ? "短视线点名在你身上：看向" : "短视线点名在你身上：背对");
                        }
                    }
                }

                if(Debuffs.DebuffDontMove.Contains(Status.StatusId))
                {
                    if(C.OutputInChat)
                    {
                        ChatPrinter.Yellow(IsLie ? "反转加速度炸弹在你身上：移动" : "加速度炸弹在你身上：禁止移动");
                    }
                }
            }
        }
    }

    public override void OnRemoveBuffEffect(uint sourceId, FFXIVClientStructs.FFXIV.Client.Game.Status Status)
    {
        if(!DebuffList.Contains(Status.StatusId)) return;

        DebugEcho($"RemoveBuff {PlayerName(sourceId)} status={Status.StatusId}");

        FakeStatuses.RemoveAll(x => x.objectId == sourceId && x.statusId == Status.StatusId);
        PendingEchoReminders.RemoveAll(x => x.PlayerId == sourceId && x.StatusId == Status.StatusId);
    }

    private void UseCommand(string cmd)
    {
        Controller.Schedule(() =>
        {
            if(Svc.Condition[ConditionFlag.DutyRecorderPlayback])
            {
                DuoLog.Warning($"录像回放中，原本会执行命令：{cmd}");
            }
            else
            {
                Chat.ExecuteCommand(cmd);
            }
        }, 2000 + Random.Shared.Next(2000));
    }

    public override void OnStartingCast(uint source, uint castId)
    {
        if(castId.EqualsAny<uint>(0xBA94, 0xBAA4, 0xC5DE, 0xBA95, 0xBAA5))
        {
            DebugEcho($"Cast source={Hex(source)} cast={Hex(castId)}；{MagicState()}");
        }

        if(castId.EqualsAny<uint>(CastNoFloodFakePurpleBlue, CastNoFloodFakeBluePurple, CastNoFloodTruePurpleBlue, CastNoFloodTrueBluePurple))
        {
            P4CurrentNoFloodCastId = castId;
            P4NoFloodReportedDirections.Clear();
            DebugEcho($"无之泛滥触发 cast={Hex(castId)}");
            Controller.Schedule(() => ShowNoFloodHint(castId), 500);
        }

        if(castId == 0xBA94)
        {
            Controller.Schedule(() =>
            {
                DebugEcho($"BA94 延迟读取；{MagicState()}");
                ShowIceThunderHint(P4FakeIce, P4FakeThunder);
            }, 250);
        }
        else if(castId == 0xBAA4)
        {
            P4StoredMagic = new P4MagicStorage(null, null);
            P4MagicReleaseText = null;
            DebugEcho($"开始魔法储存；{MagicState()}");
        }
        else if(castId == 0xC5DE)
        {
            Controller.Schedule(() =>
            {
                var fakeThunder = P4StoredMagic?.FakeThunder ?? P4FakeThunder;
                DebugEcho($"C5DE 延迟读取 雷={MagicBool(fakeThunder)}；{MagicState()}");
                ShowMagicHint(GetThunderHint(fakeThunder));
            }, 250);
        }
        else if(castId == 0xBA95)
        {
            Controller.Schedule(() =>
            {
                var fakeIce = P4StoredMagic?.FakeIce ?? P4FakeIce;
                DebugEcho($"BA95 延迟读取 冰={MagicBool(fakeIce)}；{MagicState()}");
                ShowMagicHint(GetIceHint(fakeIce));
            }, 250);
        }
        else if(castId == 0xBAA5)
        {
            DebugEcho($"BAA5 安排3秒后组合；{MagicState()}");
            Controller.Schedule(ShowP4MagicReleaseHint, 3000);
        }
    }

    private void ShowP4MagicReleaseHint()
    {
        var storedIce = P4StoredMagic?.FakeIce;
        var storedThunder = P4StoredMagic?.FakeThunder;
        var currentIce = P4FakeIce;
        var currentThunder = P4FakeThunder;
        var resolvedIce = storedIce.HasValue && currentIce.HasValue ? storedIce.Value != currentIce.Value : currentIce;
        var resolvedThunder = storedThunder.HasValue && currentThunder.HasValue ? storedThunder.Value != currentThunder.Value : currentThunder;
        DebugEcho($"BAA5 组合 储存冰={MagicBool(storedIce)} 当前冰={MagicBool(currentIce)} => {MagicBool(resolvedIce)}；储存雷={MagicBool(storedThunder)} 当前雷={MagicBool(currentThunder)} => {MagicBool(resolvedThunder)}");
        ShowIceThunderHint(resolvedIce, resolvedThunder);
    }

    private void ShowIceThunderHint(bool? fakeIce, bool? fakeThunder)
    {
        if(!fakeIce.HasValue || !fakeThunder.HasValue)
        {
            DebugEcho($"冰雷提示跳过：冰={MagicBool(fakeIce)} 雷={MagicBool(fakeThunder)}；{MagicState()}");
            return;
        }
        DebugEcho($"冰雷提示 冰={MagicBool(fakeIce)} 雷={MagicBool(fakeThunder)} => {GetIceThunderHint(fakeIce.Value, fakeThunder.Value)}");
        ShowMagicHint(GetIceThunderHint(fakeIce.Value, fakeThunder.Value));
    }

    private void ShowMagicHint(string? text)
    {
        if(text == null)
        {
            DebugEcho($"魔法提示跳过：文本为空；{MagicState()}");
            return;
        }
        P4MagicReleaseText = text;
        SendChatHint(text);
        DebugEcho($"魔法提示输出 {ChatHintCommand} {text}");
        Controller.Schedule(() => P4MagicReleaseText = null, 7000);
    }

    private void ShowNoFloodHint(uint castId)
    {
        var party = Controller.GetPartyMembers().ToList();
        if(party.Count == 0)
        {
            DebugEcho($"无之泛滥跳过：队伍为空 cast={Hex(castId)}");
            return;
        }

        var leftIsBlue = castId is CastNoFloodFakeBluePurple or CastNoFloodTrueBluePurple;
        var entries = new List<(string Job, string Dir, string Color)>();

        foreach(var member in party)
        {
            var pc = member;
            if(pc == null)
            {
                continue;
            }

            if(!P4NoFloodPartyInfo.TryGetValue(pc.ObjectId, out var info) || info.ColorStatusId == null || info.LifeStatusId == null)
            {
                DebugEcho($"无之泛滥缺缓存 {PlayerName(pc.ObjectId)}");
                continue;
            }

            var colorStatusId = info.ColorStatusId.Value;
            var lifeStatusId = info.LifeStatusId.Value;
            var isLifeWound = colorStatusId.EqualsAny(Debuffs.NoFloodLifeWound);
            var isDeathWound = colorStatusId.EqualsAny(Debuffs.NoFloodDeathWound);
            if(!isLifeWound && !isDeathWound)
            {
                DebugEcho($"无之泛滥未知伤口 {PlayerName(pc.ObjectId)} color={colorStatusId} life={lifeStatusId}");
                continue;
            }

            var isAllagan = lifeStatusId == Debuffs.DebuffLive;
            var isBeyondDeath = lifeStatusId.EqualsAny(Debuffs.DebuffDie);
            if(!isAllagan && !isBeyondDeath)
            {
                DebugEcho($"无之泛滥未知生死 {PlayerName(pc.ObjectId)} color={colorStatusId} life={lifeStatusId}");
                continue;
            }

            var colorFake = info.ColorIsFake ?? false;
            var lifeFake = info.LifeIsFake ?? false;
            var castFake = castId is CastNoFloodFakePurpleBlue or CastNoFloodFakeBluePurple;
            var targetBlue = isAllagan ? isLifeWound : isDeathWound;
            if(castFake) targetBlue = !targetBlue;

            var dir = targetBlue == leftIsBlue ? "← 边" : "→ 边";
            var color = targetBlue ? "蓝" : "紫";
            entries.Add(($"{GetChineseJobName(pc.ClassJob.RowId)}", dir, color));
            DebugEcho($"无之泛滥队员 {PlayerName(pc.ObjectId)} wound={(isLifeWound ? "生者" : "死者")}({colorStatusId}) life={(isAllagan ? "亚拉戈" : "超越死亡")}({lifeStatusId}) fakeC={colorFake} fakeL={lifeFake} castFake={castFake} => {dir}{color}");
        }

        if(entries.Count == 0)
        {
            DebugEcho($"无之泛滥跳过：无有效队员条目 cast={Hex(castId)}");
            return;
        }

        var groups = entries.GroupBy(x => (x.Dir, x.Color)).ToDictionary(g => g.Key, g => g.Select(x => x.Job).Distinct().ToList());
        foreach(var key in groups.Keys.OrderBy(x => x.Dir == "← 边" ? 0 : 1).ThenBy(x => x.Color == "蓝" ? 0 : 1))
        {
            var text = $"吃{key.Dir}{key.Color}色 【{groups[key].Print("、")}】";
            var reportKey = $"{castId:X}:{key.Dir}:{key.Color}";
            if(P4NoFloodReportedDirections.Add(reportKey))
            {
                DebugEcho($"无之泛滥输出 {text}");
                SendChatHint(text);
            }
        }
    }

    private string? GetIceHint(bool? fakeIce) => fakeIce switch
    {
        true => "吃扇形",
        false => "不吃",
        _ => null,
    };

    private string? GetThunderHint(bool? fakeThunder) => fakeThunder switch
    {
        true => "吃直条",
        false => "不吃",
        _ => null,
    };

    private string GetIceThunderHint(bool fakeIce, bool fakeThunder) => (fakeIce, fakeThunder) switch
    {
        (false, false) => "都躲开",
        (false, true) => "吃直条",
        (true, false) => "吃扇形",
        (true, true) => "都吃",
    };
    public override void OnSettingsDraw()
    {
        ImGui.Checkbox("将自己的 Debuff 输出到本地聊天（仅自己可见）", ref C.OutputInChat);
        ImGui.Checkbox("发送聊天提示", ref C.OutputTimedEchoReminder);
        ImGui.RadioButton("默语提示 /e", ref C.ChatOutputChannel, 0);
        ImGui.SameLine();
        ImGui.RadioButton("小队提示 /p", ref C.ChatOutputChannel, 1);
        ImGui.SameLine();
        ImGui.RadioButton("自定义前缀", ref C.ChatOutputChannel, 2);
        if(C.ChatOutputChannel == 2)
        {
            ImGui.SetNextItemWidth(150f);
            ImGui.InputText("自定义前缀", ref C.CustomChatPrefix, 32);
        }
        ImGui.Checkbox("输出调试日志", ref C.OutputDebugEcho);
        ImGui.SetNextItemWidth(150f);
        ImGuiEx.SliderFloat("聊天提示倒计时（秒）", ref C.TimedEchoReminderTH, 3, 20);
        ImGuiEx.Checkbox("散开时给自己标点（危险）", ref C.UseSelfmark, enabled: C.UseSelfmark || ImGuiEx.Ctrl);
        ImGuiEx.Tooltip("按住 CTRL 点击以启用");
        if(C.UseSelfmark)
        {
            DrawMarkingParam("短散开标点", ref C.MarkingParamShortSpread);
            DrawMarkingParam("长散开标点", ref C.MarkingParamLongSpread);
        }

        ImGui.Checkbox("提示分摊/散开", ref C.OutputStackSpreadReminder);
        ImGui.SetNextItemWidth(150f);
        ImGuiEx.SliderFloat($"提前显示分摊/散开（秒）", ref C.StackSpreadTH, 3, 20);
        ImGui.Checkbox("提示移动/禁止移动", ref C.OutputMoveDontmoveReminder);
        ImGui.SetNextItemWidth(150f);
        ImGuiEx.SliderFloat($"提前显示移动/禁止移动（秒）", ref C.MoveDontmoveTH, 3, 20);
        ImGui.Checkbox("提示看向/背对", ref C.OutputLookDontlookReminder);
        ImGui.SetNextItemWidth(150f);
        ImGuiEx.SliderFloat($"提前显示看向/背对（秒）", ref C.LookDontlookTH, 3, 20);
        ImGui.Checkbox("提示月环/范围放置", ref C.OutputDonutAoeReminder);
        ImGui.SetNextItemWidth(150f);
        ImGuiEx.SliderFloat($"提前显示月环/范围放置（秒）", ref C.DonutAOETH, 3, 20);

        if(ImGui.CollapsingHeader("调试"))
        {
            if(ImGui.Button("导出"))
            {
                GenericHelpers.Copy(JsonConvert.SerializeObject(FakeStatuses));
            }

            if(ImGui.Button("导入"))
            {
                FakeStatuses = JsonConvert.DeserializeObject<List<StatusInfo>>(GenericHelpers.Paste()) ?? throw new NullReferenceException();
            }

            ImGui.Checkbox(nameof(IsLie), ref IsLie);
            ImGuiEx.Text($"Debuff列表：{DebuffList.Print()}");
            ImGuiEx.Text($"施法者：{IsTruth.Select(x => $"{x.Key}: {x.Value}").Print("\n")}");
            ImGuiEx.Text($"假机制：\n{FakeStatuses.Select(x => $"{x.objectId.GetObject()} / {x.statusId} ({Svc.Data.GetExcelSheet<Status>().GetRowOrDefault(x.statusId)?.Name})").Print("\n")}");
        }
    }

    private void DrawMarkingParam(string name, ref uint param)
    {
        ImGui.PushID(name);
        ImGui.SetNextItemWidth(200f);
        if(ImGui.BeginCombo(name, param == 0 ? "未设置" : TextCommandParam.GetRef(param).ValueNullable?.Param.GetText(), ImGuiComboFlags.HeightLarge))
        {
            if(ImGui.Selectable("未设置", param == 0))
            {
                param = 0;
            }

            foreach(var x in ValidTextParams)
            {
                if(ImGui.Selectable(TextCommandParam.Get(x).Param.GetText(), param == x))
                {
                    param = x;
                }
            }
            ImGui.EndCombo();
        }
        ImGui.PopID();
    }

    public class Config
    {
        public float StackSpreadTH = 8.5f;
        public float MoveDontmoveTH = 8f;
        public float LookDontlookTH = 10f;
        public float DonutAOETH = 10f;
        public bool OutputStackSpreadReminder = true;
        public bool OutputMoveDontmoveReminder = true;
        public bool OutputLookDontlookReminder = true;
        public bool OutputDonutAoeReminder = true;
        public uint MarkingParamShortSpread;
        public uint MarkingParamLongSpread;
        public bool UseSelfmark = false;
        public bool OutputInChat = true;
        public bool OutputTimedEchoReminder = true;
        public int ChatOutputChannel = 0;
        public string CustomChatPrefix = "/e";
        public bool OutputDebugEcho = true;
        public float TimedEchoReminderTH = 12f;
    }

    private uint[] ValidTextParams = [80, 82, 84, 86, 88, 90, 92, 94, 96, 98, 100, 102, 104, 476, 478, 480,];
}
