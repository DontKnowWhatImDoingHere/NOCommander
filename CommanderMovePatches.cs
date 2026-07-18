using HarmonyLib;
using NuclearOption.Networking;

namespace NuclearOptionCommander;

[HarmonyPatch(typeof(UnitCommand), "ServerSetDestination")]
internal static class CommanderMoveDestinationPatch
{
    private static void Postfix(UnitCommand __instance, GlobalPosition waypoint, Player player)
    {
        if (CommanderPlugin.Instance?.IsCommanderModeActive == true)
        {
            CommanderMoveService.NotifyPlayerDestination(__instance, waypoint, player);
        }
    }
}
