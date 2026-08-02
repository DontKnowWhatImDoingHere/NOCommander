using HarmonyLib;
using NuclearOption.MissionEditorScripts;
using Rewired;
using System.Reflection;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderCameraFollowService
{
    private const float PovMovementScale = 1f / 6f;
    private const float PovBoostedMovementScale = 0.5f;
    private static readonly FieldInfo? FreeCameraPanField = AccessTools.Field(typeof(CameraFreeState), "panView");
    private static readonly FieldInfo? FreeCameraTiltField = AccessTools.Field(typeof(CameraFreeState), "tiltView");

    private readonly CommanderSelectionService selectionService;
    private Unit? target;
    private Vector3 lastGlobalPosition;
    private Quaternion lastTargetRotation;
    private Vector3 povLocalPosition;
    private Quaternion povLocalRotation;
    private float spacePressedAt;
    private bool spaceHeld;
    private bool longSpaceTriggered;

    internal CommanderCameraFollowService(CommanderSelectionService selectionService)
    {
        this.selectionService = selectionService;
        Instance = this;
    }

    internal static CommanderCameraFollowService? Instance { get; private set; }
    internal bool Enabled { get; private set; }
    internal bool FollowRotation { get; private set; }
    internal bool PovMode { get; private set; }
    internal static bool IsPovActive => Instance?.Enabled == true && Instance.PovMode;
    internal bool CanFollow => selectionService.FocusedSelection is Unit unit && !unit.disabled;
    internal Aircraft? FollowedAircraft => Enabled ? target as Aircraft : null;

    internal void Toggle()
    {
        if (Enabled)
        {
            Disable();
            return;
        }

        Unit? selected = selectionService.FocusedSelection;
        if (selected == null || selected.disabled)
        {
            return;
        }

        target = selected;
        lastGlobalPosition = selected.GlobalPosition().AsVector3();
        lastTargetRotation = selected.transform.rotation;
        Enabled = true;
    }

    internal void ToggleRotation()
    {
        if (!CanFollow)
        {
            return;
        }

        if (!Enabled)
        {
            Toggle();
        }

        FollowRotation = !FollowRotation;
        PovMode = false;
        Unit? selected = selectionService.FocusedSelection;
        if (selected != null)
        {
            lastTargetRotation = selected.transform.rotation;
        }
    }

    internal void TogglePov()
    {
        if (!CanFollow)
        {
            return;
        }

        if (!Enabled)
        {
            Toggle();
        }

        PovMode = !PovMode;
        FollowRotation = false;
        CapturePovOffset();
    }

    internal void CenterOnSelection()
    {
        Unit? selected = selectionService.FocusedSelection;
        CameraStateManager? cameraManager = SceneSingleton<CameraStateManager>.i;
        if (selected == null || selected.disabled || cameraManager == null)
        {
            return;
        }

        float length = selected.definition != null ? selected.definition.length : selected.maxRadius * 2f;
        float distance = Mathf.Max(20f, selected.maxRadius * 4f, length * 2f);
        Vector3 targetPosition = selected.transform.position + Vector3.up * Mathf.Max(1f, selected.maxRadius * 0.35f);
        Vector3 viewDirection = cameraManager.transform.forward;
        if (viewDirection.sqrMagnitude < 0.1f)
        {
            viewDirection = -selected.transform.forward;
        }

        cameraManager.transform.position = targetPosition - viewDirection.normalized * distance;
        cameraManager.transform.rotation = Quaternion.LookRotation(targetPosition - cameraManager.transform.position, Vector3.up);
        cameraManager.cameraVelocity = Vector3.zero;

        target = selected;
        lastGlobalPosition = selected.GlobalPosition().AsVector3();
        lastTargetRotation = selected.transform.rotation;
        if (PovMode)
        {
            CapturePovOffset();
        }
    }

    internal void CenterOnSelectionIfFollowing()
    {
        if (Enabled)
        {
            CenterOnSelection();
        }
    }

    internal void Tick()
    {
        HandleSpaceShortcut();
        if (!Enabled)
        {
            return;
        }

        Unit? selected = selectionService.FocusedSelection;
        if (selected == null || selected.disabled)
        {
            Disable();
            return;
        }

        Vector3 currentGlobalPosition = selected.GlobalPosition().AsVector3();
        if (!ReferenceEquals(selected, target))
        {
            target = selected;
            lastGlobalPosition = currentGlobalPosition;
            lastTargetRotation = selected.transform.rotation;
            CapturePovOffset();
            return;
        }

        CameraStateManager? cameraManager = SceneSingleton<CameraStateManager>.i;
        if (cameraManager != null)
        {
            if (PovMode)
            {
                cameraManager.transform.position = selected.transform.TransformPoint(povLocalPosition);
                cameraManager.transform.rotation = selected.transform.rotation * povLocalRotation;
                SyncFreeCameraAngles(cameraManager);
            }
            else
            {
                cameraManager.transform.position += currentGlobalPosition - lastGlobalPosition;
                if (FollowRotation)
                {
                    Quaternion rotationDelta = selected.transform.rotation * Quaternion.Inverse(lastTargetRotation);
                    cameraManager.transform.rotation = rotationDelta * cameraManager.transform.rotation;
                }
            }
        }
        lastGlobalPosition = currentGlobalPosition;
        lastTargetRotation = selected.transform.rotation;
    }

    internal void Disable()
    {
        Enabled = false;
        FollowRotation = false;
        PovMode = false;
        target = null;
    }

    internal static void ApplyCommanderLatePose(CameraStateManager cameraManager)
    {
        CommanderCameraFollowService? service = Instance;
        Unit? selected = service?.selectionService.FocusedSelection;
        if (service == null || !service.Enabled || selected == null || selected.disabled)
        {
            return;
        }

        if (!service.PovMode)
        {
            return;
        }

        // Keep FreeCam translation, but rebuild rotation from the complete unit pose.
        // CameraFreeState always produces a zero-roll Euler rotation, which otherwise
        // slowly removes aircraft bank from an attached POV.
        Vector3 attachedPosition = selected.transform.TransformPoint(service.povLocalPosition);
        float movementScale = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
            ? PovBoostedMovementScale
            : PovMovementScale;
        cameraManager.transform.position = Vector3.LerpUnclamped(
            attachedPosition,
            cameraManager.transform.position,
            movementScale);
        service.povLocalPosition = selected.transform.InverseTransformPoint(cameraManager.transform.position);
        service.ApplyPovLookInput(cameraManager);
        cameraManager.transform.rotation = selected.transform.rotation * service.povLocalRotation;
        SyncFreeCameraAngles(cameraManager);
    }

    private void HandleSpaceShortcut()
    {
        var shortcut = CommanderSettings.CameraCenterFollow;
        if (shortcut.IsDown())
        {
            spaceHeld = true;
            longSpaceTriggered = false;
            spacePressedAt = Time.unscaledTime;
        }

        if (spaceHeld && !longSpaceTriggered && shortcut.IsPressed() && Time.unscaledTime - spacePressedAt >= 0.45f)
        {
            if (!Enabled)
            {
                Toggle();
            }
            CenterOnSelection();
            longSpaceTriggered = true;
        }

        if (spaceHeld && shortcut.IsUp())
        {
            if (!longSpaceTriggered)
            {
                CenterOnSelection();
            }
            spaceHeld = false;
        }
    }

    private void CapturePovOffset()
    {
        Unit? selected = selectionService.FocusedSelection;
        CameraStateManager? cameraManager = SceneSingleton<CameraStateManager>.i;
        if (selected == null || cameraManager == null)
        {
            return;
        }

        povLocalPosition = selected.transform.InverseTransformPoint(cameraManager.transform.position);
        Vector3 localForward = Quaternion.Inverse(selected.transform.rotation)
            * cameraManager.transform.forward;
        if (localForward.sqrMagnitude < 0.001f)
        {
            localForward = Vector3.forward;
        }

        // Preserve where the camera is looking, but do not preserve world-level roll as
        // a counter-rotation. POV roll must come entirely from the attached unit.
        Vector3 localUp = Mathf.Abs(Vector3.Dot(localForward.normalized, Vector3.up)) > 0.995f
            ? Vector3.forward
            : Vector3.up;
        povLocalRotation = Quaternion.LookRotation(localForward, localUp);
        cameraManager.transform.rotation = selected.transform.rotation * povLocalRotation;
        SyncFreeCameraAngles(cameraManager);
    }

    private static void SyncFreeCameraAngles(CameraStateManager cameraManager)
    {
        if (cameraManager.currentState != cameraManager.freeState)
        {
            return;
        }

        Vector3 euler = cameraManager.transform.eulerAngles;
        FreeCameraPanField?.SetValue(cameraManager.freeState, euler.y);
        FreeCameraTiltField?.SetValue(cameraManager.freeState, euler.x);
    }

    private void ApplyPovLookInput(CameraStateManager cameraManager)
    {
        Player? player = GameManager.playerInput;
        if (player == null
            || InputFieldChecker.InsideInputField
            || !player.GetButton("Free Look"))
        {
            return;
        }

        float fovScale = Mathf.Min(cameraManager.mainCamera.fieldOfView / 20f, 1f);
        float pitch = fovScale
            * player.GetAxis("Tilt View")
            * 0.3f
            * PlayerSettings.viewSensitivity;
        float yaw = fovScale
            * player.GetAxis("Pan View")
            * 0.3f
            * PlayerSettings.viewSensitivity
            * (PlayerSettings.viewInvertPitch ? -1f : 1f);

        if (Mathf.Approximately(pitch, 0f) && Mathf.Approximately(yaw, 0f))
        {
            return;
        }

        povLocalRotation = Quaternion.AngleAxis(yaw, Vector3.up)
            * povLocalRotation
            * Quaternion.AngleAxis(pitch, Vector3.right);
    }
}
