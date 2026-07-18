using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace NuclearOptionCommander;

internal static class CommanderSupplyDebugTelemetry
{
    private const float RouteSampleSpacing = 250f;
    private const float RuntimeLogInterval = 3f;

    private static readonly FieldInfo? HeloUrgencyField = AccessTools.Field(typeof(AutopilotHelo), "terrainAvoidanceUrgency");
    private static readonly Dictionary<Aircraft, FlightEntry> Flights = new();
    private static readonly Dictionary<Autopilot, FlightEntry> Autopilots = new();

    internal static void TrackAircraft(Aircraft aircraft, GlobalPosition target, bool safeFlight, bool airdrop)
    {
        FlightEntry entry = new(aircraft, target, safeFlight, airdrop);
        Flights[aircraft] = entry;
        if (aircraft.autopilot != null)
        {
            Autopilots[aircraft.autopilot] = entry;
        }

        LogRouteProfile(entry);
    }

    internal static void RecordAutoAim(Autopilot autopilot, float altitudeHold, bool followTerrain)
    {
        if (!Autopilots.TryGetValue(autopilot, out FlightEntry entry)
            || entry.Aircraft == null
            || Time.unscaledTime < entry.NextRuntimeLog)
        {
            return;
        }

        entry.NextRuntimeLog = Time.unscaledTime + RuntimeLogInterval;
        Aircraft aircraft = entry.Aircraft;
        Vector3 horizontalVelocity = aircraft.rb != null ? aircraft.rb.velocity : Vector3.zero;
        horizontalVelocity.y = 0f;
        Vector3 direction = horizontalVelocity.sqrMagnitude > 1f
            ? horizontalVelocity.normalized
            : new Vector3(aircraft.transform.forward.x, 0f, aircraft.transform.forward.z).normalized;
        float lookAhead = Mathf.Max(aircraft.speed, 100f) * 6f;
        Vector3 samplePoint = aircraft.transform.position + direction * lookAhead;

        bool terrainFound = PathfindingAgent.RaycastTerrain(samplePoint, out RaycastHit hit);
        float currentGround = aircraft.transform.position.y - aircraft.radarAlt;
        float terrainRise = terrainFound ? hit.point.y - currentGround : 0f;
        float urgency = HeloUrgencyField?.GetValue(autopilot) is float value ? value : 0f;
        if (autopilot is AutopilotTiltwing tiltwing)
        {
            urgency = tiltwing.GetTerrainWarningSystem()?.urgency ?? urgency;
        }

        CommanderPlugin.Log.LogInfo(
            $"[SupplyTerrain] aircraft={CommanderGameAccess.GetUnitLabel(aircraft)} radarAlt={aircraft.radarAlt:F1}m " +
            $"hold={altitudeHold:F1}m followTerrain={followTerrain} speed={aircraft.speed:F1}m/s " +
            $"lookAhead={lookAhead:F0}m terrainFound={terrainFound} riseAhead={terrainRise:F1}m urgency={urgency:F2}");
    }

    internal static void NotifyAircraftKilled(Unit unit)
    {
        if (unit is Aircraft aircraft && Flights.TryGetValue(aircraft, out FlightEntry entry) && !entry.LossLogged)
        {
            entry.LossLogged = true;
            CommanderPlugin.Log.LogWarning(
                $"[SupplyAircraftLost] aircraft={CommanderGameAccess.GetUnitLabel(aircraft)} " +
                $"position={aircraft.GlobalPosition()} target={entry.Target} radarAlt={aircraft.radarAlt:F1}m " +
                $"speed={aircraft.speed:F1}m/s safeFlight={entry.SafeFlight} airdrop={entry.Airdrop}");
        }
    }

    internal static void NotifyAircraftEnded(Aircraft aircraft)
    {
        if (ReferenceEquals(aircraft, null) || !Flights.TryGetValue(aircraft, out FlightEntry entry))
        {
            return;
        }

        if (aircraft.unitState == Unit.UnitState.Destroyed && !entry.LossLogged)
        {
            NotifyAircraftKilled(aircraft);
        }
        else
        {
            CommanderPlugin.Log.LogInfo(
                $"[SupplyAircraftEnded] aircraft={CommanderGameAccess.GetUnitLabel(aircraft)} state={aircraft.unitState} " +
                $"safeFlight={entry.SafeFlight} airdrop={entry.Airdrop}");
        }

        Flights.Remove(aircraft);
        if (aircraft.autopilot != null)
        {
            Autopilots.Remove(aircraft.autopilot);
        }
    }

    internal static void Reset()
    {
        Flights.Clear();
        Autopilots.Clear();
    }

    private static void LogRouteProfile(FlightEntry entry)
    {
        Vector3 start = entry.Aircraft.transform.position;
        Vector3 end = entry.Target.ToLocalPosition();
        Vector3 horizontal = end - start;
        horizontal.y = 0f;
        float distance = horizontal.magnitude;
        int sampleCount = Mathf.Clamp(Mathf.CeilToInt(distance / RouteSampleSpacing) + 1, 2, 400);
        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;
        float maxStep = 0f;
        float previousHeight = 0f;
        int hits = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            float progress = sampleCount == 1 ? 0f : i / (float)(sampleCount - 1);
            Vector3 point = Vector3.Lerp(start, end, progress);
            if (!PathfindingAgent.RaycastTerrain(point, out RaycastHit hit))
            {
                continue;
            }

            float height = hit.point.y;
            minHeight = Mathf.Min(minHeight, height);
            maxHeight = Mathf.Max(maxHeight, height);
            if (hits > 0)
            {
                maxStep = Mathf.Max(maxStep, Mathf.Abs(height - previousHeight));
            }
            previousHeight = height;
            hits++;
        }

        CommanderPlugin.Log.LogInfo(
            $"[SupplyRouteTerrain] aircraft={CommanderGameAccess.GetUnitLabel(entry.Aircraft)} distance={distance:F0}m " +
            $"samples={sampleCount} hits={hits} min={(hits > 0 ? minHeight : 0f):F1}m " +
            $"max={(hits > 0 ? maxHeight : 0f):F1}m range={(hits > 0 ? maxHeight - minHeight : 0f):F1}m " +
            $"max250mStep={maxStep:F1}m safeFlight={entry.SafeFlight} airdrop={entry.Airdrop}");
    }

    private sealed class FlightEntry
    {
        internal FlightEntry(Aircraft aircraft, GlobalPosition target, bool safeFlight, bool airdrop)
        {
            Aircraft = aircraft;
            Target = target;
            SafeFlight = safeFlight;
            Airdrop = airdrop;
        }

        internal Aircraft Aircraft { get; }
        internal GlobalPosition Target { get; }
        internal bool SafeFlight { get; }
        internal bool Airdrop { get; }
        internal float NextRuntimeLog { get; set; }
        internal bool LossLogged { get; set; }
    }
}
