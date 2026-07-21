using Rewired;

namespace NuclearOptionCommander;

internal sealed class CommanderCameraController
{
    private CameraBaseState? previousState;
    private CameraStateManager? activeManager;
    private bool active;

    internal string MissingBindingWarning { get; private set; } = string.Empty;

    internal bool TryActivate()
    {
        CameraStateManager? cameraManager = SceneSingleton<CameraStateManager>.i;
        if (cameraManager == null || cameraManager.freeState == null)
        {
            return false;
        }

        activeManager = cameraManager;
        previousState = cameraManager.currentState;
        if (cameraManager.currentState != cameraManager.freeState)
        {
            cameraManager.SwitchState(cameraManager.freeState);
        }

        active = true;
        DetectMissingBindings();
        return true;
    }

    internal void Deactivate(bool restorePreviousState = true)
    {
        if (!active)
        {
            return;
        }

        CameraStateManager? cameraManager = SceneSingleton<CameraStateManager>.i;
        if (restorePreviousState
            && ReferenceEquals(cameraManager, activeManager)
            && cameraManager != null
            && previousState != null
            && cameraManager.currentState != previousState)
        {
            cameraManager.SwitchState(previousState);
        }

        activeManager = null;
        previousState = null;
        active = false;
    }

    private void DetectMissingBindings()
    {
        Player? player = GameManager.playerInput;
        if (player == null)
        {
            MissingBindingWarning = string.Empty;
            return;
        }

        bool missingForward = !HasKeyboardPole(player, "Move Longitudinal", Pole.Positive);
        bool missingBackward = !HasKeyboardPole(player, "Move Longitudinal", Pole.Negative);
        bool missingFreeLook = !HasBinding(player, "Free Look", ControllerType.Keyboard)
            && !HasBinding(player, "Free Look", ControllerType.Mouse);

        string missing = string.Empty;
        if (missingForward) missing = "Move Longitudinal: Forward";
        if (missingBackward) missing += (missing.Length > 0 ? ", " : string.Empty) + "Move Longitudinal: Backward";
        if (missingFreeLook) missing += (missing.Length > 0 ? ", " : string.Empty) + "Free Look";
        MissingBindingWarning = missing;

    }

    private static bool HasKeyboardPole(Player? player, string action, Pole pole)
    {
        if (player == null)
        {
            return false;
        }

        foreach (ActionElementMap map in player.controllers.maps.ElementMapsWithAction(
            ControllerType.Keyboard, action, false))
        {
            if (map.axisContribution == pole)
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasBinding(Player? player, string action, ControllerType controllerType)
    {
        if (player == null)
        {
            return false;
        }

        foreach (ActionElementMap map in player.controllers.maps.ElementMapsWithAction(
            controllerType, action, false))
        {
            return true;
        }
        return false;
    }
}
