using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Configuration;
using ECommons;
using ECommons.GameFunctions;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using ECommons.ImGuiMethods;
using Splatoon;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SplaSim.SplatoonScripts.Duties.Dawntrail.DancingMadUltimate;
public class P5_Exaflare : SplatoonScript
{
    public override Metadata Metadata => new(10, "地火");
    private const uint TerritoryDancingMadUltimate = 1363;
    private const uint ExaflareCast = 47932;
    private const uint CastSpread = 47934;
    private const float GroupInterval = 2.5f;
    private const float CastSpreadStart = 16.2f;
    private const float FirstImpactDelay = 5.2f;
    private const float ImpactInterval = 0.5f;
    private const float DiagonalStep = 5.0f;
    private const float ImpactRadius = 6.0f;
    private const float StageGrace = 0.10f;
    private const float PreferredRadius = 13.5f;
    private const int ExaflareImpactCount = 6;
    private const int MaxTrackedExaflareLines = 16;
    private const int VisiblePredictedHazardCount = 3;
    private const string DestinationElementName = "Destination";
    private const string PredictedHazardElementPrefix = "PredictedExaflare";

    private static readonly Vector3 ArenaCenter = new(100.0f, 0.0f, 100.0f);
    private static readonly Vector3 InitialDestination = new(100.0f, 0.0f, 105.0f);
    private static readonly float StepDistance = MathF.Sqrt(DiagonalStep * DiagonalStep * 2.0f);
    private static readonly Vector4 NavigationColor1 = 0xC800FFFF.ToVector4();
    private static readonly Vector4 NavigationColor2 = 0xC8FF00FF.ToVector4();
    private static readonly List<Vector3> Candidates =
    [
        new(99.654045f, 0.0000038146973f, 95.066025f),
        new(94.977036f, 0.0000019073486f, 100.6433f),
        new(100.189415f, 0.0f, 105.241425f),
        new(105.031425f, -0.0000019073486f, 100.19568f)
    ];

    private readonly List<LineCast> _lines = [];
    private readonly HashSet<int> _permanentSafeCandidateIndexes = [];
    private bool _active;
    private uint _castSpreadSource;

    public override HashSet<uint>? ValidTerritories { get; } = [TerritoryDancingMadUltimate];
  
    private Config C => Controller.GetConfig<Config>();

    public override void OnSetup()
    {
        Controller.RegisterElement(DestinationElementName, new Element(0)
        {
            Enabled = false,
            radius = 1.15f,
            thicc = 5.0f,
            fillIntensity = 0.25f,
            color = 0xC800BFFF,
            tether = true
        });

        for (var line = 0; line < MaxTrackedExaflareLines; line++)
        {
            for (var impact = 0; impact < ExaflareImpactCount; impact++)
            {
                Controller.RegisterElement(PredictedHazardElementName(line, impact), new Element(0)
                {
                    Enabled = false,
                    radius = ImpactRadius,
                    fillIntensity = 0.25f,
                    color = 0x780000FFu
                });
            }
        }
    }

    public override void OnCombatStart() => ResetState();
    public override void OnCombatEnd() => ResetState();
    public override void OnReset() => ResetState();

    public override void OnDirectorUpdate(DirectorUpdateCategory category)
    {
        if (category is DirectorUpdateCategory.Commence or DirectorUpdateCategory.Recommence or DirectorUpdateCategory.Wipe)
            ResetState();
    }

    public override void OnStartingCast(uint source, uint castId)
    {
        if (castId == ExaflareCast)
        {
            CaptureExaflare(source);
            return;
        }

        if (_active && castId == CastSpread)
            _castSpreadSource = source;
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if ((set.Action?.RowId ?? 0) == CastSpread)
            ResetState();
    }

    public override void OnUpdate()
    {
        var player = Controller.BasePlayer;
        if (!_active || player == null)
        {
            DisableElements();
            return;
        }

        if (!TryGetElapsed(out var elapsed))
        {
            DisableElements();
            return;
        }

        UpdatePredictedHazardElements(elapsed);
        UpdatePermanentSafeCandidates(elapsed);

        if (elapsed > LastKnownHazardTime() + 0.20f)
        {
            ResetState();
            return;
        }

        if (!TryPlanDestination(elapsed, player.Position, CurrentStage(elapsed), out var nextDestination))
        {
            DisableElements();
            return;
        }

        DrawDestination(nextDestination);
    }

    private void CaptureExaflare(uint source)
    {
        if (_lines.Any(line => line.Source == source))
            return;

        if (source.GetObject() is not IBattleChara battle)
            return;

        var group = Math.Min(5, _lines.Count / 2);
        _lines.Add(new LineCast(
            source,
            group,
            group * GroupInterval,
            new Vector3(battle.Position.X, 0.0f, battle.Position.Z),
            new Vector3(MathF.Sin(battle.Rotation), 0.0f, MathF.Cos(battle.Rotation)) * StepDistance));

        _active = true;
    }

    private bool TryGetElapsed(out float elapsed)
    {
        elapsed = 0.0f;
        var found = false;

        foreach (var line in _lines)
        {
            if (line.Source.GetObject() is not IBattleChara battle)
                continue;
            if (!battle.IsCasting || battle.CastActionId != ExaflareCast)
                continue;

            elapsed = MathF.Max(elapsed, line.GroupStart + battle.CurrentCastTime);
            found = true;
        }

        if (_castSpreadSource.GetObject() is IBattleChara spread && spread.IsCasting && spread.CastActionId == CastSpread)
        {
            elapsed = MathF.Max(elapsed, CastSpreadStart + spread.CurrentCastTime);
            found = true;
        }

        return found;
    }

    private bool TryPlanDestination(float elapsed, Vector3 playerPosition, int stage, out Vector3 destination)
    {
        destination = default;
        var hazards = BuildActiveHazards(elapsed);
        if (hazards.Count == 0)
            return false;

        if (stage == 0)
        {
            destination = InitialDestination;
            return true;
        }

        var intervals = BuildIntervals(elapsed, stage, hazards);
        if (intervals.Count == 0)
            return false;

        if (TryPlanIntervals(playerPosition, hazards, intervals, out destination))
            return true;

        return TryPlanIntervals(playerPosition, hazards, new[] { intervals[0] }, out destination);
    }

    private bool TryPlanIntervals(Vector3 playerPosition, IReadOnlyCollection<Hazard> hazards, IReadOnlyList<RouteInterval> intervals, out Vector3 destination)
    {
        destination = default;
        var states = new List<RouteState> { new(0.0f, playerPosition, null) };

        foreach (var interval in intervals)
        {
            var activeHazards = hazards
                .Where(hazard => hazard.Time >= interval.Start + 0.001f && hazard.Time <= interval.End + 0.05f)
                .ToList();
            if (activeHazards.Count == 0)
                continue;

            var nextStates = new List<RouteState>();
            foreach (var state in states)
            {
                for (var candidateIndex = 0; candidateIndex < Candidates.Count; candidateIndex++)
                {
                    var candidate = Candidates[candidateIndex];
                    var distance = DistanceXZ(state.Position, candidate);
                    if (!IsPositionSafe(candidate, activeHazards))
                        continue;

                    var radius = DistanceXZ(ArenaCenter, candidate);
                    var permanentSafeBonus = _permanentSafeCandidateIndexes.Contains(candidateIndex) ? -1000.0f : 0.0f;
                    var cost = state.Cost + distance + MathF.Abs(radius - PreferredRadius) * 0.05f + permanentSafeBonus;
                    nextStates.Add(new RouteState(cost, candidate, state.FirstDestination ?? candidate));
                }
            }

            if (nextStates.Count == 0)
                return false;

            states = nextStates
                .OrderBy(state => state.Cost)
                .Take(120)
                .ToList();
        }

        var best = states.OrderBy(state => state.Cost).FirstOrDefault();
        if (!best.FirstDestination.HasValue)
            return false;

        destination = best.FirstDestination.Value;
        return true;
    }

    private int CurrentStage(float elapsed)
    {
        if (elapsed < FirstImpactDelay + StageGrace)
            return 0;

        return Math.Clamp((int)MathF.Floor((elapsed - FirstImpactDelay - StageGrace) / GroupInterval) + 1, 0, 6);
    }

    private static float StageEnd(int stage, float lastKnownTime)
        => stage < 6 ? FirstImpactDelay + StageGrace + GroupInterval * stage : lastKnownTime;

    private List<RouteInterval> BuildIntervals(float elapsed, int stage, IReadOnlyCollection<Hazard> hazards)
    {
        var intervals = new List<RouteInterval>();
        var start = elapsed;
        var lastKnownTime = hazards.Max(hazard => hazard.Time) + 0.20f;
        for (var currentStage = stage; currentStage <= 6; currentStage++)
        {
            var end = MathF.Min(StageEnd(currentStage, lastKnownTime), lastKnownTime);
            if (end > start + 0.05f)
                intervals.Add(new RouteInterval(start, end));

            start = end;
            if (start >= lastKnownTime - 0.01f)
                break;
        }

        return intervals;
    }

    private List<Hazard> BuildActiveHazards(float elapsed)
    {
        var hazards = new List<Hazard>(_lines.Count * VisiblePredictedHazardCount);
        foreach (var line in _lines)
        {
            var added = 0;
            for (var i = 0; i < ExaflareImpactCount; i++)
            {
                var hazardTime = line.GroupStart + FirstImpactDelay + ImpactInterval * i;
                if (hazardTime < elapsed - 0.05f)
                    continue;

                hazards.Add(new Hazard(
                    hazardTime,
                    line.Start + line.Step * i,
                    line.Group));

                added++;
                if (added >= VisiblePredictedHazardCount)
                    break;
            }
        }

        return hazards;
    }

    private List<Hazard> BuildAllKnownHazards()
    {
        var hazards = new List<Hazard>(_lines.Count * ExaflareImpactCount);
        foreach (var line in _lines)
        {
            for (var i = 0; i < ExaflareImpactCount; i++)
            {
                hazards.Add(new Hazard(
                    line.GroupStart + FirstImpactDelay + ImpactInterval * i,
                    line.Start + line.Step * i,
                    line.Group));
            }
        }

        return hazards;
    }

    private void UpdatePermanentSafeCandidates(float elapsed)
    {
        var allHazards = BuildAllKnownHazards();
        if (allHazards.Count == 0)
            return;

        for (var candidateIndex = 0; candidateIndex < Candidates.Count; candidateIndex++)
        {
            if (_permanentSafeCandidateIndexes.Contains(candidateIndex))
                continue;

            var candidate = Candidates[candidateIndex];
            var overlappingHazards = allHazards
                .Where(hazard => DistanceXZ(candidate, hazard.Position) <= ImpactRadius)
                .ToList();

            if (overlappingHazards.Count < 2)
                continue;

            if (overlappingHazards.All(hazard => hazard.Time < elapsed - 0.05f))
                _permanentSafeCandidateIndexes.Add(candidateIndex);
        }
    }

    private float LastKnownHazardTime()
    {
        return _lines.Count == 0
            ? 0.0f
            : _lines.Max(line => line.GroupStart + FirstImpactDelay + ImpactInterval * (ExaflareImpactCount - 1));
    }

    private static bool IsPositionSafe(Vector3 position, IEnumerable<Hazard> hazards)
    {
        foreach (var hazard in hazards)
        {
            if (DistanceXZ(position, hazard.Position) <= ImpactRadius)
                return false;
        }

        return true;
    }

    private void DrawDestination(Vector3 destination)
    {
        if (Controller.TryGetElementByName(DestinationElementName, out var marker))
        {
            marker.Enabled = true;
            marker.color = GradientColor.Get(NavigationColor1, NavigationColor2).ToUint();
            marker.SetRefPosition(destination);
        }
    }

    private void UpdatePredictedHazardElements(float elapsed)
    {
        DisablePredictedHazardElements();

        if (!C.DrawPredictedHazards)
            return;

        for (var lineIndex = 0; lineIndex < Math.Min(_lines.Count, MaxTrackedExaflareLines); lineIndex++)
        {
            var line = _lines[lineIndex];
            var displayed = 0;
            for (var impact = 0; impact < ExaflareImpactCount; impact++)
            {
                var hazardTime = line.GroupStart + FirstImpactDelay + ImpactInterval * impact;
                if (hazardTime < elapsed - 0.05f)
                    continue;

                if (!Controller.TryGetElementByName(PredictedHazardElementName(lineIndex, impact), out var element))
                    break;

                element.Enabled = true;
                element.radius = C.PredictedHazardRadius;
                element.fillIntensity = C.PredictedHazardFillIntensity;
                element.color = C.PredictedHazardColor.ToUint();
                element.SetRefPosition(line.Start + line.Step * impact);

                displayed++;
                if (displayed >= VisiblePredictedHazardCount)
                    break;
            }
        }
    }

    private static string PredictedHazardElementName(int line, int impact)
        => $"{PredictedHazardElementPrefix}{line}_{impact}";

    private void DisablePredictedHazardElements()
    {
        foreach (var element in Controller.GetRegisteredElements().Where(element => element.Key.StartsWith(PredictedHazardElementPrefix)))
            element.Value.Enabled = false;
    }

    private void DisableElements()
    {
        foreach (var element in Controller.GetRegisteredElements().Values)
            element.Enabled = false;
    }

    private void ResetState()
    {
        _active = false;
        _castSpreadSource = 0;
        _lines.Clear();
        _permanentSafeCandidateIndexes.Clear();
        DisableElements();
    }

    private static float DistanceXZ(Vector3 left, Vector3 right)
    {
        var dx = left.X - right.X;
        var dz = left.Z - right.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    public override void OnSettingsDraw()
    {
        ImGui.Checkbox("显示预测危险区 / Show predicted hazards", ref C.DrawPredictedHazards);
        ImGui.SetNextItemWidth(150.0f);
        ImGui.DragFloat("预测圆显示半径 / Visual hazard radius", ref C.PredictedHazardRadius, 0.1f, 0.1f, 20.0f, "%.1f");
        ImGui.SetNextItemWidth(150.0f);
        ImGui.DragFloat("填充透明度 / Fill intensity", ref C.PredictedHazardFillIntensity, 0.01f, 0.0f, 1.0f, "%.2f");
        ImGui.ColorEdit4("危险区颜色 / Hazard color", ref C.PredictedHazardColor, ImGuiColorEditFlags.NoInputs);
    }

    private readonly record struct LineCast(uint Source, int Group, float GroupStart, Vector3 Start, Vector3 Step);
    private readonly record struct Hazard(float Time, Vector3 Position, int Group);
    private readonly record struct RouteInterval(float Start, float End);
    private readonly record struct RouteState(float Cost, Vector3 Position, Vector3? FirstDestination);

    public class Config : IEzConfig
    {
        public bool DrawPredictedHazards;
        public float PredictedHazardRadius = ImpactRadius;
        public float PredictedHazardFillIntensity = 0.25f;
        public Vector4 PredictedHazardColor = 0x780000FFu.ToVector4();
    }
}
