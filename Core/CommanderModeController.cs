using UnityEngine;
using UnityEngine.SceneManagement;

namespace NuclearOptionCommander;

internal sealed class CommanderModeController : MonoBehaviour
{
    private CommanderCameraController? cameraController;
    private CommanderCursorController? cursorController;
    private CommanderSelectionService? selectionService;
    private CommanderFactionVehicleService? factionVehicleService;
    private CommanderCameraFollowService? cameraFollowService;
    private CommanderTacticalMapService? tacticalMapService;
    private CommanderRadarService? radarService;
    private CommanderMobileEmplacementService? mobileEmplacementService;
    private CommanderDirectPathService? directPathService;
    private CommanderRepairService? repairService;
    private CommanderSupplyHeliService? supplyHeliService;
    private CommanderAirCommandService? airCommandService;
    private CommanderSpawnService? spawnService;
    private CommanderMarkerService? markerService;
    private CommanderMoveService? moveService;
    private CommanderOverlayUi? overlayUi;
    private CommanderInputController? inputController;
    private CommanderPersistentOperations? persistentOperations;
    private float nextInactiveEntryProbeAt;
    private bool aircraftSelectionMenuPresent;

    internal bool IsActive { get; private set; }

    private void Awake()
    {
        CommanderUiScale.ApplyResolutionPreset();
        cameraController = new CommanderCameraController();
        cursorController = new CommanderCursorController();
        selectionService = new CommanderSelectionService();
        cameraFollowService = new CommanderCameraFollowService(selectionService);
        factionVehicleService = new CommanderFactionVehicleService();
        tacticalMapService = new CommanderTacticalMapService(cameraFollowService);
        radarService = new CommanderRadarService(selectionService);
        mobileEmplacementService = new CommanderMobileEmplacementService(selectionService);
        directPathService = new CommanderDirectPathService(selectionService);
        repairService = new CommanderRepairService();
        supplyHeliService = new CommanderSupplyHeliService();
        airCommandService = new CommanderAirCommandService(tacticalMapService);
        spawnService = new CommanderSpawnService(selectionService, factionVehicleService, tacticalMapService);
        persistentOperations = new CommanderPersistentOperations(
            spawnService,
            supplyHeliService,
            airCommandService,
            mobileEmplacementService);
        markerService = new CommanderMarkerService(selectionService);
        moveService = new CommanderMoveService(selectionService);
        overlayUi = new CommanderOverlayUi(
            selectionService,
            moveService,
            spawnService,
            radarService,
            mobileEmplacementService,
            repairService,
            directPathService,
            supplyHeliService,
            airCommandService,
            () => Deactivate());
        inputController = new CommanderInputController(
            overlayUi,
            selectionService,
            spawnService,
            markerService,
            moveService,
            tacticalMapService,
            supplyHeliService,
            mobileEmplacementService,
            airCommandService);
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void Update()
    {
        CommanderUiScale.RefreshResolutionPreset();
        persistentOperations?.Tick();
        if (!IsActive)
        {
            return;
        }

        if (CommanderSettings.ToggleUi.IsDown())
        {
            overlayUi?.ToggleScreenshotUi();
        }

        if (GameManager.GetLocalAircraft(out _))
        {
            Deactivate(restorePreviousCamera: false);
            return;
        }

        cursorController?.Tick();
        selectionService?.Tick();
        cameraFollowService?.Tick();
        moveService?.Tick();
        markerService?.Tick();
        tacticalMapService?.Tick();
        radarService?.Tick();
        mobileEmplacementService?.TickActive();
        supplyHeliService?.TickActive();
        airCommandService?.TickActive();
        spawnService?.TickActive();
        overlayUi?.Tick();
        inputController?.Tick();
    }

    private void OnGUI()
    {
        Matrix4x4 previousMatrix = CommanderUiScale.Begin();
        try
        {
            if (!IsActive)
            {
                if (ShouldShowCommanderEntry())
                {
                    overlayUi?.DrawInactiveLauncher(Activate);
                }
                return;
            }

            overlayUi?.Draw();
            if (overlayUi?.ShowTacticalMapUi == true)
            {
                tacticalMapService?.DrawControls();
            }
        }
        finally
        {
            CommanderUiScale.End(previousMatrix);
        }
    }

    private bool ShouldShowCommanderEntry()
    {
        if (GameManager.GetLocalAircraft(out _)
            || (GameManager.gameState != GameState.SinglePlayer && GameManager.gameState != GameState.Multiplayer))
        {
            return false;
        }

        if (DynamicMap.mapMaximized)
        {
            return true;
        }

        if (Time.unscaledTime >= nextInactiveEntryProbeAt)
        {
            nextInactiveEntryProbeAt = Time.unscaledTime + 0.75f;
            aircraftSelectionMenuPresent = UnityEngine.Object.FindObjectOfType<AircraftSelectionMenu>() != null;
        }
        return aircraftSelectionMenuPresent;
    }

    private void OnDisable()
    {
        Deactivate();
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        Deactivate(restorePreviousCamera: false);
    }

    private void OnApplicationQuit()
    {
        Deactivate();
    }

    internal void Toggle()
    {
        if (IsActive)
        {
            Deactivate();
            return;
        }

        Activate();
    }

    private void Activate()
    {
        if (IsActive)
        {
            return;
        }

        if (GameManager.GetLocalAircraft(out _))
        {
            CommanderPlugin.Log.LogWarning("Commander mode is only available while the player is outside an aircraft.");
            return;
        }

        AircraftSelectionMenu? aircraftSelectionMenu = UnityEngine.Object.FindObjectOfType<AircraftSelectionMenu>();
        if (aircraftSelectionMenu != null && aircraftSelectionMenu.gameObject.activeInHierarchy)
        {
            aircraftSelectionMenu.ReturnToMap();
        }

        if (cameraController == null || !cameraController.TryActivate())
        {
            CommanderPlugin.Log.LogWarning("Commander mode could not start because the free camera is not available yet.");
            return;
        }

        cursorController?.Activate();
        IsActive = true;
        selectionService?.Activate();
        markerService?.Activate();
        radarService?.Activate();
        mobileEmplacementService?.Activate();
        supplyHeliService?.Activate();
        airCommandService?.Activate();
        spawnService?.Activate();
        overlayUi?.Activate();
        if (!string.IsNullOrEmpty(cameraController.MissingBindingWarning))
        {
            overlayUi?.ShowCameraBindingWarning(cameraController.MissingBindingWarning);
        }
        if (overlayUi?.ShowTacticalMapUi == true)
        {
            tacticalMapService?.Open();
        }
        CommanderPlugin.Log.LogInfo("Commander mode enabled.");
    }

    private void Deactivate(bool restorePreviousCamera = true)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        selectionService?.Deactivate();
        cameraFollowService?.Disable();
        markerService?.Deactivate();
        radarService?.Deactivate();
        mobileEmplacementService?.Deactivate();
        directPathService?.Deactivate();
        supplyHeliService?.Deactivate();
        airCommandService?.Deactivate();
        spawnService?.Deactivate();
        overlayUi?.Deactivate();
        tacticalMapService?.Close();
        cursorController?.Deactivate();
        cameraController?.Deactivate(restorePreviousCamera);
        CommanderPlugin.Log.LogInfo("Commander mode disabled.");
    }

    private void OnActiveSceneChanged(Scene previousScene, Scene newScene)
    {
        Deactivate(restorePreviousCamera: false);
        selectionService?.ResetSession();
        cameraFollowService?.Disable();
        tacticalMapService?.ResetSession();
        radarService?.ResetSession();
        mobileEmplacementService?.ResetSession();
        directPathService?.ResetSession();
        repairService?.ResetSession();
        supplyHeliService?.ResetSession();
        airCommandService?.ResetSession();
        aircraftSelectionMenuPresent = false;
        nextInactiveEntryProbeAt = 0f;
        factionVehicleService?.ResetSession();
        spawnService?.ResetSession();
    }

}
