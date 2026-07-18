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
    private CommanderSupplyHeliService? supplyHeliService;
    private CommanderAirCommandService? airCommandService;
    private CommanderSpawnService? spawnService;
    private CommanderMarkerService? markerService;
    private CommanderMoveService? moveService;
    private CommanderOverlayUi? overlayUi;
    private CommanderInputController? inputController;

    internal bool IsActive { get; private set; }

    private void Awake()
    {
        cameraController = new CommanderCameraController();
        cursorController = new CommanderCursorController();
        selectionService = new CommanderSelectionService();
        cameraFollowService = new CommanderCameraFollowService(selectionService);
        factionVehicleService = new CommanderFactionVehicleService();
        tacticalMapService = new CommanderTacticalMapService(cameraFollowService);
        radarService = new CommanderRadarService(selectionService);
        mobileEmplacementService = new CommanderMobileEmplacementService(selectionService);
        directPathService = new CommanderDirectPathService(selectionService);
        supplyHeliService = new CommanderSupplyHeliService();
        airCommandService = new CommanderAirCommandService(tacticalMapService);
        spawnService = new CommanderSpawnService(selectionService, factionVehicleService, tacticalMapService);
        markerService = new CommanderMarkerService(selectionService);
        moveService = new CommanderMoveService(selectionService);
        overlayUi = new CommanderOverlayUi(
            selectionService,
            moveService,
            spawnService,
            radarService,
            mobileEmplacementService,
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
        if (Input.GetKeyDown(KeyCode.F8))
        {
            Toggle();
        }

        if (!IsActive)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.H))
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
        mobileEmplacementService?.Tick();
        supplyHeliService?.Tick();
        airCommandService?.Tick();
        spawnService?.Tick();
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

    private static bool ShouldShowCommanderEntry()
    {
        if (GameManager.GetLocalAircraft(out _)
            || (GameManager.gameState != GameState.SinglePlayer && GameManager.gameState != GameState.Multiplayer))
        {
            return false;
        }

        return DynamicMap.mapMaximized || UnityEngine.Object.FindObjectOfType<AircraftSelectionMenu>() != null;
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
        CommanderVehicleDefinitionDumper.DumpAllGroundVehicles();
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
        supplyHeliService?.ResetSession();
        airCommandService?.ResetSession();
        factionVehicleService?.ResetSession();
        spawnService?.ResetSession();
    }
}
