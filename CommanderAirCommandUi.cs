using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderAirCommandUi
{
    private const int WindowId = 0x434F4143;

    private readonly CommanderAirCommandService service;
    private Rect windowRect = new(0f, 0f, 820f, 760f);
    private Vector2 aircraftScroll;
    private Vector2 hardpointScroll;
    private Vector2 loadoutDropdownScroll;
    private Vector2 airbaseScroll;
    private int openHardpointGroup = -1;
    private bool positionInitialized;
    private bool helpVisible;

    internal CommanderAirCommandUi(CommanderAirCommandService service)
    {
        this.service = service;
    }

    internal bool Visible { get; private set; }

    internal void Toggle()
    {
        Visible = !Visible;
        service.SetUiVisible(Visible);
        if (Visible)
        {
            helpVisible = false;
            aircraftScroll = Vector2.zero;
            hardpointScroll = Vector2.zero;
            airbaseScroll = Vector2.zero;
            openHardpointGroup = -1;
        }
    }

    internal void Hide()
    {
        Visible = false;
        service.SetUiVisible(false);
    }

    internal void ResetPosition()
    {
        positionInitialized = false;
    }

    internal void Tick()
    {
        float width = Mathf.Min(820f, CommanderUiScale.Width - 32f);
        float height = Mathf.Min(760f, CommanderUiScale.Height - 32f);
        if (!positionInitialized)
        {
            windowRect = new Rect(
                Mathf.Max(74f, (CommanderUiScale.Width - width) * 0.5f),
                Mathf.Max(12f, (CommanderUiScale.Height - height) * 0.5f),
                width,
                height);
            positionInitialized = true;
        }
        else
        {
            windowRect.width = width;
            windowRect.height = height;
            windowRect = CommanderUiTheme.ClampWindow(windowRect);
        }
    }

    internal bool ContainsScreenPoint(Vector2 screenPoint)
    {
        Vector2 guiPoint = CommanderUiScale.ScreenToGui(screenPoint);
        return Visible && !service.AwaitingAreaSelection && windowRect.Contains(guiPoint);
    }

    internal void Draw()
    {
        if (!Visible || service.AwaitingAreaSelection)
        {
            return;
        }

        windowRect = GUI.Window(WindowId, windowRect, DrawWindow, "AIR COMMAND", CommanderUiTheme.Window);
    }

    private void DrawWindow(int windowId)
    {
        CommanderUiTheme.DrawHelpButton(windowRect.width, ref helpVisible);
        if (GUI.Button(new Rect(windowRect.width - 34f, 3f, 26f, 22f), "X", CommanderUiTheme.DangerButton))
        {
            Hide();
            return;
        }

        float y = 36f;
        if (helpVisible)
        {
            CommanderUiTheme.DrawHelpOverlay(
                new Rect(12f, y, windowRect.width - 24f, 86f),
                "Aircraft use Basegame hardpoints, weapons, flight AI, evasion and landing. Mirrored stations are always edited together; combined bays automatically clear precluded forward/aft bays. The selected role limits offensive target assessment inside the mission area.");
            y += 94f;
        }

        bool dropdownOpen = openHardpointGroup >= 0;
        float modeWidth = (windowRect.width - 34f) * 0.2f;
        bool oldEnabled = GUI.enabled;
        GUI.enabled = oldEnabled && !dropdownOpen;
        DrawModeButton(CommanderAirCommandService.AirCommandMode.AwacsJammer, "AWACS / JAM", 12f, y, modeWidth);
        DrawModeButton(CommanderAirCommandService.AirCommandMode.Cas, "CAS", 14f + modeWidth, y, modeWidth);
        DrawModeButton(CommanderAirCommandService.AirCommandMode.AirGuard, "AIR SUPERIORITY", 16f + modeWidth * 2f, y, modeWidth);
        DrawModeButton(CommanderAirCommandService.AirCommandMode.Arad, "ARAD", 18f + modeWidth * 3f, y, modeWidth);
        DrawModeButton(CommanderAirCommandService.AirCommandMode.StrategicStrike, "STRIKE", 20f + modeWidth * 4f, y, modeWidth);
        y += 44f;

        GUI.enabled = oldEnabled;
        GUI.Label(new Rect(12f, y, windowRect.width - 24f, 24f), "MISSION LOADOUT", CommanderUiTheme.MutedLabel);
        y += 24f;
        float loadoutHeight = 136f;
        Rect loadoutPanel = new(12f, y, windowRect.width - 24f, loadoutHeight);
        GUI.Box(loadoutPanel, string.Empty, CommanderUiTheme.Panel);
        DrawLoadoutEditor(loadoutPanel);
        y += loadoutHeight + 8f;

        GUI.enabled = oldEnabled && !dropdownOpen;
        GUI.Label(new Rect(12f, y, windowRect.width - 24f, 24f), "AIRCRAFT", CommanderUiTheme.MutedLabel);
        y += 24f;
        float aircraftHeight = 150f;
        Rect aircraftView = new(12f, y, windowRect.width - 24f, aircraftHeight);
        float aircraftInnerHeight = Mathf.Max(aircraftView.height, service.Options.Count * 58f + 4f);
        aircraftScroll = GUI.BeginScrollView(aircraftView, aircraftScroll, new Rect(0f, 0f, aircraftView.width - 18f, aircraftInnerHeight));
        for (int i = 0; i < service.Options.Count; i++)
        {
            CommanderAirCommandService.AirMissionOption option = service.Options[i];
            if (GUI.Button(new Rect(2f, 2f + i * 58f, aircraftView.width - 24f, 52f),
                service.GetOptionLabel(option),
                i == service.SelectedOptionIndex ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
            {
                service.SelectOption(i);
                airbaseScroll = Vector2.zero;
                hardpointScroll = Vector2.zero;
                openHardpointGroup = -1;
            }
        }
        GUI.EndScrollView();
        y += aircraftHeight + 8f;

        GUI.enabled = oldEnabled && !dropdownOpen;
        GUI.Label(new Rect(12f, y, windowRect.width - 24f, 24f), "DEPARTURE AIRBASE", CommanderUiTheme.MutedLabel);
        y += 24f;
        float footerHeight = 112f;
        float airbaseHeight = Mathf.Max(72f, windowRect.height - y - footerHeight);
        Rect airbaseView = new(12f, y, windowRect.width - 24f, airbaseHeight);
        float airbaseInnerHeight = Mathf.Max(airbaseView.height, service.Airbases.Count * 40f + 4f);
        airbaseScroll = GUI.BeginScrollView(airbaseView, airbaseScroll, new Rect(0f, 0f, airbaseView.width - 18f, airbaseInnerHeight));
        for (int i = 0; i < service.Airbases.Count; i++)
        {
            CommanderAirCommandService.AirbaseOption airbase = service.Airbases[i];
            if (GUI.Button(new Rect(2f, 2f + i * 40f, airbaseView.width - 24f, 34f),
                service.GetAirbaseLabel(airbase),
                i == service.SelectedAirbaseIndex ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
            {
                service.SelectAirbase(i);
            }
        }
        GUI.EndScrollView();
        y += airbaseHeight + 6f;

        GUI.enabled = oldEnabled && !dropdownOpen && service.CanLaunchSelected;
        if (GUI.Button(new Rect(12f, y, windowRect.width - 24f, 38f),
            service.AwaitingAreaSelection ? "SELECT MISSION AREA..." : "REQUEST MISSION",
            CommanderUiTheme.PrimaryButton))
        {
            service.BeginAreaSelection();
        }
        GUI.enabled = oldEnabled;
        y += 42f;

        string status = service.AwaitingAreaSelection
            ? "Click the tactical map or 3D terrain. Esc cancels."
            : service.StatusText;
        GUI.Label(new Rect(12f, y, windowRect.width - 24f, 42f),
            $"ACTIVE {service.ActiveMissionCount}  |  {status}", CommanderUiTheme.MutedLabel);
        DrawLoadoutDropdown(loadoutPanel);
        GUI.DragWindow(new Rect(0f, 0f, windowRect.width - 44f, 28f));
    }

    private void DrawLoadoutEditor(Rect panel)
    {
        float half = (panel.width - 36f) * 0.5f;
        GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, half, 22f), "PRIMARY WEAPON  [REQUIRED]", CommanderUiTheme.Header);
        GUI.Label(new Rect(panel.x + 24f + half, panel.y + 8f, half, 22f), "SECONDARY WEAPON  [OPTIONAL]", CommanderUiTheme.Header);
        bool popupOpen = openHardpointGroup >= 0;
        bool oldEnabled = GUI.enabled;
        GUI.enabled = oldEnabled && !popupOpen;
        string primary = service.SelectedPrimaryWeapon == null
            ? "SELECT PRIMARY   v"
            : service.GetMissionWeaponLabel(service.SelectedPrimaryWeapon) + "   v";
        if (GUI.Button(new Rect(panel.x + 12f, panel.y + 36f, half, 58f), primary,
            service.SelectedPrimaryWeapon == null ? CommanderUiTheme.DangerButton : CommanderUiTheme.SelectedButton))
        {
            openHardpointGroup = 0;
            loadoutDropdownScroll = Vector2.zero;
        }
        string secondary = service.SelectedSecondaryWeapon == null
            ? "NONE   v"
            : service.GetMissionWeaponLabel(service.SelectedSecondaryWeapon) + "   v";
        if (GUI.Button(new Rect(panel.x + 24f + half, panel.y + 36f, half, 58f), secondary,
            service.SelectedSecondaryWeapon == null ? CommanderUiTheme.Button : CommanderUiTheme.SelectedButton))
        {
            openHardpointGroup = 1;
            loadoutDropdownScroll = Vector2.zero;
        }
        GUI.enabled = oldEnabled;
        GUI.Label(new Rect(panel.x + 12f, panel.y + 102f, panel.width - 24f, 24f),
            "Aircraft are sorted by mounted primary stores, then secondary stores. Mirrored hardpoints and bay conflicts are applied automatically.", CommanderUiTheme.MutedLabel);
    }

    private void DrawLoadoutDropdown(Rect loadoutPanel)
    {
        if (openHardpointGroup < 0 || openHardpointGroup > 1)
        {
            return;
        }

        bool secondary = openHardpointGroup == 1;
        Rect popup = new(loadoutPanel.x + 50f, loadoutPanel.y + 12f, loadoutPanel.width - 100f, 360f);
        GUI.Box(popup, string.Empty, CommanderUiTheme.Window);
        GUI.Label(new Rect(popup.x + 12f, popup.y + 8f, popup.width - 52f, 24f),
            secondary ? "SELECT SECONDARY WEAPON" : "SELECT PRIMARY WEAPON", CommanderUiTheme.Header);
        if (GUI.Button(new Rect(popup.xMax - 34f, popup.y + 7f, 24f, 24f), "X", CommanderUiTheme.DangerButton))
        {
            openHardpointGroup = -1;
            return;
        }

        Rect view = new(popup.x + 10f, popup.y + 38f, popup.width - 20f, popup.height - 48f);
        int extraRows = secondary ? 1 : 0;
        Rect inner = new(0f, 0f, view.width - 20f,
            Mathf.Max(view.height, (service.WeaponOptions.Count + extraRows + 2) * 38f + 4f));
        loadoutDropdownScroll = GUI.BeginScrollView(view, loadoutDropdownScroll, inner);
        float y = 2f;
        if (secondary && GUI.Button(new Rect(4f, y, inner.width - 8f, 34f), "NONE", CommanderUiTheme.DangerButton))
        {
            service.SelectSecondaryWeapon(-1);
            openHardpointGroup = -1;
        }
        if (secondary) y += 38f;
        bool separatorDrawn = false;
        for (int i = 0; i < service.WeaponOptions.Count; i++)
        {
            WeaponMount mount = service.WeaponOptions[i];
            if (!separatorDrawn && !service.IsWeaponSuitable(mount))
            {
                GUI.Label(new Rect(4f, y, inner.width - 8f, 30f), "-- OTHER --", CommanderUiTheme.MutedLabel);
                y += 32f;
                separatorDrawn = true;
            }
            if (GUI.Button(new Rect(4f, y, inner.width - 8f, 34f),
                service.GetMissionWeaponLabel(mount), CommanderUiTheme.Button))
            {
                if (secondary) service.SelectSecondaryWeapon(i);
                else service.SelectPrimaryWeapon(i);
                openHardpointGroup = -1;
            }
            y += 38f;
        }
        GUI.EndScrollView();
    }

    private void DrawModeButton(
        CommanderAirCommandService.AirCommandMode mode,
        string label,
        float x,
        float y,
        float width)
    {
        if (GUI.Button(new Rect(x, y, width - 4f, 36f), label,
            service.SelectedMode == mode ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            service.SelectMode(mode);
            aircraftScroll = Vector2.zero;
            hardpointScroll = Vector2.zero;
            airbaseScroll = Vector2.zero;
            openHardpointGroup = -1;
        }
    }
}
