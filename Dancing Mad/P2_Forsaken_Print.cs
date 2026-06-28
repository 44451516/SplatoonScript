using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons;
using ECommons.Automation;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.Hooks;
using ECommons.Logging;
using ECommons.PartyFunctions;
using ECommons.Schedulers;
using Splatoon.SplatoonScripting;
using Splatoon.SplatoonScripting.Priority;

namespace SplatoonScriptsOfficial.Duties.Dawntrail.Dancing_Mad;

internal class P2_Forsaken_Print : SplatoonScript
{
    #region Metadata

    public override Metadata? Metadata => new(5, "mirage");
    public override HashSet<uint>? ValidTerritories => [TerritoryDmad];

    #endregion

    #region Constants

    private const uint TerritoryDmad = 1363;

    private const uint StatusStack = 5084;
    private const uint StatusSpread = 5085;
    private const uint StatusCone = 5086;

    private const uint MapEffectTowerIndexMin = 1;
    private const uint MapEffectTowerIndexMax = 8;
    private const ushort MapEffectTowerSpawnData1 = 1;
    private const ushort MapEffectTowerSpawnData2 = 2;
    private const int TowerPairMapStepOffset = 2;
    private const int AutoPrintDelayMs = 1000;

    private const int SceneP2 = 7;
    private const int PartyPlayerCount = 8;
    private const int StepCount = 8;
    private const int PrintStepArrayLength = StepCount + 1;

    private const int Step1PartySegmentSize = 4;
    private const int Step1PartySegment1Index = 0;
    private const int Step1PartySegment2Index = 4;

    private static readonly HashSet<int> FirstHalfTowerSteps = [1, 2, 3, 8];
    private static readonly HashSet<int> SecondHalfTowerSteps = [4, 5, 6, 7];

    private const int InitialGroupSpreadCount = 3;
    private const int InitialGroupStackCount = 1;
    private const int InitialGroupConeCount = 3;

    private const int PatternCount = 2;
    private static readonly PatternDefinition[] Patterns =
    [
        new(0, "211", 2, 1, 1),
        new(1, "022", 0, 2, 2),
    ];

    private static readonly RolePosition[] RoleSelectorPositions =
    [
        RolePosition.T1,
        RolePosition.T2,
        RolePosition.H1,
        RolePosition.H2,
        RolePosition.M1,
        RolePosition.M2,
        RolePosition.R1,
        RolePosition.R2,
    ];

    #endregion

    #region Config

    private Config C => Controller.GetConfig<Config>();
    private IPlayerCharacter BasePlayer => Controller.BasePlayer;

    #endregion

    #region State

    private readonly uint[] _activeTowerEntityIds = [0, 0];
    private readonly List<uint> _pendingTowerSpawnPositions = [];
    private bool _hasActiveTowers;
    private int _step;
    private int _lastPrintedStep;
    private int _pendingPrintStep;
    private readonly List<PlayerInfo> _infos = [];
    private bool _initialGroupResolved;
    private readonly List<RolePosition> _fixedGroupA = [];
    private readonly List<RolePosition> _fixedGroupB = [];
    private bool _fixedGroupsResolved;

    #endregion

    #region Types

    private readonly record struct PatternDefinition(int Id, string Label, int StackCount, int SpreadCount, int ConeCount);

    private enum PrintChannel
    {
        Echo,
        Party,
    }

    private enum MechanicHalf
    {
        None,
        First,
        Second,
    }

    private enum DebuffKind
    {
        None,
        Stack,
        Spread,
        Cone,
    }

    private sealed class PlayerInfo
    {
        public required IPlayerCharacter Player;
        public MechanicHalf Half;
        public DebuffKind Debuff;
        public string? RoleLabel;
    }

    private readonly record struct PrioritySlot(uint EntityId, RolePosition Role, DebuffKind Debuff);

    private readonly record struct Step1Pair(IReadOnlyList<RolePosition> Roles, DebuffKind LeftDebuff, DebuffKind RightDebuff);

    private sealed class Config : IEzConfig
    {
        public PriorityData PriorityData = CreateDefaultPriorityData();
        public bool[] PrintStepEnabled = new bool[PrintStepArrayLength];
        public PrintChannel Channel = PrintChannel.Echo;

        public void EnsureDefaults()
        {
            PriorityData ??= CreateDefaultPriorityData();
            NormalizeRoleAssignments(PriorityData);
            if(PrintStepEnabled == null || PrintStepEnabled.Length != PrintStepArrayLength)
            {
                var fixedSteps = new bool[PrintStepArrayLength];
                if(PrintStepEnabled != null)
                {
                    var copyLength = Math.Min(PrintStepEnabled.Length, fixedSteps.Length);
                    Array.Copy(PrintStepEnabled, fixedSteps, copyLength);
                }

                PrintStepEnabled = fixedSteps;
            }

            if(!Enum.IsDefined(Channel))
                Channel = PrintChannel.Echo;
        }

        private static PriorityData CreateDefaultPriorityData()
            => new()
            {
                Name = "P2 荒芜状态打印职责分配",
                Description = "固定职责：MT ST H1 H2 D1 D2 D3 D4",
                PriorityLists = [CreateRoleAssignmentList()],
            };
    }

    #endregion

    #region Lifecycle

    public override void OnSetup()
        => C.EnsureDefaults();

    public override void OnUpdate()
    {
        if(!IsPhaseActive())
        {
            ResetState();
            return;
        }

        if(!_initialGroupResolved)
            TryResolveInitialGroup();
    }

    public override void OnReset()
        => ResetState();

    public override void OnDirectorUpdate(DirectorUpdateCategory category)
    {
        if(category.EqualsAny(DirectorUpdateCategory.Commence, DirectorUpdateCategory.Recommence,
               DirectorUpdateCategory.Wipe))
            ResetState();
    }

    public override void OnMapEffect(uint position, ushort data1, ushort data2)
    {
        if(!IsPhaseActive())
            return;

        if(!IsTowerSpawnMapEffect(data1, data2) || !IsTowerMapPosition(position))
            return;

        AddTowerSpawnPosition(position);
    }

    public override void OnSettingsDraw()
    {
        C.EnsureDefaults();

        ImGui.TextUnformatted("普通 1238/4567 状态打印");
        ImGui.TextUnformatted("第 1 步初始化 A/B 分组；后续固定 A/B 分组打印。");
        ImGui.Spacing();

        ImGui.TextUnformatted($"当前频道：{FormatChannel(C.Channel)}");
        if(ImGui.Button("使用 /e"))
            C.Channel = PrintChannel.Echo;
        ImGui.SameLine();
        if(ImGui.Button("使用 /p"))
            C.Channel = PrintChannel.Party;

        ImGui.Spacing();
        ImGui.TextUnformatted("每步自动打印");
        ImGui.Separator();
        for(var step = 1; step <= StepCount; step++)
            ImGui.Checkbox($"打印第 {step} 步", ref C.PrintStepEnabled[step]);

        ImGui.Spacing();
        if(ImGui.Button("立即打印当前状态"))
            PrintCurrentState(manual: true);
        ImGui.SameLine();
        if(ImGui.Button("打印位置角色名"))
            PrintRoleAssignments();

        ImGui.Spacing();
        ImGui.TextUnformatted("职责分配");
        ImGui.TextUnformatted("小分组：MT/H1　ST/H2　D1/D3　D2/D4。");
        ImGui.Separator();
        DrawRoleAssignments();
    }

    private void DrawRoleAssignments()
    {
        var assignmentList = GetOrCreateRoleAssignmentList();
        DrawRoleAssignmentList(assignmentList);

        if(ImGui.Button("编辑全局职责分配"))
            Chat.Instance.ExecuteCommand("/splatoon p");
    }

    private static PriorityList CreateRoleAssignmentList()
        => new()
        {
            IsRole = true,
            List =
            [
                new JobbedPlayer { Role = RolePosition.T1 },
                new JobbedPlayer { Role = RolePosition.T2 },
                new JobbedPlayer { Role = RolePosition.H1 },
                new JobbedPlayer { Role = RolePosition.H2 },
                new JobbedPlayer { Role = RolePosition.M1 },
                new JobbedPlayer { Role = RolePosition.M2 },
                new JobbedPlayer { Role = RolePosition.R1 },
                new JobbedPlayer { Role = RolePosition.R2 },
            ],
        };

    private PriorityList GetOrCreateRoleAssignmentList()
    {
        NormalizeRoleAssignments(C.PriorityData);
        return C.PriorityData.PriorityLists[0];
    }

    private static void NormalizeRoleAssignments(PriorityData data)
    {
        data.PriorityLists.RemoveAll(list => !list.IsRole);
        if(data.PriorityLists.Count == 0)
            data.PriorityLists.Add(CreateRoleAssignmentList());

        var assignmentList = data.PriorityLists[0];
        if(data.PriorityLists.Count > 1)
            data.PriorityLists.RemoveRange(1, data.PriorityLists.Count - 1);
        assignmentList.IsRole = true;
        EnsureRoleAssignmentList(assignmentList);
    }

    private static void EnsureRoleAssignmentList(PriorityList assignmentList)
    {
        var existing = assignmentList.List
            .Where(entry => entry.Role != RolePosition.Not_Selected)
            .GroupBy(entry => entry.Role)
            .ToDictionary(group => group.Key, group => group.First());

        assignmentList.List.Clear();
        foreach(var role in RoleSelectorPositions)
        {
            if(existing.TryGetValue(role, out var entry))
                assignmentList.List.Add(entry);
            else
                assignmentList.List.Add(new JobbedPlayer { Role = role });
        }
    }

    private void DrawRoleAssignmentList(PriorityList assignmentList)
    {
        if(ImGui.BeginTable("P2PrintRoleAssignmentTable", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("职责", ImGuiTableColumnFlags.WidthFixed, 120f);
            ImGui.TableSetupColumn("解析", ImGuiTableColumnFlags.WidthFixed, 260f);
            ImGui.TableSetupColumn("当前点名", ImGuiTableColumnFlags.WidthFixed, 100f);

            for(var index = 0; index < assignmentList.List.Count; index++)
            {
                var entry = assignmentList.List[index];
                ImGui.PushID(index);
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(FormatRolePosition(entry.Role));

                ImGui.TableNextColumn();
                if(entry.IsInParty(true, out var resolved))
                    ImGui.TextUnformatted($"{resolved.NameWithWorld} | {resolved.ClassJob}");
                else
                    ImGui.TextUnformatted("未解析");

                ImGui.TableNextColumn();
                if(resolved?.IGameObject is IPlayerCharacter player)
                    ImGui.TextUnformatted(FormatDebuffForStep1(GetDebuffKind(player)));
                else
                    ImGui.TextUnformatted("无");

                ImGui.PopID();
            }

            ImGui.EndTable();
        }
    }

    private void PrintRoleAssignments()
    {
        C.EnsureDefaults();
        NormalizeRoleAssignments(C.PriorityData);
        var assignmentList = C.PriorityData.GetFirstValidList();
        if(assignmentList == null)
        {
            SendLine("[P2 位置] 未解析");
            return;
        }

        SendLine($"MT/H1 : {FormatRoleAssignment(assignmentList, RolePosition.T1)}　{FormatRoleAssignment(assignmentList, RolePosition.H1)}");
        SendLine($"ST/H2 : {FormatRoleAssignment(assignmentList, RolePosition.T2)}　{FormatRoleAssignment(assignmentList, RolePosition.H2)}");
        SendLine($"D1/D3 : {FormatRoleAssignment(assignmentList, RolePosition.M1)}　{FormatRoleAssignment(assignmentList, RolePosition.R1)}");
        SendLine($"D2/D4 : {FormatRoleAssignment(assignmentList, RolePosition.M2)}　{FormatRoleAssignment(assignmentList, RolePosition.R2)}");
    }

    private static string FormatRoleAssignment(PriorityList assignmentList, RolePosition role)
    {
        var entry = assignmentList.List.FirstOrDefault(item => item.Role == role);
        if(entry == null || !entry.IsInParty(assignmentList.IsRole, out var resolved))
            return $"{FormatRolePosition(role)}=未解析";

        return $"{FormatRolePosition(role)}={resolved.Name}";
    }

    #endregion

    #region Tower Tracking

    private void AddTowerSpawnPosition(uint position)
    {
        AddUniquePairPosition(_pendingTowerSpawnPositions, position);
        if(_pendingTowerSpawnPositions.Count != 2)
            return;

        if(!TryGetTowerPairReference(_pendingTowerSpawnPositions[0], _pendingTowerSpawnPositions[1],
               out var reference, out var paired))
        {
            _pendingTowerSpawnPositions.Clear();
            return;
        }

        _pendingTowerSpawnPositions.Clear();
        SetActiveTowers(reference, paired);
    }

    private static void AddUniquePairPosition(List<uint> list, uint position)
    {
        if(list.Count >= 2)
            list.Clear();

        if(!list.Contains(position))
            list.Add(position);
    }

    private void SetActiveTowers(uint reference, uint paired)
    {
        C.EnsureDefaults();
        _activeTowerEntityIds[0] = reference;
        _activeTowerEntityIds[1] = paired;
        _hasActiveTowers = true;
        _step++;

        if(_step == 1)
            TryResolveFixedGroups();

        if(_step is >= 1 and <= StepCount && C.PrintStepEnabled[_step] && _lastPrintedStep != _step)
        {
            _lastPrintedStep = _step;
            SchedulePrintCurrentState(_step);
        }
    }

    private static bool IsTowerMapPosition(uint position)
        => position is >= MapEffectTowerIndexMin and <= MapEffectTowerIndexMax;

    private static bool IsTowerSpawnMapEffect(ushort data1, ushort data2)
        => data1 == MapEffectTowerSpawnData1 && data2 == MapEffectTowerSpawnData2;

    private static bool TryGetTowerPairReference(uint first, uint second, out uint reference, out uint paired)
    {
        if(AddMapSteps(first, TowerPairMapStepOffset) == second)
        {
            reference = first;
            paired = second;
            return true;
        }

        if(AddMapSteps(second, TowerPairMapStepOffset) == first)
        {
            reference = second;
            paired = first;
            return true;
        }

        reference = 0;
        paired = 0;
        return false;
    }

    private static uint AddMapSteps(uint position, int steps)
    {
        var zeroBased = (int)position - 1;
        var wrapped = (zeroBased + steps) % StepCount;
        if(wrapped < 0)
            wrapped += StepCount;
        return (uint)(wrapped + 1);
    }

    #endregion

    #region Print

    private void PrintCurrentState(bool manual)
    {
        C.EnsureDefaults();

        if(!_fixedGroupsResolved)
            TryResolveFixedGroups();

        var lines = BuildFixedGroupLines();
        foreach(var line in lines)
            SendLine(line);
    }

    private void SchedulePrintCurrentState(int step)
    {
        _pendingPrintStep = step;
        _ = new TickScheduler(() =>
        {
            if(_pendingPrintStep != step || _step != step || !IsPhaseActive())
                return;

            _pendingPrintStep = 0;
            PrintCurrentState(manual: false);
        }, AutoPrintDelayMs);
    }

    private List<string> BuildFixedGroupLines()
    {
        var lines = new List<string> { $"第 {_step} 步" };
        if(_step == 1)
        {
            lines.Add($"A : {FormatFixedGroup(_fixedGroupA)}");
            lines.Add($"B : {FormatFixedGroup(_fixedGroupB)}");
        }
        else if(FirstHalfTowerSteps.Contains(_step))
        {
            lines.Add($"A : {FormatFixedGroup(_fixedGroupA)}");
        }
        else if(SecondHalfTowerSteps.Contains(_step))
        {
            lines.Add($"B : {FormatFixedGroup(_fixedGroupB)}");
        }

        return lines;
    }

    private string FormatFixedGroup(IReadOnlyList<RolePosition> roles)
    {
        var slots = TryGetPrioritySlots(out var prioritySlots) ? prioritySlots : new List<PrioritySlot>();
        var segments = new List<string>();
        for(var i = 0; i < roles.Count; i += 2)
        {
            var pairPlayers = new List<string>();
            for(var j = 0; j < 2 && i + j < roles.Count; j++)
                pairPlayers.Add(FormatFixedGroupPlayer(roles[i + j], slots));

            if(pairPlayers.Count > 0)
                segments.Add(string.Join("　", pairPlayers));
        }

        return string.Join("  ", segments);
    }

    private string FormatFixedGroupPlayer(RolePosition role, IReadOnlyList<PrioritySlot> slots)
    {
        var slot = slots.FirstOrDefault(item => item.Role == role);
        var debuff = slot == default ? DebuffKind.None : GetLiveDebuffKind(ResolveLivePlayer(slot));
        return $"{FormatRolePosition(role)}{FormatDebuffForStep1(debuff)}";
    }

    private bool TryResolveFixedGroups()
    {
        if(!TryGetPrioritySlots(out var slots))
            return false;

        var aRoles = new List<RolePosition>();
        var bRoles = new List<RolePosition>();

        var hasThPair1 = TryBuildStep1PairByRoles(slots, RolePosition.T1, RolePosition.H1, out var mtH1);
        var hasThPair2 = TryBuildStep1PairByRoles(slots, RolePosition.T2, RolePosition.H2, out var stH2);
        AddComplementaryPairGroup(hasThPair1, mtH1, hasThPair2, stH2, aRoles, bRoles);

        var hasDpsPair1 = TryBuildStep1PairByRoles(slots, RolePosition.M1, RolePosition.R1, out var d1D3);
        var hasDpsPair2 = TryBuildStep1PairByRoles(slots, RolePosition.M2, RolePosition.R2, out var d2D4);
        AddComplementaryPairGroup(hasDpsPair1, d1D3, hasDpsPair2, d2D4, aRoles, bRoles);

        if(aRoles.Count == 0 && bRoles.Count == 0)
            return false;

        _fixedGroupA.Clear();
        _fixedGroupA.AddRange(aRoles);
        _fixedGroupB.Clear();
        _fixedGroupB.AddRange(bRoles);
        _fixedGroupsResolved = true;
        return true;
    }

    private bool TryGetPrioritySlots(out List<PrioritySlot> slots)
    {
        slots = [];
        NormalizeRoleAssignments(C.PriorityData);
        var priorityList = C.PriorityData.GetFirstValidList();
        if(priorityList == null || priorityList.List.Count < PartyPlayerCount)
            return false;

        foreach(var entry in priorityList.List.Take(PartyPlayerCount))
        {
            if(!entry.IsInParty(priorityList.IsRole, out var member) || member?.IGameObject is not IPlayerCharacter player)
                return false;

            slots.Add(new PrioritySlot(player.EntityId, entry.Role, GetDebuffKind(player)));
        }

        return slots.Count == PartyPlayerCount;
    }

    private static bool TryBuildStep1PairByRoles(IReadOnlyList<PrioritySlot> slots, RolePosition leftRole, RolePosition rightRole, out Step1Pair pair)
    {
        pair = default;
        var left = slots.FirstOrDefault(slot => slot.Role == leftRole);
        var right = slots.FirstOrDefault(slot => slot.Role == rightRole);
        if(left == default || right == default)
            return false;

        pair = new Step1Pair(
            [left.Role, right.Role],
            left.Debuff,
            right.Debuff);
        return true;
    }

    private static void AddComplementaryPairGroup(
        bool hasFirst,
        Step1Pair first,
        bool hasSecond,
        Step1Pair second,
        List<RolePosition> aRoles,
        List<RolePosition> bRoles)
    {
        if(hasFirst && hasSecond)
        {
            if(ContainsDebuff(first, DebuffKind.Stack))
            {
                aRoles.AddRange(first.Roles);
                bRoles.AddRange(second.Roles);
                return;
            }

            if(ContainsDebuff(second, DebuffKind.Stack))
            {
                aRoles.AddRange(second.Roles);
                bRoles.AddRange(first.Roles);
                return;
            }
        }

        AddResolvedPairFallback(hasFirst, first, aRoles, bRoles);
        AddResolvedPairFallback(hasSecond, second, aRoles, bRoles);
    }

    private static void AddResolvedPairFallback(bool hasPair, Step1Pair pair, List<RolePosition> aRoles, List<RolePosition> bRoles)
    {
        if(!hasPair)
            return;

        if(ContainsDebuff(pair, DebuffKind.Stack))
            aRoles.AddRange(pair.Roles);
        else
            bRoles.AddRange(pair.Roles);
    }

    private static bool ContainsDebuff(Step1Pair pair, DebuffKind debuff)
        => pair.LeftDebuff == debuff || pair.RightDebuff == debuff;

    private IPlayerCharacter? ResolveLivePlayer(PrioritySlot slot)
    {
        if(slot == default)
            return null;

        return Controller.GetPartyMembers().FirstOrDefault(player => player.EntityId == slot.EntityId);
    }

    private void SendLine(string text)
    {
        var command = C.Channel switch
        {
            PrintChannel.Party => $"/p {text}",
            _ => $"/e {text}",
        };

        if(Svc.Condition[ConditionFlag.DutyRecorderPlayback])
            DuoLog.Information(command);
        else
            Chat.Instance.ExecuteCommand(command);
    }

    #endregion

    #region Role Resolution

    private bool TryResolveState(out int patternId, out string roleLabel)
    {
        patternId = -1;
        roleLabel = "";

        if(!TryEnsureInfos())
            return false;

        UpdateDebuffs();

        if(!_initialGroupResolved)
        {
            if(!TryResolveInitialGroup())
                return false;

            _initialGroupResolved = true;
        }

        if(!_hasActiveTowers || _step < 1)
            return false;

        if(_step == 1)
        {
            patternId = 0;
            roleLabel = GetBasePlayerInfo()?.RoleLabel ?? "";
            return roleLabel.Length > 0;
        }

        if(!TryDetectPartyPattern(out patternId))
            return false;

        ResolvePattern(patternId);

        roleLabel = GetBasePlayerInfo()?.RoleLabel ?? "";
        return roleLabel.Length > 0;
    }

    private static DebuffKind GetDebuffKind(IPlayerCharacter player)
    {
        if(player.StatusList.Any(s => s.StatusId == StatusStack))
            return DebuffKind.Stack;
        if(player.StatusList.Any(s => s.StatusId == StatusSpread))
            return DebuffKind.Spread;
        if(player.StatusList.Any(s => s.StatusId == StatusCone))
            return DebuffKind.Cone;
        return DebuffKind.None;
    }

    private static DebuffKind GetLiveDebuffKind(IPlayerCharacter? player)
        => player == null ? DebuffKind.None : GetDebuffKind(player);

    private List<IPlayerCharacter> GetOrderedPartyPlayers()
    {
        C.EnsureDefaults();
        NormalizeRoleAssignments(C.PriorityData);
        var priority = C.PriorityData.GetPlayers(_ => true);
        if(priority == null || priority.Count != PartyPlayerCount)
            return [];

        var names = priority.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        var members = Controller.GetPartyMembers()
            .Where(p => names.Contains(p.Name.ToString()))
            .ToList();
        if(members.Count != PartyPlayerCount)
            return [];

        return OrderByPriority(members).ToList();
    }

    private List<IPlayerCharacter> GetLivePartyMembers()
        => Controller.GetPartyMembers().ToList();

    private int GetPriorityIndex(IPlayerCharacter player)
    {
        NormalizeRoleAssignments(C.PriorityData);
        var priorityList = C.PriorityData.GetPlayers(_ => true);
        if(priorityList == null)
            return int.MaxValue;

        var name = player.Name.ToString();
        for(var i = 0; i < priorityList.Count; i++)
        {
            if(priorityList[i].Name == name)
                return i;
        }

        return int.MaxValue;
    }

    private IEnumerable<PlayerInfo> OrderInfosByPriority(IEnumerable<PlayerInfo> infos)
        => infos.OrderBy(i => GetPriorityIndex(i.Player)).ThenBy(i => i.Player.EntityId);

    private IEnumerable<IPlayerCharacter> OrderByPriority(IEnumerable<IPlayerCharacter> players)
        => players.OrderBy(GetPriorityIndex).ThenBy(p => p.EntityId);

    private bool TryEnsureInfos()
    {
        var party = GetOrderedPartyPlayers();
        if(party.Count != PartyPlayerCount)
            return false;

        if(_infos.Count != PartyPlayerCount)
        {
            _infos.Clear();
            foreach(var player in party)
            {
                _infos.Add(new PlayerInfo
                {
                    Player = player,
                    Half = MechanicHalf.None,
                    Debuff = GetDebuffKind(player),
                });
            }
        }

        return true;
    }

    private void UpdateDebuffs()
    {
        foreach(var info in _infos)
            info.Debuff = GetDebuffKind(info.Player);
    }

    private bool TryGetActiveMechanicHalf(out MechanicHalf half)
    {
        if(FirstHalfTowerSteps.Contains(_step))
        {
            half = MechanicHalf.First;
            return true;
        }

        if(SecondHalfTowerSteps.Contains(_step))
        {
            half = MechanicHalf.Second;
            return true;
        }

        half = MechanicHalf.None;
        return false;
    }

    private void CountDebuffKindsFromActiveGroup(out int stack, out int spread, out int cone)
    {
        stack = 0;
        spread = 0;
        cone = 0;
        if(!TryGetActiveMechanicHalf(out var activeHalf))
            return;

        foreach(var info in _infos)
        {
            if(info.Half != activeHalf)
                continue;

            switch(info.Debuff)
            {
                case DebuffKind.Stack: stack++; break;
                case DebuffKind.Spread: spread++; break;
                case DebuffKind.Cone: cone++; break;
            }
        }
    }

    private bool TryDetectPartyPattern(out int patternId)
    {
        patternId = -1;
        if(!TryEnsureInfos())
            return false;

        if(!TryGetActiveMechanicHalf(out _))
            return false;

        CountDebuffKindsFromActiveGroup(out var stack, out var spread, out var cone);

        for(var i = 0; i < PatternCount; i++)
        {
            var pattern = Patterns[i];
            if(pattern.StackCount != stack || pattern.SpreadCount != spread || pattern.ConeCount != cone)
                continue;

            if(patternId >= 0)
            {
                patternId = -1;
                return false;
            }

            patternId = i;
        }

        return patternId >= 0;
    }

    private bool IsTower(PlayerInfo info)
    {
        if(FirstHalfTowerSteps.Contains(_step))
            return info.Half == MechanicHalf.First;
        if(SecondHalfTowerSteps.Contains(_step))
            return info.Half == MechanicHalf.Second;
        return false;
    }

    private bool TryResolveInitialGroup()
    {
        if(!TryEnsureInfos())
            return false;

        UpdateDebuffs();
        ClearInitialAssignments();

        var firstHalfGroup = GetStep1PartySegment(0).ToList();
        var secondHalfGroup = GetStep1PartySegment(1).ToList();

        if(!TryResolveInitialGroupSegment(firstHalfGroup) || !TryResolveInitialGroupSegment(secondHalfGroup))
            return false;

        return _infos.All(i => i.Half != MechanicHalf.None && i.RoleLabel != null);
    }

    private void ClearInitialAssignments()
    {
        foreach(var info in _infos)
        {
            info.Half = MechanicHalf.None;
            info.RoleLabel = null;
        }
    }

    private IReadOnlyList<PlayerInfo> GetStep1PartySegment(int segmentIndex)
    {
        if(_infos.Count != PartyPlayerCount)
            return [];

        var start = segmentIndex switch
        {
            0 => Step1PartySegment1Index,
            1 => Step1PartySegment2Index,
            _ => -1,
        };
        if(start < 0)
            return [];

        return _infos.Skip(start).Take(Step1PartySegmentSize).ToList();
    }

    private bool TryResolveInitialGroupSegment(IReadOnlyList<PlayerInfo> group)
    {
        if(group.Count != Step1PartySegmentSize)
            return false;

        CountDebuffKinds(group, out var stack, out var spread, out var cone);

        if(spread == InitialGroupSpreadCount && stack == InitialGroupStackCount)
            return ApplySpreadInitialGroup(group);
        if(cone == InitialGroupConeCount && stack == InitialGroupStackCount)
            return ApplyConeInitialGroup(group);

        return false;
    }

    private static void CountDebuffKinds(IReadOnlyList<PlayerInfo> group, out int stack, out int spread, out int cone)
    {
        stack = 0;
        spread = 0;
        cone = 0;
        foreach(var info in group)
        {
            switch(info.Debuff)
            {
                case DebuffKind.Stack: stack++; break;
                case DebuffKind.Spread: spread++; break;
                case DebuffKind.Cone: cone++; break;
            }
        }
    }

    private bool ApplySpreadInitialGroup(IReadOnlyList<PlayerInfo> group)
    {
        var stackPlayer = group.FirstOrDefault(i => i.Debuff == DebuffKind.Stack);
        if(stackPlayer == null)
            return false;

        stackPlayer.Half = MechanicHalf.First;
        stackPlayer.RoleLabel = "211_RightTowerStack";

        var spreadPlayers = OrderInfosByPriority(group.Where(i => i.Debuff == DebuffKind.Spread)).ToList();
        if(spreadPlayers.Count != InitialGroupSpreadCount)
            return false;

        spreadPlayers[0].Half = MechanicHalf.First;
        spreadPlayers[0].RoleLabel = "211_RightTowerSpread";
        spreadPlayers[1].Half = MechanicHalf.Second;
        spreadPlayers[1].RoleLabel = "211_RightTowerStackOutside";
        spreadPlayers[2].Half = MechanicHalf.Second;
        spreadPlayers[2].RoleLabel = "211_RightTowerStackOutside";

        return true;
    }

    private bool ApplyConeInitialGroup(IReadOnlyList<PlayerInfo> group)
    {
        var stackPlayer = group.FirstOrDefault(i => i.Debuff == DebuffKind.Stack);
        if(stackPlayer == null)
            return false;

        stackPlayer.Half = MechanicHalf.First;
        stackPlayer.RoleLabel = "211_LeftTowerStack";

        var conePlayers = OrderInfosByPriority(group.Where(i => i.Debuff == DebuffKind.Cone)).ToList();
        if(conePlayers.Count != InitialGroupConeCount)
            return false;

        conePlayers[0].Half = MechanicHalf.First;
        conePlayers[0].RoleLabel = "211_LeftTowerCone";
        conePlayers[1].Half = MechanicHalf.Second;
        conePlayers[1].RoleLabel = "211_LeftTowerStackOutside";
        conePlayers[2].Half = MechanicHalf.Second;
        conePlayers[2].RoleLabel = "211_LeftTowerBaitCone";

        return true;
    }

    private void ResolvePattern(int patternId)
    {
        foreach(var info in _infos)
            info.RoleLabel = ResolvePatternRole(patternId, info);
    }

    private string? ResolvePatternRole(int patternId, PlayerInfo info)
    {
        return patternId switch
        {
            0 => ResolvePattern211(info),
            1 => ResolvePattern022(info),
            _ => null,
        };
    }

    private string? ResolvePattern211(PlayerInfo info)
    {
        if(IsTower(info))
        {
            return info.Debuff switch
            {
                DebuffKind.Stack => GetPriorityRank(
                    _infos.Where(i => IsTower(i) && i.Debuff == DebuffKind.Stack), info) switch
                {
                    0 => "211_LeftTowerStack",
                    1 => "211_RightTowerStack",
                    _ => null,
                },
                DebuffKind.Spread => "211_RightTowerSpread",
                DebuffKind.Cone => "211_LeftTowerCone",
                _ => null,
            };
        }

        return GetPriorityRank(_infos.Where(i => !IsTower(i)), info) switch
        {
            0 => "211_LeftTowerStackOutside",
            1 => "211_LeftTowerBaitCone",
            2 or 3 => "211_RightTowerStackOutside",
            _ => null,
        };
    }

    private string? ResolvePattern022(PlayerInfo info)
    {
        if(IsTower(info))
        {
            if(info.Debuff == DebuffKind.Spread)
            {
                return GetPriorityRank(
                    _infos.Where(i => IsTower(i) && i.Debuff == DebuffKind.Spread), info) switch
                {
                    0 => "022_RightTowerSpreadLeft",
                    1 => "022_RightTowerSpreadRight",
                    _ => null,
                };
            }

            if(info.Debuff == DebuffKind.Cone)
            {
                return GetPriorityRank(
                    _infos.Where(i => IsTower(i) && i.Debuff == DebuffKind.Cone), info) switch
                {
                    0 => "022_LeftTowerConeLeft",
                    1 => "022_LeftTowerConeRight",
                    _ => null,
                };
            }

            return null;
        }

        return GetPriorityRank(_infos.Where(i => !IsTower(i)), info) switch
        {
            0 => "022_LeftDemise1",
            1 => "022_LeftDemise2",
            2 => "022_RightDemise3",
            3 => "022_RightDemise4",
            _ => null,
        };
    }

    private int GetPriorityRank(IEnumerable<PlayerInfo> subset, PlayerInfo target)
    {
        var ordered = OrderInfosByPriority(subset).ToList();
        return ordered.FindIndex(i => i.Player.EntityId == target.Player.EntityId);
    }

    private PlayerInfo? GetBasePlayerInfo()
    {
        if(BasePlayer == null)
            return null;

        return _infos.FirstOrDefault(i => i.Player.EntityId == BasePlayer.EntityId);
    }

    #endregion

    #region Formatting

    private static string FormatChannel(PrintChannel channel)
        => channel == PrintChannel.Party ? "/p" : "/e";

    private static string FormatTower(uint tower)
        => tower == 0 ? "无" : tower.ToString();

    private static string FormatHalf(MechanicHalf half)
        => half switch
        {
            MechanicHalf.First => "前半",
            MechanicHalf.Second => "后半",
            _ => "无",
        };

    private static string FormatDebuffForStep1(DebuffKind debuff)
        => debuff switch
        {
            DebuffKind.Stack => "分摊",
            DebuffKind.Spread => "钢铁",
            DebuffKind.Cone => "扇形",
            _ => "无",
        };

    private static string FormatRolePosition(RolePosition role)
        => role switch
        {
            RolePosition.T1 => "MT",
            RolePosition.T2 => "ST",
            RolePosition.H1 => "H1",
            RolePosition.H2 => "H2",
            RolePosition.M1 => "D1",
            RolePosition.M2 => "D2",
            RolePosition.R1 => "D3",
            RolePosition.R2 => "D4",
            _ => "未选",
        };

    private static string FormatJobName(uint jobId)
    {
        return jobId switch
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
    }

    #endregion

    #region State

    private bool IsPhaseActive()
        => Controller.Scene == SceneP2;

    private void ResetState()
    {
        _hasActiveTowers = false;
        _step = 0;
        _lastPrintedStep = 0;
        _pendingPrintStep = 0;
        _pendingTowerSpawnPositions.Clear();
        _activeTowerEntityIds[0] = 0;
        _activeTowerEntityIds[1] = 0;
        _infos.Clear();
        _initialGroupResolved = false;
        _fixedGroupA.Clear();
        _fixedGroupB.Clear();
        _fixedGroupsResolved = false;
    }

    #endregion
}
