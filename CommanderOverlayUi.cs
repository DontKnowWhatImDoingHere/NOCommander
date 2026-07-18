using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderOverlayUi
{
    private const int WindowId = 0x434F4D4D;
    private const int ReserveWindowId = 0x434F4D52;
    private const int PinnedWindowId = 0x434F4D50;
    private const int RadarWindowId = 0x434F4D44;

    private readonly CommanderSelectionService selectionService;
    private readonly CommanderMoveService moveService;
    private readonly CommanderSpawnService spawnService;
    private readonly CommanderRadarService radarService;
    private readonly CommanderMobileEmplacementService mobileEmplacementService;
    private readonly CommanderDirectPathService directPathService;
    private readonly CommanderSupplyHeliService supplyHeliService;
    private readonly CommanderAirCommandService airCommandService;
    private readonly CommanderSupplyHeliUi supplyHeliUi;
    private readonly CommanderAirCommandUi airCommandUi;
    private readonly CommanderDepotUi depotUi;
    private readonly CommanderWorldMarkerRenderer worldMarkerRenderer;
    private readonly Action exitCommander;

    private bool panelVisible;
    private bool reserveWindowVisible;
    private bool panelHelpVisible;
    private bool reserveHelpVisible;
    private bool pinnedHelpVisible;
    private bool radarHelpVisible;
    private bool selectionHelpVisible;
    private bool settingsVisible;
    private bool uiSettingsVisible;
    private bool pinnedWindowVisible = true;
    private bool pinnedShowsMissions;
    private bool showSupplyMissions = true;
    private bool showAirCommandMissions = true;
    private bool screenshotUiHidden;
    private bool showCommandButton = true;
    private bool showFactionMoney = true;
    private bool showTacticalMap = true;
    private bool showSelectionBar = true;
    private bool showPinnedUnits = true;
    private bool showUnitSystems = true;
    private bool showDepotUi = true;
    private bool showSupplyUi = true;
    private bool showAirCommandUi = true;
    private bool showWorldMarkers = true;
    private bool reserveShowsUnits;
    private bool positionsInitialized;
    private Rect launcherRect;
    private Rect moneyRect;
    private Rect panelRect;
    private Rect reserveWindowRect;
    private Rect selectionBarRect;
    private Rect pinnedWindowRect;
    private Rect radarWindowRect;
    private Rect selectionHelpRect;
    private Rect pinnedLauncherRect;
    private Vector2 reserveScroll;
    private Vector2 pinnedScroll;
    private GUIStyle? ghostCommandStyle;

    internal CommanderOverlayUi(
        CommanderSelectionService selectionService,
        CommanderMoveService moveService,
        CommanderSpawnService spawnService,
        CommanderRadarService radarService,
        CommanderMobileEmplacementService mobileEmplacementService,
        CommanderDirectPathService directPathService,
        CommanderSupplyHeliService supplyHeliService,
        CommanderAirCommandService airCommandService,
        Action exitCommander)
    {
        this.selectionService = selectionService;
        this.moveService = moveService;
        this.spawnService = spawnService;
        this.radarService = radarService;
        this.mobileEmplacementService = mobileEmplacementService;
        this.directPathService = directPathService;
        this.supplyHeliService = supplyHeliService;
        this.airCommandService = airCommandService;
        this.exitCommander = exitCommander;
        supplyHeliUi = new CommanderSupplyHeliUi(supplyHeliService);
        airCommandUi = new CommanderAirCommandUi(airCommandService);
        depotUi = new CommanderDepotUi(spawnService);
        worldMarkerRenderer = new CommanderWorldMarkerRenderer(selectionService, moveService, spawnService, supplyHeliService);
    }

    internal void Activate()
    {
        panelVisible = false;
        reserveWindowVisible = false;
        panelHelpVisible = false;
        reserveHelpVisible = false;
        supplyHeliUi.Hide();
        airCommandUi.Hide();
        depotUi.Reset();
        screenshotUiHidden = false;
    }

    internal void Deactivate()
    {
        panelVisible = false;
        reserveWindowVisible = false;
        supplyHeliUi.Hide();
        airCommandUi.Hide();
        depotUi.Reset();
    }

    internal void Tick()
    {
        CommanderUiTheme.Ensure();
        if (!showTacticalMap && CommanderTacticalMapService.Instance?.IsOpen == true)
        {
            CommanderTacticalMapService.Instance.Close();
        }
        float centerY = CommanderUiScale.Height * 0.5f;
        launcherRect = new Rect(10f, centerY - 42f, 52f, 84f);
        moneyRect = new Rect((CommanderUiScale.Width - 250f) * 0.5f, 10f, 250f, 38f);

        if (!positionsInitialized)
        {
            float panelHeight = Mathf.Min(650f, CommanderUiScale.Height - 24f);
            panelRect = new Rect(74f, Mathf.Max(12f, centerY - panelHeight * 0.5f), 400f, panelHeight);
            float reserveWidth = Mathf.Min(590f, CommanderUiScale.Width - 24f);
            float reserveHeight = Mathf.Min(610f, CommanderUiScale.Height - 24f);
            reserveWindowRect = new Rect(
                Mathf.Max(12f, CommanderUiScale.Width - reserveWidth - 12f),
                Mathf.Max(58f, CommanderUiScale.Height - reserveHeight - 12f),
                reserveWidth,
                reserveHeight);
            pinnedWindowRect = new Rect(
                Mathf.Max(12f, CommanderUiScale.Width - 354f),
                Mathf.Clamp(CommanderUiScale.Height * 0.66f - 170f, 58f, CommanderUiScale.Height - 352f),
                342f,
                340f);
            radarWindowRect = new Rect(
                Mathf.Max(12f, CommanderUiScale.Width - 354f),
                Mathf.Clamp(CommanderUiScale.Height * 0.66f - 418f, 58f, CommanderUiScale.Height - 330f),
                342f,
                318f);
            positionsInitialized = true;
        }
        else
        {
            panelRect.height = Mathf.Min(650f, CommanderUiScale.Height - 24f);
            reserveWindowRect.width = Mathf.Min(590f, CommanderUiScale.Width - 24f);
            reserveWindowRect.height = Mathf.Min(610f, CommanderUiScale.Height - 24f);
        }
        panelRect = CommanderUiTheme.ClampWindow(panelRect);
        reserveWindowRect = CommanderUiTheme.ClampWindow(reserveWindowRect);
        pinnedWindowRect = CommanderUiTheme.ClampWindow(pinnedWindowRect);
        pinnedLauncherRect = new Rect(Mathf.Max(8f, pinnedWindowRect.x - 68f), pinnedWindowRect.y, 62f, 28f);
        radarWindowRect = CommanderUiTheme.ClampWindow(radarWindowRect);
        selectionBarRect = new Rect(
            Mathf.Max(12f, (CommanderUiScale.Width - 680f) * 0.5f),
            CommanderUiScale.Height - 158f,
            Mathf.Min(680f, CommanderUiScale.Width - 24f),
            74f);
        selectionHelpRect = new Rect(selectionBarRect.x, selectionBarRect.y - 92f, selectionBarRect.width, 84f);
        supplyHeliUi.Tick();
        airCommandUi.Tick();
        depotUi.Tick();
    }

    internal bool ContainsScreenPoint(Vector2 screenPoint)
    {
        Vector2 guiPoint = CommanderUiScale.ScreenToGui(screenPoint);
        if (screenshotUiHidden)
        {
            return false;
        }
        return launcherRect.Contains(guiPoint)
            || (showFactionMoney && moneyRect.Contains(guiPoint))
            || (panelVisible && panelRect.Contains(guiPoint))
            || (reserveWindowVisible && reserveWindowRect.Contains(guiPoint))
            || (showSelectionBar && selectionService.SelectedUnits.Count > 0 && selectionBarRect.Contains(guiPoint))
            || (selectionHelpVisible && selectionHelpRect.Contains(guiPoint))
            || (showPinnedUnits && HasPinEntries && (pinnedLauncherRect.Contains(guiPoint) || (pinnedWindowVisible && pinnedWindowRect.Contains(guiPoint))))
            || (showUnitSystems && TryGetUnitSystemsTarget(out _, out _) && radarWindowRect.Contains(guiPoint))
            || (showDepotUi && depotUi.ContainsScreenPoint(screenPoint))
            || (showSupplyUi && supplyHeliUi.ContainsScreenPoint(screenPoint))
            || (showAirCommandUi && airCommandUi.ContainsScreenPoint(screenPoint));
    }

    internal void DrawInactiveLauncher(Action activateCommander)
    {
        CommanderUiTheme.Ensure();
        float centerY = CommanderUiScale.Height * 0.5f;
        launcherRect = new Rect(10f, centerY - 42f, 52f, 84f);
        if (GUI.Button(launcherRect, "CMD", CommanderUiTheme.PrimaryButton))
        {
            activateCommander();
        }
    }

    internal void Draw()
    {
        if (screenshotUiHidden)
        {
            return;
        }
        CommanderUiTheme.Ensure();
        if (showWorldMarkers)
        {
            worldMarkerRenderer.Draw(supplyHeliUi.Visible && supplyHeliUi.ShowLz);
        }
        GUIStyle commandStyle = showCommandButton
            ? (panelVisible ? CommanderUiTheme.SelectedButton : CommanderUiTheme.PrimaryButton)
            : GetGhostCommandStyle();
        if (GUI.Button(launcherRect, "CMD", commandStyle))
        {
            panelVisible = !panelVisible;
        }

        if (showFactionMoney)
        {
            GUI.Box(moneyRect, $"FACTION FUNDS   {spawnService.GetFactionFundsLabel()}", CommanderUiTheme.Money);
        }

        if (panelVisible)
        {
            panelRect = GUI.Window(WindowId, panelRect, DrawPanelWindow, "COMMANDER", CommanderUiTheme.Window);
        }
        if (reserveWindowVisible)
        {
            reserveWindowRect = GUI.Window(ReserveWindowId, reserveWindowRect, DrawReserveWindow, "FACTION RESERVE", CommanderUiTheme.Window);
        }

        if (showPinnedUnits && HasPinEntries)
        {
            if (GUI.Button(pinnedLauncherRect, pinnedWindowVisible ? "PINS <" : "PINS >", CommanderUiTheme.Button))
            {
                pinnedWindowVisible = !pinnedWindowVisible;
            }
            if (pinnedWindowVisible)
            {
                pinnedWindowRect = GUI.Window(PinnedWindowId, pinnedWindowRect, DrawPinnedWindow, "UNIT LIST", CommanderUiTheme.Window);
            }
        }
        if (showUnitSystems && TryGetUnitSystemsTarget(out _, out _))
        {
            radarWindowRect = GUI.Window(RadarWindowId, radarWindowRect, DrawRadarWindow, "UNIT SYSTEMS", CommanderUiTheme.Window);
        }

        if (showDepotUi) depotUi.Draw();
        if (showSupplyUi) supplyHeliUi.Draw();
        if (showAirCommandUi) airCommandUi.Draw();
        if (showSelectionBar) DrawSelectionBar();
    }

    internal bool ShowTacticalMapUi => showTacticalMap && !screenshotUiHidden;
    internal void ToggleScreenshotUi()
    {
        screenshotUiHidden = !screenshotUiHidden;
        if (screenshotUiHidden && CommanderTacticalMapService.Instance?.IsOpen == true)
        {
            CommanderTacticalMapService.Instance.Close();
        }
    }
    private bool HasPinEntries => selectionService.PinnedUnits.Count > 0 || selectionService.MissionUnits.Count > 0;

    private void DrawPanelWindow(int windowId)
    {
        if (CommanderUiTheme.DrawHelpButton(panelRect.width, ref panelHelpVisible))
        {
            CommanderUiTheme.DrawHelpOverlay(
                new Rect(12f, 34f, panelRect.width - 24f, 92f),
                "LMB selects a unit. Hold Shift to add units. RMB issues a move order. Select depots to open Depot Control. The MAP tab in the upper-right opens the tactical map.");
        }
        if (GUI.Button(new Rect(panelRect.width - 34f, 3f, 26f, 22f), "X", CommanderUiTheme.Button))
        {
            panelVisible = false;
        }

        float y = panelHelpVisible ? 136f : 38f;
        if (GUI.Button(new Rect(12f, y, panelRect.width - 24f, 38f), "SELECT NEAREST DEPOT", CommanderUiTheme.Button))
        {
            spawnService.SelectNearestDepot();
        }
        y += 46f;
        if (GUI.Button(new Rect(12f, y, panelRect.width - 24f, 38f), "SUPPLY AIRCRAFT", CommanderUiTheme.PrimaryButton))
        {
            supplyHeliUi.Toggle();
        }
        y += 46f;
        bool oldAirEnabled = GUI.enabled;
        GUI.enabled = oldAirEnabled && CommanderSettings.EnableAirCommand;
        if (GUI.Button(new Rect(12f, y, panelRect.width - 24f, 38f), "AIR COMMAND", CommanderUiTheme.PrimaryButton))
        {
            airCommandUi.Toggle();
        }
        GUI.enabled = oldAirEnabled;
        y += 46f;
        if (GUI.Button(new Rect(12f, y, panelRect.width - 24f, 38f), "FACTION RESERVE", CommanderUiTheme.Button))
        {
            reserveWindowVisible = !reserveWindowVisible;
        }
        y += 48f;

        string helper = supplyHeliService.AwaitingTargetSelection
            ? "Select the cargo destination in the 3D world. Esc cancels."
            : airCommandService.AwaitingAreaSelection
                ? "Select the Air Command mission area on the tactical map or in the 3D world."
                : mobileEmplacementService.AwaitingDestination
                    ? "Select the trailer destination in the 3D world. Esc cancels."
            : spawnService.AwaitingRallyPointSelection
                ? "Select the rally point on the tactical map or in the 3D world."
                : "Ready";
        GUI.Label(new Rect(14f, y, panelRect.width - 28f, 36f), helper, CommanderUiTheme.MutedLabel);
        y += 42f;

        float settingsY = panelRect.height - (settingsVisible ? 336f : 102f);
        if (GUI.Button(new Rect(12f, settingsY, panelRect.width - 24f, 34f), "SETTINGS", CommanderUiTheme.Button))
        {
            settingsVisible = !settingsVisible;
        }
        if (settingsVisible)
        {
            float tabWidth = (panelRect.width - 46f) * 0.5f;
            if (GUI.Button(new Rect(20f, settingsY + 40f, tabWidth, 30f), "GAMEPLAY",
                uiSettingsVisible ? CommanderUiTheme.Button : CommanderUiTheme.SelectedButton))
            {
                uiSettingsVisible = false;
            }
            if (GUI.Button(new Rect(26f + tabWidth, settingsY + 40f, tabWidth, 30f), "UI / HIDE",
                uiSettingsVisible ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
            {
                uiSettingsVisible = true;
            }

            if (!uiSettingsVisible)
            {
                CommanderSettings.LimitVehiclesToOwnSide = GUI.Toggle(
                    new Rect(20f, settingsY + 78f, panelRect.width - 40f, 26f),
                    CommanderSettings.LimitVehiclesToOwnSide,
                    "Limit vehicles to own side (aircraft ignored)", CommanderUiTheme.Toggle);
                CommanderSettings.EnableMobileEmplacements = GUI.Toggle(
                    new Rect(20f, settingsY + 108f, panelRect.width - 40f, 26f),
                    CommanderSettings.EnableMobileEmplacements,
                    "Enable Mobile Emplacements (experimental)", CommanderUiTheme.Toggle);
                CommanderSettings.EnableAirCommand = GUI.Toggle(
                    new Rect(20f, settingsY + 138f, panelRect.width - 40f, 26f),
                    CommanderSettings.EnableAirCommand,
                    "Enable Air Command (experimental)", CommanderUiTheme.Toggle);
            }
            else
            {
                float left = 20f;
                float right = panelRect.width * 0.5f + 4f;
                float width = panelRect.width * 0.5f - 26f;
                showCommandButton = GUI.Toggle(new Rect(left, settingsY + 78f, width, 24f), showCommandButton, "Command button", CommanderUiTheme.Toggle);
                showFactionMoney = GUI.Toggle(new Rect(right, settingsY + 78f, width, 24f), showFactionMoney, "Faction funds", CommanderUiTheme.Toggle);
                showTacticalMap = GUI.Toggle(new Rect(left, settingsY + 106f, width, 24f), showTacticalMap, "Tactical map", CommanderUiTheme.Toggle);
                showSelectionBar = GUI.Toggle(new Rect(right, settingsY + 106f, width, 24f), showSelectionBar, "Selection bar", CommanderUiTheme.Toggle);
                showPinnedUnits = GUI.Toggle(new Rect(left, settingsY + 134f, width, 24f), showPinnedUnits, "Unit / mission list", CommanderUiTheme.Toggle);
                showUnitSystems = GUI.Toggle(new Rect(right, settingsY + 134f, width, 24f), showUnitSystems, "Unit systems", CommanderUiTheme.Toggle);
                showDepotUi = GUI.Toggle(new Rect(left, settingsY + 162f, width, 24f), showDepotUi, "Depot UI", CommanderUiTheme.Toggle);
                showSupplyUi = GUI.Toggle(new Rect(right, settingsY + 162f, width, 24f), showSupplyUi, "Supply UI", CommanderUiTheme.Toggle);
                showAirCommandUi = GUI.Toggle(new Rect(left, settingsY + 190f, width, 24f), showAirCommandUi, "Air Command UI", CommanderUiTheme.Toggle);
                showWorldMarkers = GUI.Toggle(new Rect(right, settingsY + 190f, width, 24f), showWorldMarkers, "World markers", CommanderUiTheme.Toggle);
                GUI.Label(new Rect(20f, settingsY + 218f, 86f, 26f), "UI SCALE", CommanderUiTheme.MutedLabel);
                bool canScaleDown = CommanderSettings.UiScale > 0.5f;
                bool canScaleUp = CommanderSettings.UiScale < 1.5f;
                bool oldEnabled = GUI.enabled;
                GUI.enabled = oldEnabled && canScaleDown;
                if (GUI.Button(new Rect(110f, settingsY + 214f, 34f, 28f), "-", CommanderUiTheme.Button))
                {
                    CommanderUiScale.Step(-1);
                    ResetUiLayout();
                }
                GUI.enabled = oldEnabled;
                GUI.Label(new Rect(150f, settingsY + 218f, 70f, 24f), $"{CommanderSettings.UiScale:0.##}x", CommanderUiTheme.Header);
                GUI.enabled = oldEnabled && canScaleUp;
                if (GUI.Button(new Rect(224f, settingsY + 214f, 34f, 28f), "+", CommanderUiTheme.Button))
                {
                    CommanderUiScale.Step(1);
                    ResetUiLayout();
                }
                GUI.enabled = oldEnabled;
                GUI.Label(new Rect(20f, settingsY + 248f, panelRect.width - 40f, 20f), "H toggles the complete UI for screenshots.", CommanderUiTheme.MutedLabel);
                if (GUI.Button(new Rect(20f, settingsY + 272f, panelRect.width - 40f, 30f), "RESET UI LAYOUT", CommanderUiTheme.Button))
                {
                    ResetUiLayout();
                }
            }
        }

        if (GUI.Button(new Rect(12f, panelRect.height - 54f, panelRect.width - 24f, 38f), "EXIT COMMANDER MODE", CommanderUiTheme.DangerButton))
        {
            exitCommander();
        }

        GUI.DragWindow(new Rect(0f, 0f, panelRect.width - 72f, 28f));
    }

    private void ResetUiLayout()
    {
        positionsInitialized = false;
        supplyHeliUi.ResetPosition();
        airCommandUi.ResetPosition();
        depotUi.ResetPosition();
        CommanderTacticalMapService.Instance?.ResetLayoutPosition();
    }

    private GUIStyle GetGhostCommandStyle()
    {
        if (ghostCommandStyle != null)
        {
            return ghostCommandStyle;
        }

        ghostCommandStyle = new GUIStyle(CommanderUiTheme.Button);
        ghostCommandStyle.normal.background = null;
        ghostCommandStyle.hover.background = null;
        ghostCommandStyle.active.background = null;
        Color dim = ghostCommandStyle.normal.textColor;
        dim.a = 0.5f;
        ghostCommandStyle.normal.textColor = dim;
        ghostCommandStyle.hover.textColor = dim;
        ghostCommandStyle.active.textColor = dim;
        return ghostCommandStyle;
    }

    private void DrawSelectionBar()
    {
        int count = selectionService.SelectedUnits.Count;
        if (count == 0)
        {
            return;
        }

        GUI.Box(selectionBarRect, string.Empty, CommanderUiTheme.Panel);
        GUI.Label(new Rect(selectionBarRect.x + 12f, selectionBarRect.y + 3f, 190f, 24f), "UNIT SELECTION", CommanderUiTheme.MutedLabel);
        if (GUI.Button(new Rect(selectionBarRect.xMax - 34f, selectionBarRect.y + 3f, 26f, 22f), "?", CommanderUiTheme.HelpButton))
        {
            selectionHelpVisible = !selectionHelpVisible;
        }
        Unit? focused = selectionService.FocusedSelection;
        string label = count == 1 && focused != null
            ? CommanderGameAccess.GetUnitLabel(focused)
            : $"{count} UNITS SELECTED";
        GUI.Label(new Rect(selectionBarRect.x + 14f, selectionBarRect.y + 37f, selectionBarRect.width - 322f, 24f), label, CommanderUiTheme.Header);

        float buttonX = selectionBarRect.xMax - 252f;
        if (selectionHelpVisible)
        {
            CommanderUiTheme.DrawHelpOverlay(selectionHelpRect,
                "STOP holds commandable friendly ground/ship units and arrests rotation. AI returns them to Basegame tasks. PIN stores the selection; hold Alt to expose DEL for friendly units.");
        }
        bool oldEnabled = GUI.enabled;
        GUI.enabled = oldEnabled && moveService.HasCommandableSelection;
        if (GUI.Button(new Rect(buttonX, selectionBarRect.y + 32f, 72f, 34f), "STOP", CommanderUiTheme.DangerButton))
        {
            moveService.StopSelectedUnits();
        }
        if (GUI.Button(new Rect(buttonX + 78f, selectionBarRect.y + 32f, 72f, 34f), "AI", CommanderUiTheme.PrimaryButton))
        {
            moveService.ResumeAiForSelectedUnits();
        }
        GUI.enabled = oldEnabled;
        bool deleteMode = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        string pinLabel = deleteMode ? "DEL" : (selectionService.IsCurrentSelectionPinned ? "UNPIN" : "PIN");
        GUI.enabled = oldEnabled && (!deleteMode || selectionService.CanDeleteSelection);
        if (GUI.Button(new Rect(buttonX + 156f, selectionBarRect.y + 32f, 84f, 34f), pinLabel,
            deleteMode ? CommanderUiTheme.DangerButton : CommanderUiTheme.Button))
        {
            if (deleteMode)
            {
                selectionService.DeleteSelectedUnits();
            }
            else
            {
                selectionService.TogglePinSelected();
            }
        }
        GUI.enabled = oldEnabled;
    }

    private void DrawPinnedWindow(int windowId)
    {
        CommanderUiTheme.DrawHelpButton(pinnedWindowRect.width, ref pinnedHelpVisible);
        float y = pinnedHelpVisible ? 106f : 36f;
        if (pinnedHelpVisible)
        {
            CommanderUiTheme.DrawHelpOverlay(new Rect(12f, 34f, pinnedWindowRect.width - 24f, 62f),
                "PINS contains manual pins. MISSIONS contains aircraft spawned by Supply or Air Command. Click an entry to select it; X removes only the list entry.");
        }

        float tabWidth = (pinnedWindowRect.width - 30f) * 0.5f;
        if (GUI.Button(new Rect(12f, y, tabWidth, 30f), "PINS",
            pinnedShowsMissions ? CommanderUiTheme.Button : CommanderUiTheme.SelectedButton))
        {
            pinnedShowsMissions = false;
            pinnedScroll = Vector2.zero;
        }
        if (GUI.Button(new Rect(18f + tabWidth, y, tabWidth, 30f), "MISSIONS",
            pinnedShowsMissions ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            pinnedShowsMissions = true;
            pinnedScroll = Vector2.zero;
        }
        y += 38f;

        if (pinnedShowsMissions)
        {
            float filterWidth = (pinnedWindowRect.width - 30f) * 0.5f;
            showSupplyMissions = GUI.Toggle(new Rect(12f, y, filterWidth, 26f),
                showSupplyMissions, "SUPPLY", CommanderUiTheme.Toggle);
            showAirCommandMissions = GUI.Toggle(new Rect(18f + filterWidth, y, filterWidth, 26f),
                showAirCommandMissions, "AIR COMMAND", CommanderUiTheme.Toggle);
            y += 32f;
        }

        List<Unit> visibleUnits = new();
        IReadOnlyList<Unit> source = pinnedShowsMissions
            ? selectionService.MissionUnits
            : selectionService.PinnedUnits;
        for (int i = 0; i < source.Count; i++)
        {
            Unit unit = source[i];
            if (!pinnedShowsMissions)
            {
                visibleUnits.Add(unit);
                continue;
            }
            CommanderSelectionService.MissionPinInfo info = selectionService.GetMissionInfo(unit);
            if ((showSupplyMissions && info.Source == "SUPPLY")
                || (showAirCommandMissions && info.Source == "AIR COMMAND"))
            {
                visibleUnits.Add(unit);
            }
        }

        Rect view = new(10f, y, pinnedWindowRect.width - 20f, pinnedWindowRect.height - y - 12f);
        float rowHeight = pinnedShowsMissions ? 58f : 40f;
        Rect inner = new(0f, 0f, view.width - 18f, Mathf.Max(view.height, visibleUnits.Count * rowHeight + 4f));
        pinnedScroll = GUI.BeginScrollView(view, pinnedScroll, inner);
        for (int i = 0; i < visibleUnits.Count; i++)
        {
            Unit unit = visibleUnits[i];
            float rowY = 2f + i * rowHeight;
            if (GUI.Button(new Rect(4f, rowY, inner.width - 44f, rowHeight - 6f), string.Empty,
                pinnedShowsMissions ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
            {
                selectionService.SelectPinnedUnit(unit);
            }
            GUI.Label(new Rect(12f, rowY + 5f, inner.width - 64f, 22f),
                CommanderGameAccess.GetUnitLabel(unit), CommanderUiTheme.Header);
            if (pinnedShowsMissions)
            {
                CommanderSelectionService.MissionPinInfo info = selectionService.GetMissionInfo(unit);
                GUI.Label(new Rect(12f, rowY + 28f, inner.width - 64f, 20f),
                    $"{info.Source}  |  {info.Mission}", CommanderUiTheme.MutedLabel);
            }
            if (GUI.Button(new Rect(inner.width - 36f, rowY, 32f, rowHeight - 6f), "X", CommanderUiTheme.DangerButton))
            {
                selectionService.RemovePinnedUnit(unit);
                break;
            }
        }
        GUI.EndScrollView();
        GUI.DragWindow(new Rect(0f, 0f, pinnedWindowRect.width - 44f, 28f));
    }

    private void DrawRadarWindow(int windowId)
    {
        if (!TryGetUnitSystemsTarget(out Unit focusedUnit, out CommanderRadarService.RadarState? state))
        {
            return;
        }

        CommanderUiTheme.DrawHelpButton(radarWindowRect.width, ref radarHelpVisible);
        if (radarHelpVisible)
        {
            CommanderUiTheme.DrawHelpOverlay(new Rect(10f, 32f, radarWindowRect.width - 20f, 54f),
                state?.IsCommandTruck == true
                    ? "Counts cover the fire-control network around this command truck. Local radar can be switched below when present."
                    : mobileEmplacementService.IsMoveableTrailer(focusedUnit)
                        ? "Relocate this static trailer through a nearby fire-control truck and an idle HLT/MSV Tractor. Loading and deployment each take 10 seconds."
                    : focusedUnit is Ship
                        ? "Request a Basegame UH-90K naval-supply run for this ship. Purchased airframes are refunded after a successful return."
                        : "Switch local radar emissions or force a ground vehicle to ignore the road network and drive directly to every new destination.");
        }
        float y = radarHelpVisible ? 92f : 38f;
        bool friendly = CommanderGameAccess.IsFriendlyUnit(focusedUnit, CommanderGameAccess.GetLocalHq());
        if (!friendly)
        {
            GUI.Label(new Rect(12f, y, radarWindowRect.width - 24f, 24f), "ENEMY UNIT  |  CONTROLS UNAVAILABLE", CommanderUiTheme.MutedLabel);
            y += 30f;
        }
        if (state?.IsCommandTruck == true)
        {
            GUI.Label(new Rect(12f, y, radarWindowRect.width - 24f, 22f),
                $"NEARBY  {state.NearbyRadarCount} RADAR   /   {state.NearbyLauncherCount} LAUNCHERS", CommanderUiTheme.Header);
            y += 30f;
        }
        bool oldEnabled = GUI.enabled;
        if (state != null)
        {
            GUI.enabled = oldEnabled && friendly && state.HasRadar;
            if (GUI.Button(new Rect(12f, y, 126f, 34f),
                state.HasRadar ? (state.IsRadarOnline ? "RDR ONLINE" : "RDR OFFLINE") : "NO LOCAL RDR",
                state.IsRadarOnline ? CommanderUiTheme.SelectedButton : CommanderUiTheme.DangerButton))
            {
                radarService.ToggleRadar();
            }
            GUI.enabled = oldEnabled;
            GUI.Label(new Rect(148f, y, radarWindowRect.width - 160f, 34f), radarService.StatusText, CommanderUiTheme.MutedLabel);
            y += 42f;
        }

        if (directPathService.CanConfigure(focusedUnit))
        {
            bool directRoute = directPathService.IsEnabled(focusedUnit);
            GUI.enabled = oldEnabled && friendly && !CommanderMobileEmplacementService.IsReservedHauler(focusedUnit);
            if (GUI.Button(new Rect(12f, y, radarWindowRect.width - 24f, 36f),
                directRoute ? "DIRECT ROUTE  ON" : "DIRECT ROUTE  OFF",
                directRoute ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
            {
                directPathService.ToggleFocusedUnit();
            }
            GUI.enabled = oldEnabled;
            y += 42f;
        }

        if (focusedUnit is Ship ship)
        {
            GUI.enabled = oldEnabled && friendly;
            if (GUI.Button(new Rect(12f, y, radarWindowRect.width - 24f, 36f),
                supplyHeliService.GetNavalSupplyButtonLabel(ship), CommanderUiTheme.PrimaryButton))
            {
                supplyHeliService.RequestNavalSupply(ship);
            }
            GUI.enabled = oldEnabled;
            y += 40f;
            GUI.Label(new Rect(12f, y, radarWindowRect.width - 24f, 38f), supplyHeliService.StatusText, CommanderUiTheme.MutedLabel);
        }
        else if (mobileEmplacementService.IsMoveableTrailer(focusedUnit))
        {
            GUI.enabled = oldEnabled && friendly && !mobileEmplacementService.IsRelocating(focusedUnit);
            if (GUI.Button(new Rect(12f, y, radarWindowRect.width - 24f, 36f),
                mobileEmplacementService.IsRelocating(focusedUnit) ? "RELOCATION ACTIVE" : "RELOCATE TRAILER",
                CommanderUiTheme.PrimaryButton))
            {
                mobileEmplacementService.BeginRelocation();
            }
            GUI.enabled = oldEnabled;
            y += 42f;
            GUI.Label(new Rect(12f, y, radarWindowRect.width - 24f, 42f), mobileEmplacementService.StatusText, CommanderUiTheme.MutedLabel);
        }
        GUI.DragWindow(new Rect(0f, 0f, radarWindowRect.width - 44f, 28f));
    }

    private bool TryGetUnitSystemsTarget(out Unit unit, out CommanderRadarService.RadarState? state)
    {
        unit = selectionService.FocusedSelection!;
        state = null;
        if (unit == null || unit.disabled)
        {
            return false;
        }

        if (radarService.TryGetFocusedState(out CommanderRadarService.RadarState radarState)
            && ReferenceEquals(radarState.Unit, unit))
        {
            state = radarState;
        }

        return state != null
            || unit is Ship
            || mobileEmplacementService.IsMoveableTrailer(unit)
            || directPathService.CanConfigure(unit);
    }

    private void DrawReserveWindow(int windowId)
    {
        if (CommanderUiTheme.DrawHelpButton(reserveWindowRect.width, ref reserveHelpVisible))
        {
            CommanderUiTheme.DrawHelpOverlay(
                new Rect(12f, 34f, reserveWindowRect.width - 24f, 78f),
                "This reads ProductionUnit directly from every friendly Basegame Factory. Category HOLD blocks automatic deployment for that factory output category. Unit HOLD blocks only the selected vehicle type.");
        }
        if (GUI.Button(new Rect(reserveWindowRect.width - 34f, 3f, 26f, 22f), "X", CommanderUiTheme.DangerButton))
        {
            reserveWindowVisible = false;
            return;
        }

        float y = reserveHelpVisible ? 122f : 38f;
        GUI.Label(new Rect(12f, y, reserveWindowRect.width - 24f, 30f),
            $"FUNDS  {spawnService.GetFactionFundsLabel()}    |    VEHICLES IN RESERVE  {spawnService.GetProductionReserveTotal()}", CommanderUiTheme.Header);
        y += 38f;

        float modeWidth = (reserveWindowRect.width - 30f) * 0.5f;
        if (GUI.Button(new Rect(12f, y, modeWidth, 34f), "CATEGORIES",
            reserveShowsUnits ? CommanderUiTheme.Button : CommanderUiTheme.SelectedButton))
        {
            reserveShowsUnits = false;
            reserveScroll = Vector2.zero;
        }
        if (GUI.Button(new Rect(18f + modeWidth, y, modeWidth, 34f), "INDIVIDUAL UNITS",
            reserveShowsUnits ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            reserveShowsUnits = true;
            reserveScroll = Vector2.zero;
        }
        y += 44f;

        GUI.Label(new Rect(12f, y, reserveWindowRect.width - 24f, 22f),
            reserveShowsUnits ? "FACTORY OUTPUT BY UNIT" : "FACTORY OUTPUT BY CATEGORY", CommanderUiTheme.MutedLabel);
        y += 24f;
        Rect view = new(12f, y, reserveWindowRect.width - 24f, reserveWindowRect.height - y - 14f);
        if (reserveShowsUnits)
        {
            IReadOnlyList<VehicleDefinition> definitions = spawnService.GetProductionVehicleDefinitions();
            Rect inner = new(0f, 0f, view.width - 20f, Mathf.Max(view.height, definitions.Count * 40f + 6f));
            reserveScroll = GUI.BeginScrollView(view, reserveScroll, inner);
            for (int i = 0; i < definitions.Count; i++)
            {
                VehicleDefinition definition = definitions[i];
                string category = CommanderGameAccess.GetVehicleCategoryLabel(definition);
                bool categoryHeld = spawnService.IsCategoryHeld(category);
                bool individuallyHeld = spawnService.IsVehicleHeld(definition);
                Rect row = new(4f, 3f + i * 40f, inner.width - 8f, 36f);
                GUI.Box(row, string.Empty, CommanderUiTheme.Panel);

                if (categoryHeld)
                {
                    GUI.Label(new Rect(row.x + 8f, row.y + 8f, 104f, 20f), "CATEGORY HOLD", CommanderUiTheme.MutedLabel);
                }
                else
                {
                    bool updatedHeld = GUI.Toggle(new Rect(row.x + 8f, row.y + 7f, 64f, 22f), individuallyHeld, "HOLD", CommanderUiTheme.Toggle);
                    if (updatedHeld != individuallyHeld)
                    {
                        spawnService.ToggleHeldVehicle(definition);
                    }
                }

                GUI.Label(new Rect(row.x + 120f, row.y + 6f, row.width - 255f, 24f),
                    CommanderGameAccess.GetVehicleLabel(definition), CommanderUiTheme.Label);
                GUI.Label(new Rect(row.xMax - 126f, row.y + 6f, 118f, 24f),
                    $"RESERVE {spawnService.GetReserveCount(definition)}", CommanderUiTheme.MutedLabel);
            }
            GUI.EndScrollView();

            if (definitions.Count == 0)
            {
                GUI.Label(view, "No friendly vehicle factories are currently active.", CommanderUiTheme.Label);
            }
        }
        else
        {
            IReadOnlyList<string> categories = spawnService.GetProductionCategories();
            Rect inner = new(0f, 0f, view.width - 20f, Mathf.Max(view.height, categories.Count * 46f + 6f));
            reserveScroll = GUI.BeginScrollView(view, reserveScroll, inner);
            for (int i = 0; i < categories.Count; i++)
            {
                string category = categories[i];
                bool held = spawnService.IsCategoryHeld(category);
                Rect row = new(4f, 3f + i * 46f, inner.width - 8f, 42f);
                GUI.Box(row, string.Empty, CommanderUiTheme.Panel);
                bool updatedHeld = GUI.Toggle(new Rect(row.x + 10f, row.y + 10f, 64f, 22f), held, "HOLD", CommanderUiTheme.Toggle);
                if (updatedHeld != held)
                {
                    spawnService.ToggleHeldCategory(category);
                }
                GUI.Label(new Rect(row.x + 94f, row.y + 8f, row.width - 230f, 26f), category, CommanderUiTheme.Header);
                GUI.Label(new Rect(row.xMax - 126f, row.y + 8f, 118f, 26f),
                    $"RESERVE {spawnService.GetProductionCategoryReserveCount(category)}", CommanderUiTheme.MutedLabel);
            }
            GUI.EndScrollView();

            if (categories.Count == 0)
            {
                GUI.Label(view, "No friendly vehicle factories are currently active.", CommanderUiTheme.Label);
            }
        }
        GUI.DragWindow(new Rect(0f, 0f, reserveWindowRect.width - 72f, 28f));
    }
}
