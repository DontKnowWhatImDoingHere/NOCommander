using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderInputController
{
    private readonly CommanderOverlayUi overlayUi;
    private readonly CommanderSelectionService selectionService;
    private readonly CommanderSpawnService spawnService;
    private readonly CommanderMarkerService markerService;
    private readonly CommanderMoveService moveService;
    private readonly CommanderTacticalMapService tacticalMapService;
    private readonly CommanderSupplyHeliService supplyHeliService;
    private readonly CommanderMobileEmplacementService mobileEmplacementService;
    private readonly CommanderAirCommandService airCommandService;

    internal CommanderInputController(
        CommanderOverlayUi overlayUi,
        CommanderSelectionService selectionService,
        CommanderSpawnService spawnService,
        CommanderMarkerService markerService,
        CommanderMoveService moveService,
        CommanderTacticalMapService tacticalMapService,
        CommanderSupplyHeliService supplyHeliService,
        CommanderMobileEmplacementService mobileEmplacementService,
        CommanderAirCommandService airCommandService)
    {
        this.overlayUi = overlayUi;
        this.selectionService = selectionService;
        this.spawnService = spawnService;
        this.markerService = markerService;
        this.moveService = moveService;
        this.tacticalMapService = tacticalMapService;
        this.supplyHeliService = supplyHeliService;
        this.mobileEmplacementService = mobileEmplacementService;
        this.airCommandService = airCommandService;
    }

    internal void Tick()
    {
        if (spawnService.IsMapInteractionActive())
        {
            return;
        }

        Vector2 mousePosition = Input.mousePosition;
        if (tacticalMapService.ContainsScreenPoint(mousePosition))
        {
            return;
        }

        DynamicMap? dynamicMap = SceneSingleton<DynamicMap>.i;
        if (tacticalMapService.IsOpen && dynamicMap != null && dynamicMap.IsCursorInMapRectangle())
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            HandlePrimaryClick(mousePosition);
        }

        if (Input.GetMouseButtonDown(1))
        {
            HandleSecondaryClick(mousePosition);
        }
    }

    private void HandlePrimaryClick(Vector2 mousePosition)
    {
        if (overlayUi.ContainsScreenPoint(mousePosition))
        {
            return;
        }

        if (supplyHeliService.AwaitingTargetSelection)
        {
            supplyHeliService.TrySpawnAtWorldPoint(mousePosition);
            return;
        }

        if (airCommandService.AwaitingAreaSelection)
        {
            airCommandService.TrySetAreaFromWorld(mousePosition);
            return;
        }

        if (mobileEmplacementService.AwaitingDestination)
        {
            mobileEmplacementService.TrySetDestinationFromWorld(mousePosition);
            return;
        }

        if (spawnService.AwaitingRallyPointSelection)
        {
            spawnService.TrySetRallyPointFromWorld(mousePosition);
            return;
        }

        bool additive = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (markerService.TryGetMarkerUnitAt(mousePosition, out Unit markerUnit))
        {
            selectionService.SelectUnit(markerUnit, additive);
            CommanderCameraFollowService.Instance?.CenterOnSelectionIfFollowing();
            return;
        }

        if (CommanderGameAccess.TryRaycastSelectableUnit(mousePosition, out Unit worldUnit))
        {
            selectionService.SelectUnit(worldUnit, additive);
            CommanderCameraFollowService.Instance?.CenterOnSelectionIfFollowing();
            return;
        }

        if (!additive)
        {
            selectionService.DeselectAll();
        }
    }

    private void HandleSecondaryClick(Vector2 mousePosition)
    {
        if (overlayUi.ContainsScreenPoint(mousePosition))
        {
            return;
        }

        moveService.TryIssueMoveOrder(mousePosition);
    }
}
