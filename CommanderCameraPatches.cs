using HarmonyLib;
using UnityEngine;

namespace NuclearOptionCommander;

[HarmonyPatch(typeof(CameraStateManager), nameof(CameraStateManager.SetFollowingUnit))]
internal static class CommanderCameraFollowingPatch
{
    private static bool Prefix(Unit unit)
    {
        if (CommanderPlugin.Instance?.IsCommanderModeActive != true || !DynamicMap.mapMaximized)
        {
            return true;
        }

        if (CommanderTacticalMapService.Instance?.SuppressMapFollow == true)
        {
            return false;
        }

        return !CommanderGameAccess.ShouldAllowCommanderSelection(unit, CommanderGameAccess.GetLocalHq());
    }
}

[HarmonyPatch(typeof(SonicBoomManager), nameof(SonicBoomManager.ManageSonicBooms))]
internal static class CommanderFollowSonicBoomPatch
{
    private static void Prefix(ref Vector3 __state)
    {
        CameraStateManager? camera = SceneSingleton<CameraStateManager>.i;
        __state = camera != null ? camera.cameraVelocity : Vector3.zero;
        Aircraft? followed = CommanderCameraFollowService.Instance?.FollowedAircraft;
        if (camera != null && followed?.rb != null)
        {
            // SonicBoomManager only needs the listener velocity during this call.
            // Restoring it immediately avoids feeding aircraft speed into FreeCam.
            camera.cameraVelocity = followed.rb.velocity;
        }
    }

    private static void Postfix(Vector3 __state)
    {
        CameraStateManager? camera = SceneSingleton<CameraStateManager>.i;
        if (camera != null)
        {
            camera.cameraVelocity = __state;
        }
    }
}
