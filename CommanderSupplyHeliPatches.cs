using System;
using HarmonyLib;
using UnityEngine;

namespace NuclearOptionCommander;

[HarmonyPatch]
internal static class CommanderSupplyHeliPatches
{
    [HarmonyPatch(typeof(CameraStateManager), "LateUpdate")]
    [HarmonyPostfix]
    private static void CameraLateUpdatePostfix(CameraStateManager __instance)
    {
        CommanderCameraFollowService.ApplyCommanderLatePose(__instance);
    }

    [HarmonyPatch(typeof(FactionHQ), nameof(FactionHQ.RegisterFactionUnit))]
    [HarmonyPostfix]
    private static void RegisterFactionUnitPostfix(FactionHQ __instance, Unit unit)
    {
        CommanderSupplyHeliService.NotifyFactionUnitRegistered(__instance, unit);
    }

    [HarmonyPatch(typeof(Unit), nameof(Unit.ReportKilled))]
    [HarmonyPostfix]
    private static void ReportKilledPostfix(Unit __instance)
    {
        CommanderSupplyDebugTelemetry.NotifyAircraftKilled(__instance);
    }

    [HarmonyPatch(typeof(AIHeloTransportState), "SearchForLandingSpot")]
    [HarmonyPrefix]
    private static bool SearchForLandingSpotPrefix(AIHeloTransportState __instance)
    {
        return !CommanderSupplyHeliService.TryOverrideTransportTarget(__instance);
    }

    [HarmonyPatch(typeof(AIHeloTransportState), nameof(AIHeloTransportState.LeaveState))]
    [HarmonyPostfix]
    private static void LeaveStatePostfix(AIHeloTransportState __instance)
    {
        CommanderSupplyHeliService.NotifyTransportStateLeft(__instance);
    }

    [HarmonyPatch(typeof(AIHeloTransportState), "DeployCargo")]
    [HarmonyPrefix]
    private static bool DeployCargoPrefix(AIHeloTransportState __instance)
    {
        return !CommanderSupplyHeliService.TryDeployAssignedCargo(__instance);
    }

    [HarmonyPatch(typeof(Pilot), nameof(Pilot.SwitchState))]
    [HarmonyPrefix]
    private static bool SwitchStatePrefix(Pilot __instance, PilotBaseState state)
    {
        return !CommanderSupplyHeliService.ShouldDelayCargoTakeoff(__instance, state);
    }

    [HarmonyPatch(typeof(Aircraft), nameof(Aircraft.ReturnToInventory))]
    [HarmonyPostfix]
    private static void ReturnToInventoryPostfix(Aircraft __instance)
    {
        CommanderSupplyHeliService.NotifyAircraftReturned(__instance);
    }

    [HarmonyPatch(typeof(MountedCargo), nameof(MountedCargo.ActivateCargoVehicle))]
    [HarmonyPostfix]
    private static void ActivateCargoVehiclePostfix(
        MountedCargo __instance,
        Unit cargoUnit,
        Collider cargoCollider,
        PhysicMaterial cargoColliderMaterial)
    {
        if (__instance.attachedUnit is Aircraft aircraft && cargoUnit != null)
        {
            CommanderSupplyHeliService.NotifyCargoActivated(aircraft, cargoUnit);
        }
    }

    [HarmonyPatch(
        typeof(AutopilotHelo),
        nameof(AutopilotHelo.AutoAim),
        new Type[] { typeof(GlobalPosition), typeof(float), typeof(Vector3), typeof(Vector3), typeof(bool) })]
    [HarmonyPrefix]
    private static void HeloAutoAimPrefix(
        AutopilotHelo __instance,
        ref float altitudeHold,
        bool followTerrain)
    {
        CommanderSupplyHeliService.RaiseAssignedTerrainClearance(__instance, ref altitudeHold, followTerrain);
        CommanderSupplyDebugTelemetry.RecordAutoAim(__instance, altitudeHold, followTerrain);
    }

    [HarmonyPatch(
        typeof(AutopilotTiltwing),
        nameof(AutopilotTiltwing.AutoAim),
        new Type[] { typeof(GlobalPosition), typeof(float), typeof(Vector3), typeof(Vector3), typeof(bool) })]
    [HarmonyPrefix]
    private static void TiltwingAutoAimPrefix(
        AutopilotTiltwing __instance,
        ref float altitudeHold,
        bool followTerrain)
    {
        CommanderSupplyHeliService.RaiseAssignedTerrainClearance(__instance, ref altitudeHold, followTerrain);
        CommanderSupplyDebugTelemetry.RecordAutoAim(__instance, altitudeHold, followTerrain);
    }
}
