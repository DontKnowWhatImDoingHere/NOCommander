using System;
using System.Collections.Generic;
using NuclearOption.Networking;
using NuclearOption.SavedMission;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed partial class CommanderAirCommandService
{
    private const float PendingSpawnTimeoutSeconds = 45f;
    private const float StatusDurationSeconds = 6f;
    private const float MissionPruneIntervalSeconds = 2f;

    private readonly CommanderTacticalMapService tacticalMapService;
    private readonly CommanderMapClickTracker mapClickTracker = new();
    private readonly List<AirMissionOption> options = new();
    private readonly List<WeaponMount> weaponOptions = new();
    private readonly List<AirbaseOption> airbases = new();
    private readonly Dictionary<Aircraft, AirMission> missions = new();
    private readonly List<Aircraft> staleAircraft = new();

    private PendingAreaSelection? pendingAreaSelection;
    private PendingAircraftSpawn? pendingAircraftSpawn;
    private AirCommandMode selectedMode = AirCommandMode.AirGuard;
    private int selectedOptionIndex;
    private int selectedAirbaseIndex;
    private int selectedPrimaryWeaponIndex = -1;
    private int selectedSecondaryWeaponIndex = -1;
    private float nextAirbaseRefreshAt;
    private float nextMissionPruneAt;
    private float statusUntil;
    private string statusText = string.Empty;
    private bool uiVisible;

    internal static CommanderAirCommandService? Instance { get; private set; }

    internal CommanderAirCommandService(CommanderTacticalMapService tacticalMapService)
    {
        this.tacticalMapService = tacticalMapService;
        Instance = this;
    }

    internal AirCommandMode SelectedMode => selectedMode;
    internal IReadOnlyList<AirMissionOption> Options => options;
    internal IReadOnlyList<AirbaseOption> Airbases => airbases;
    internal IReadOnlyList<WeaponMount> WeaponOptions => weaponOptions;
    internal int SelectedOptionIndex => selectedOptionIndex;
    internal int SelectedAirbaseIndex => selectedAirbaseIndex;
    internal int SelectedPrimaryWeaponIndex => selectedPrimaryWeaponIndex;
    internal int SelectedSecondaryWeaponIndex => selectedSecondaryWeaponIndex;
    internal bool AwaitingAreaSelection => pendingAreaSelection != null;
    internal int ActiveMissionCount => missions.Count;
    internal bool CanLaunchSelected => SelectedOption != null
        && SelectedPrimaryWeapon != null
        && GetPrimaryWeaponCount(SelectedOption) > 0
        && SelectedAirbase != null;
    internal string StatusText => Time.unscaledTime <= statusUntil ? statusText : string.Empty;
    internal WeaponMount? SelectedPrimaryWeapon => selectedPrimaryWeaponIndex >= 0 && selectedPrimaryWeaponIndex < weaponOptions.Count
        ? weaponOptions[selectedPrimaryWeaponIndex]
        : null;
    internal WeaponMount? SelectedSecondaryWeapon => selectedSecondaryWeaponIndex >= 0 && selectedSecondaryWeaponIndex < weaponOptions.Count
        ? weaponOptions[selectedSecondaryWeaponIndex]
        : null;

    internal AirMissionOption? SelectedOption => options.Count == 0
        ? null
        : options[Mathf.Clamp(selectedOptionIndex, 0, options.Count - 1)];

    internal AirbaseOption? SelectedAirbase => airbases.Count == 0
        ? null
        : airbases[Mathf.Clamp(selectedAirbaseIndex, 0, airbases.Count - 1)];

    internal void Activate()
    {
        nextMissionPruneAt = CommanderScheduler.Stagger("air-command.prune", MissionPruneIntervalSeconds, 0.7f);
        nextAirbaseRefreshAt = Time.unscaledTime;
    }

    internal void Deactivate()
    {
        uiVisible = false;
        CancelAreaSelection(showStatus: false);
    }

    internal void ResetSession()
    {
        uiVisible = false;
        pendingAreaSelection = null;
        pendingAircraftSpawn = null;
        options.Clear();
        weaponOptions.Clear();
        airbases.Clear();
        missions.Clear();
        staleAircraft.Clear();
        mapClickTracker.Reset();
        selectedPrimaryWeaponIndex = -1;
        selectedSecondaryWeaponIndex = -1;
        statusText = string.Empty;
    }

    internal void Tick()
    {
        if (pendingAreaSelection != null)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelAreaSelection(showStatus: true);
            }
            else
            {
                TryHandleTacticalMapClick();
            }
        }

        if (pendingAircraftSpawn != null && Time.unscaledTime > pendingAircraftSpawn.ExpiresAt)
        {
            SetStatus($"Aircraft assignment timed out for {GetAircraftLabel(pendingAircraftSpawn.Option.Definition)}.");
            pendingAircraftSpawn = null;
        }

        if (CommanderScheduler.IsDue(ref nextMissionPruneAt, MissionPruneIntervalSeconds))
        {
            PruneMissions();
        }

        if (uiVisible && Time.unscaledTime >= nextAirbaseRefreshAt)
        {
            nextAirbaseRefreshAt = Time.unscaledTime + 0.5f;
            RefreshAirbases();
        }
    }

    internal void SetUiVisible(bool visible)
    {
        uiVisible = visible && CommanderSettings.EnableAirCommand;
        if (visible)
        {
            RefreshOptions();
        }
    }

    internal void SelectMode(AirCommandMode mode)
    {
        if (selectedMode == mode && options.Count > 0)
        {
            return;
        }

        selectedMode = mode;
        selectedOptionIndex = 0;
        selectedAirbaseIndex = 0;
        selectedPrimaryWeaponIndex = -1;
        selectedSecondaryWeaponIndex = -1;
        RefreshOptions();
    }

    internal void SelectOption(int index)
    {
        if (index < 0 || index >= options.Count)
        {
            return;
        }

        selectedOptionIndex = index;
        RefreshAirbases();
    }

    internal void SelectAirbase(int index)
    {
        if (index >= 0 && index < airbases.Count)
        {
            selectedAirbaseIndex = index;
        }
    }

    internal void SelectPrimaryWeapon(int index)
    {
        selectedPrimaryWeaponIndex = index >= 0 && index < weaponOptions.Count ? index : -1;
        if (SelectedPrimaryWeapon != null && SameMountType(SelectedPrimaryWeapon, SelectedSecondaryWeapon))
        {
            selectedSecondaryWeaponIndex = -1;
        }
        ApplySelectedWeaponsAndSort();
    }

    internal void SelectSecondaryWeapon(int index)
    {
        WeaponMount? candidate = index >= 0 && index < weaponOptions.Count ? weaponOptions[index] : null;
        selectedSecondaryWeaponIndex = candidate != null && !SameMountType(candidate, SelectedPrimaryWeapon) ? index : -1;
        ApplySelectedWeaponsAndSort();
    }

    internal void BeginAreaSelection()
    {
        if (!CommanderSettings.EnableAirCommand)
        {
            SetStatus("Air Command is disabled in Commander settings.");
            return;
        }

        AirMissionOption? option = SelectedOption;
        AirbaseOption? airbase = SelectedAirbase;
        if (option == null)
        {
            SetStatus("No aircraft with configurable hardpoints is available.");
            return;
        }

        if (SelectedPrimaryWeapon == null || GetPrimaryWeaponCount(option) <= 0)
        {
            SetStatus("Select a primary weapon supported by the selected aircraft.");
            return;
        }

        if (airbase == null)
        {
            SetStatus("No compatible friendly airbase is available.");
            return;
        }

        pendingAreaSelection = new PendingAreaSelection(option, airbase.Airbase);
        tacticalMapService.OpenFullscreen();
        tacticalMapService.SuppressMapFollow = true;
        mapClickTracker.Reset();
        SetStatus("Select the mission area on the tactical map or in the 3D world. Esc cancels.");
    }

    internal bool TrySetAreaFromWorld(Vector2 screenPosition)
    {
        if (pendingAreaSelection == null)
        {
            return false;
        }

        if (!CommanderGameAccess.TryRaycastWorldPosition(screenPosition, out GlobalPosition target))
        {
            SetStatus("No valid mission area was found under the cursor.");
            return true;
        }

        CompleteAreaSelection(target);
        return true;
    }

    internal string GetOptionLabel(AirMissionOption option)
    {
        FactionHQ? hq = CommanderGameAccess.GetLocalHq();
        int supply = hq?.GetUnitSupply(option.Definition) ?? 0;
        string cost = UnitConverter.ValueReading(option.Definition.value) ?? option.Definition.value.ToString("F1");
        return $"{GetAircraftLabel(option.Definition)}  |  PRI {GetPrimaryWeaponCount(option)} / SEC {GetSecondaryWeaponCount(option)}\nSupply {supply}  |  Cost {cost}";
    }

    internal string GetAirbaseLabel(AirbaseOption option)
    {
        string distance = UnitConverter.DistanceReading(option.Distance) ?? $"{option.Distance:F0} m";
        return $"{(option.Ready ? "READY" : "BUSY")}  |  {option.Label}  |  {distance}";
    }

    internal static void NotifyFactionUnitRegistered(FactionHQ hq, Unit unit)
    {
        Instance?.TryAssignPendingAircraft(hq, unit);
    }

    internal static void NotifyAircraftReturned(Aircraft aircraft)
    {
        Instance?.missions.Remove(aircraft);
    }

    internal static void NotifyUnitDisabled(Unit unit)
    {
        if (unit is Aircraft aircraft)
        {
            Instance?.missions.Remove(aircraft);
        }
    }

    internal static bool TryChooseMissionTarget(
        Unit searcher,
        List<WeaponStation> stations,
        out CombatAI.TargetSearchResults result)
    {
        result = default;
        if (Instance == null
            || searcher is not Aircraft aircraft
            || !Instance.missions.TryGetValue(aircraft, out AirMission mission))
        {
            return false;
        }

        result = Instance.ChooseMissionTarget(aircraft, stations, mission);
        return true;
    }

    internal static bool TryGetMissionHoldPoint(AIPilotCombatModes state, out GlobalPosition point)
    {
        point = default;
        Aircraft? aircraft = CommanderAirCommandPatches.GetStateAircraft(state);
        if (aircraft == null || Instance == null || !Instance.missions.TryGetValue(aircraft, out AirMission mission))
        {
            return false;
        }

        point = mission.AreaCenter;
        return true;
    }

    private CombatAI.TargetSearchResults ChooseMissionTarget(
        Aircraft aircraft,
        List<WeaponStation> stations,
        AirMission mission)
    {
        Unit? bestTarget = null;
        WeaponStation? bestStation = null;
        float bestOpportunity = 0f;
        float bestScore = 0f;
        bool outOfAmmo = true;
        FactionHQ? hq = aircraft.NetworkHQ;
        if (hq == null)
        {
            return new CombatAI.TargetSearchResults(null!, null!, 0f, true);
        }

        for (int stationIndex = 0; stationIndex < stations.Count; stationIndex++)
        {
            WeaponStation station = stations[stationIndex];
            if (station == null || station.Cargo || !IsStationEligible(station, mission.Mode))
            {
                continue;
            }

            if (station.Ammo <= 0)
            {
                continue;
            }

            outOfAmmo = false;
            foreach (KeyValuePair<PersistentID, TrackingInfo> entry in hq.trackingDatabase)
            {
                TrackingInfo tracking = entry.Value;
                if (!tracking.TryGetUnit(out Unit target)
                    || target == null
                    || target.disabled
                    || target.NetworkHQ == null
                    || target.NetworkHQ == hq
                    || !IsTargetEligible(target, mission.Mode)
                    || !FastMath.InRange(tracking.GetPosition(), mission.AreaCenter, mission.Radius))
                {
                    continue;
                }

                float range = Mathf.Max(FastMath.Distance(tracking.GetPosition(), aircraft.GlobalPosition()), 100f);
                OpportunityThreat assessment = CombatAI.AnalyzeTarget(station, aircraft, tracking, 0f, range, mobile: true);
                float score = assessment.GetCombinedScore() / range;
                if (score <= bestScore || !hq.IsTargetPositionAccurate(target, 1000f))
                {
                    continue;
                }

                bestScore = score;
                bestOpportunity = assessment.opportunity;
                bestTarget = target;
                bestStation = station;
            }
        }

        return new CombatAI.TargetSearchResults(bestTarget!, bestStation!, bestOpportunity, outOfAmmo);
    }

    private void RefreshOptions()
    {
        options.Clear();
        airbases.Clear();
        FactionHQ? hq = CommanderGameAccess.GetLocalHq();
        if (!uiVisible || hq == null)
        {
            return;
        }

        List<AircraftDefinition> definitions = new();
        HashSet<AircraftDefinition> seen = new();
        Encyclopedia encyclopedia = Encyclopedia.i;
        if (encyclopedia?.aircraft != null)
        {
            for (int i = 0; i < encyclopedia.aircraft.Count; i++)
            {
                AircraftDefinition definition = encyclopedia.aircraft[i];
                if (definition != null && seen.Add(definition))
                {
                    definitions.Add(definition);
                }
            }
        }

        AircraftDefinition[] resourceDefinitions = Resources.FindObjectsOfTypeAll<AircraftDefinition>();
        for (int i = 0; i < resourceDefinitions.Length; i++)
        {
            AircraftDefinition definition = resourceDefinitions[i];
            if (definition != null && seen.Add(definition))
            {
                definitions.Add(definition);
            }
        }

        int rejectedDefinition = 0;
        int rejectedPilot = 0;
        int rejectedLoadout = 0;
        int rejectedAirbase = 0;
        for (int i = 0; i < definitions.Count; i++)
        {
            AircraftDefinition definition = definitions[i];
            if (definition == null
                || definition.unitPrefab == null
                || definition.aircraftParameters == null)
            {
                rejectedDefinition++;
                continue;
            }

            if (!HasPlanePilot(definition))
            {
                rejectedPilot++;
                continue;
            }

            AirMissionOption? option = CreateVariableLoadoutOption(definition, hq, selectedMode);
            if (option == null)
            {
                rejectedLoadout++;
                continue;
            }

            bool supported = false;
            foreach (Airbase airbase in hq.GetAirbases())
            {
                if (IsCompatibleAirbase(airbase, hq, definition))
                {
                    supported = true;
                    break;
                }
            }

            if (supported)
            {
                options.Add(option);
            }
            else
            {
                rejectedAirbase++;
            }
        }

        BuildWeaponOptions();
        if (selectedPrimaryWeaponIndex < 0)
        {
            selectedPrimaryWeaponIndex = FindFirstSuitableWeaponIndex();
        }
        ApplySelectedWeaponsAndSort();
        RefreshAirbases();
        CommanderPlugin.Log.LogInfo(
            $"Air Command options refreshed: mode={selectedMode}, source={definitions.Count}, aircraft={options.Count}, airbases={airbases.Count}, "
            + $"rejected=definition:{rejectedDefinition}/pilot:{rejectedPilot}/loadout:{rejectedLoadout}/airbase:{rejectedAirbase}");
    }

    private void RefreshAirbases()
    {
        Airbase? previouslySelected = SelectedAirbase?.Airbase;
        airbases.Clear();
        AirMissionOption? option = SelectedOption;
        FactionHQ? hq = CommanderGameAccess.GetLocalHq();
        if (option == null || hq == null)
        {
            selectedAirbaseIndex = 0;
            return;
        }

        Camera? camera = SceneSingleton<CameraStateManager>.i?.mainCamera;
        Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
        foreach (Airbase airbase in hq.GetAirbases())
        {
            if (!IsCompatibleAirbase(airbase, hq, option.Definition))
            {
                continue;
            }

            Transform position = airbase.center != null ? airbase.center : airbase.transform;
            airbases.Add(new AirbaseOption(
                airbase,
                GetAirbaseName(airbase),
                Vector3.Distance(cameraPosition, position.position),
                airbase.CanSpawnAircraft(option.Definition)));
        }

        airbases.Sort(static (left, right) => left.Distance.CompareTo(right.Distance));
        int preservedIndex = previouslySelected == null
            ? -1
            : airbases.FindIndex(option => ReferenceEquals(option.Airbase, previouslySelected));
        selectedAirbaseIndex = preservedIndex >= 0
            ? preservedIndex
            : Mathf.Clamp(selectedAirbaseIndex, 0, Mathf.Max(airbases.Count - 1, 0));
    }

    private void TryHandleTacticalMapClick()
    {
        if (!DynamicMap.mapMaximized)
        {
            return;
        }

        DynamicMap? map = SceneSingleton<DynamicMap>.i;
        if (map != null && mapClickTracker.Tick(map, out GlobalPosition target))
        {
            CompleteAreaSelection(target);
        }
    }

    private void CompleteAreaSelection(GlobalPosition target)
    {
        PendingAreaSelection? selection = pendingAreaSelection;
        pendingAreaSelection = null;
        tacticalMapService.SuppressMapFollow = false;
        mapClickTracker.Reset();
        tacticalMapService.CloseFullscreen();
        if (selection == null)
        {
            return;
        }

        SpawnMission(selection.Option, selection.Airbase, target);
    }

    private void CancelAreaSelection(bool showStatus)
    {
        if (pendingAreaSelection == null)
        {
            return;
        }

        pendingAreaSelection = null;
        tacticalMapService.SuppressMapFollow = false;
        mapClickTracker.Reset();
        if (tacticalMapService.IsFullscreenOpen)
        {
            tacticalMapService.CloseFullscreen();
        }
        if (showStatus)
        {
            SetStatus("Air mission area selection cancelled.");
        }
    }

    private void SpawnMission(AirMissionOption option, Airbase airbase, GlobalPosition target)
    {
        if (NetworkManagerNuclearOption.i == null || !NetworkManagerNuclearOption.i.Server.Active)
        {
            SetStatus("Air Command is host-only.");
            return;
        }

        FactionHQ? hq = CommanderGameAccess.GetLocalHq();
        if (hq == null || !IsCompatibleAirbase(airbase, hq, option.Definition))
        {
            SetStatus("The selected airbase is no longer compatible.");
            return;
        }

        if (!airbase.CanSpawnAircraft(option.Definition))
        {
            SetStatus("The selected airbase is busy. Retry when a compatible hangar is free.");
            return;
        }

        if (pendingAircraftSpawn != null)
        {
            SetStatus("Wait for the previous Air Command aircraft to finish spawning.");
            return;
        }

        bool purchased = false;
        if (hq.GetUnitSupply(option.Definition) <= 0)
        {
            if (hq.factionFunds < option.Definition.value)
            {
                SetStatus("The faction cannot afford this aircraft.");
                return;
            }

            hq.AddFunds(-option.Definition.value);
            hq.ModifyUnitSupply(option.Definition, 1);
            purchased = true;
        }

        pendingAircraftSpawn = new PendingAircraftSpawn(
            hq,
            option,
            target,
            GetMissionRadius(option.Mode),
            Time.unscaledTime + PendingSpawnTimeoutSeconds);

        int liveryIndex = option.Definition.aircraftParameters.GetRandomLiveryForFaction(hq.faction);
        Loadout loadout = option.BuildLoadout();
        NormalizeLoadoutLength(loadout, option.Definition);
        if (!ValidateSelectedLoadout(option, loadout, airbase, hq, out string loadoutError))
        {
            pendingAircraftSpawn = null;
            if (purchased)
            {
                hq.ModifyUnitSupply(option.Definition, -1);
                hq.AddFunds(option.Definition.value);
            }
            SetStatus(loadoutError);
            return;
        }
        Airbase.TrySpawnResult result = airbase.TrySpawnAircraft(
            null,
            option.Definition,
            new LiveryKey(liveryIndex),
            loadout,
            option.Definition.aircraftParameters.DefaultFuelLevel);
        if (!result.Allowed)
        {
            pendingAircraftSpawn = null;
            if (purchased)
            {
                hq.ModifyUnitSupply(option.Definition, -1);
                hq.AddFunds(option.Definition.value);
            }
            SetStatus("The selected airbase rejected the aircraft spawn.");
            return;
        }

        SetStatus($"{GetModeLabel(option.Mode)} mission launched: {GetAircraftLabel(option.Definition)} / {option.LoadoutName}.");
        CommanderPlugin.Log.LogInfo(
            $"Air Command spawned: mode={option.Mode}, aircraft={GetAircraftLabel(option.Definition)}, loadout={option.LoadoutName}, area={target}, purchased={purchased}");
    }

    private void TryAssignPendingAircraft(FactionHQ hq, Unit unit)
    {
        PendingAircraftSpawn? pending = pendingAircraftSpawn;
        if (pending == null
            || !ReferenceEquals(hq, pending.Hq)
            || unit is not Aircraft aircraft
            || aircraft.Player != null
            || !ReferenceEquals(aircraft.definition, pending.Option.Definition))
        {
            return;
        }

        missions[aircraft] = new AirMission(pending.Option.Mode, pending.AreaCenter, pending.Radius);
        CommanderSelectionService.PinMissionUnit(
            aircraft,
            "AIR COMMAND",
            GetModeLabel(pending.Option.Mode));
        pendingAircraftSpawn = null;
        CommanderPlugin.Log.LogInfo(
            $"Air Command assigned: mode={pending.Option.Mode}, aircraft={CommanderGameAccess.GetUnitLabel(aircraft)}, radius={pending.Radius:F0}m");
    }

    private void PruneMissions()
    {
        staleAircraft.Clear();
        foreach (KeyValuePair<Aircraft, AirMission> entry in missions)
        {
            if (entry.Key == null || entry.Key.disabled)
            {
                staleAircraft.Add(entry.Key!);
            }
        }

        for (int i = 0; i < staleAircraft.Count; i++)
        {
            missions.Remove(staleAircraft[i]);
        }
    }

    private static bool TryFindBestLoadout(
        AircraftDefinition definition,
        FactionHQ hq,
        AirCommandMode mode,
        out StandardLoadout loadout,
        out float score)
    {
        loadout = null!;
        score = 0f;
        StandardLoadout[] standardLoadouts = definition.aircraftParameters.StandardLoadouts;
        Aircraft? aircraftPrefab = definition.unitPrefab.GetComponent<Aircraft>();
        if (standardLoadouts == null || aircraftPrefab?.weaponManager == null)
        {
            return false;
        }

        for (int i = 0; i < standardLoadouts.Length; i++)
        {
            StandardLoadout candidate = standardLoadouts[i];
            if (candidate == null || candidate.disabled || candidate.loadout == null)
            {
                continue;
            }

            float candidateScore = ScoreLoadout(candidate.loadout, mode);
            if (candidateScore > score)
            {
                score = candidateScore;
                loadout = candidate;
            }
        }

        return loadout != null && score > 0f;
    }

    private static float ScoreLoadout(Loadout loadout, AirCommandMode mode)
    {
        float score = 0f;
        for (int i = 0; i < loadout.weapons.Count; i++)
        {
            WeaponMount? mount = loadout.weapons[i];
            if (mount == null)
            {
                continue;
            }

            WeaponInfo? info = mount.info;
            switch (mode)
            {
                case AirCommandMode.AwacsJammer:
                    if (mount.radar || mount.prefab?.GetComponentInChildren<Radar>(true) != null)
                    {
                        score += 8f;
                    }
                    if (info?.jammer == true)
                    {
                        score += 12f;
                    }
                    break;

                case AirCommandMode.Cas:
                    if (info != null && !info.nuclear && !info.jammer && info.effectiveness.antiSurface > 0.05f)
                    {
                        score += ScoreConventionalWeapon(info.effectiveness.antiSurface, mount, info);
                    }
                    break;

                case AirCommandMode.AirGuard:
                    if (info != null
                        && !info.jammer
                        && info.effectiveness.antiAir > 0.05f
                        && info.effectiveness.antiSurface <= 0.05f)
                    {
                        score += ScoreConventionalWeapon(info.effectiveness.antiAir, mount, info);
                    }
                    break;

                case AirCommandMode.Arad:
                    if (info != null && IsAradWeapon(info))
                    {
                        score += ScoreConventionalWeapon(Mathf.Max(info.effectiveness.antiRadar, 0.5f), mount, info);
                    }
                    break;

                case AirCommandMode.StrategicStrike:
                    if (info?.strategic == true)
                    {
                        score += 20f + ScoreConventionalWeapon(
                            Mathf.Max(info.effectiveness.antiSurface, info.effectiveness.antiRadar, 0.5f), mount, info);
                    }
                    break;
            }
        }

        return score;
    }

    private static float ScoreConventionalWeapon(float effectiveness, WeaponMount mount, WeaponInfo info)
    {
        // Gun ammo counts are several orders of magnitude larger than discrete
        // stores, so treating every round as a complete weapon overwhelms CAS.
        float quantity = info.gun ? 1.5f : Mathf.Sqrt(Mathf.Max(mount.ammo, 1));
        float delivery = info.overHorizon ? 1.2f : info.bomb ? 0.9f : 1f;
        return effectiveness * quantity * delivery;
    }

    private static bool IsStationEligible(WeaponStation station, AirCommandMode mode)
    {
        WeaponInfo? info = station.WeaponInfo;
        if (info == null)
        {
            return false;
        }

        return mode switch
        {
            AirCommandMode.AwacsJammer => info.jammer,
            AirCommandMode.Cas => !info.nuclear && !info.jammer && info.effectiveness.antiSurface > 0.05f,
            // Dual-role weapons remain manually usable, but ScoreLoadout keeps
            // them out of the recommended Air Superiority section.
            AirCommandMode.AirGuard => !info.jammer && info.effectiveness.antiAir > 0.05f,
            AirCommandMode.Arad => IsAradWeapon(info),
            AirCommandMode.StrategicStrike => info.strategic,
            _ => false,
        };
    }

    private static bool IsTargetEligible(Unit target, AirCommandMode mode)
    {
        return mode switch
        {
            AirCommandMode.AwacsJammer => target.HasRadarEmission(),
            AirCommandMode.Cas => target is GroundVehicle || target is Ship || target is Building,
            AirCommandMode.AirGuard => target is Aircraft,
            AirCommandMode.Arad => target is not Aircraft && target.HasRadarEmission(),
            AirCommandMode.StrategicStrike => target is GroundVehicle || target is Ship || target is Building,
            _ => false,
        };
    }

    private static bool IsAradWeapon(WeaponInfo info)
    {
        return info.targetRequirements.minRadar > 0f
            || info.effectiveness.antiRadar > Mathf.Max(info.effectiveness.antiAir, 0.05f)
            || info.weaponPrefab?.GetComponentInChildren<ARMSeeker>(true) != null
            || info.weaponName?.IndexOf("ARAD", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void NormalizeLoadoutLength(Loadout loadout, AircraftDefinition definition)
    {
        Aircraft? aircraft = definition.unitPrefab != null
            ? definition.unitPrefab.GetComponent<Aircraft>()
            : null;
        int hardpointCount = aircraft?.weaponManager?.hardpointSets?.Length ?? loadout.weapons.Count;
        while (loadout.weapons.Count < hardpointCount)
        {
            loadout.weapons.Add(null!);
        }
        if (loadout.weapons.Count > hardpointCount)
        {
            loadout.weapons.RemoveRange(hardpointCount, loadout.weapons.Count - hardpointCount);
        }
    }

    private static bool HasPlanePilot(AircraftDefinition definition)
    {
        Aircraft? aircraft = definition.unitPrefab.GetComponent<Aircraft>();
        if (aircraft?.pilots == null)
        {
            return false;
        }

        for (int i = 0; i < aircraft.pilots.Length; i++)
        {
            if (aircraft.pilots[i] != null && aircraft.pilots[i].pilotType == Pilot.PilotType.Plane)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCompatibleAirbase(Airbase? airbase, FactionHQ hq, AircraftDefinition definition)
    {
        if (airbase == null || airbase.disabled || !airbase.GetAvailableAircraft().Contains(definition))
        {
            return false;
        }

        foreach (Airbase ownedAirbase in hq.GetAirbases())
        {
            if (ReferenceEquals(ownedAirbase, airbase))
            {
                return true;
            }
        }

        return false;
    }

    private static float GetMissionRadius(AirCommandMode mode)
    {
        return mode switch
        {
            AirCommandMode.Cas => 20000f,
            AirCommandMode.AirGuard => 30000f,
            AirCommandMode.Arad => 50000f,
            AirCommandMode.StrategicStrike => 80000f,
            _ => 60000f,
        };
    }

    internal static string GetModeLabel(AirCommandMode mode)
    {
        return mode switch
        {
            AirCommandMode.AwacsJammer => "AWACS / JAMMER",
            AirCommandMode.Cas => "CAS",
            AirCommandMode.AirGuard => "AIR SUPERIORITY",
            AirCommandMode.Arad => "ARAD",
            AirCommandMode.StrategicStrike => "STRATEGIC STRIKE",
            _ => mode.ToString().ToUpperInvariant(),
        };
    }

    private static string GetAircraftLabel(AircraftDefinition definition)
    {
        return !string.IsNullOrWhiteSpace(definition.unitName)
            ? definition.unitName
            : !string.IsNullOrWhiteSpace(definition.code)
                ? definition.code
                : definition.name;
    }

    private static string GetAirbaseName(Airbase airbase)
    {
        if (airbase.SavedAirbase != null)
        {
            if (!string.IsNullOrWhiteSpace(airbase.SavedAirbase.DisplayName))
            {
                return airbase.SavedAirbase.DisplayName;
            }
            if (!string.IsNullOrWhiteSpace(airbase.SavedAirbase.UniqueName))
            {
                return airbase.SavedAirbase.UniqueName;
            }
        }

        return airbase.name;
    }

    private void SetStatus(string text)
    {
        statusText = text;
        statusUntil = Time.unscaledTime + StatusDurationSeconds;
    }

    internal enum AirCommandMode
    {
        AwacsJammer,
        Cas,
        AirGuard,
        Arad,
        StrategicStrike,
    }

    internal sealed class AirMissionOption
    {
        internal AirMissionOption(
            AircraftDefinition definition,
            AirCommandMode mode,
            HardpointSet[] hardpointSets,
            List<AirHardpointGroup> hardpointGroups)
        {
            Definition = definition;
            Mode = mode;
            HardpointSets = hardpointSets;
            HardpointGroups = hardpointGroups;
        }

        internal AircraftDefinition Definition { get; }
        internal HardpointSet[] HardpointSets { get; }
        internal List<AirHardpointGroup> HardpointGroups { get; }
        internal string LoadoutName => "Custom hardpoints";
        internal float Score => ScoreLoadout(BuildLoadout(), Mode);
        internal AirCommandMode Mode { get; }

        internal Loadout BuildLoadout()
        {
            Loadout loadout = new();
            for (int i = 0; i < HardpointSets.Length; i++)
            {
                loadout.weapons.Add(null!);
            }
            for (int i = 0; i < HardpointGroups.Count; i++)
            {
                AirHardpointGroup group = HardpointGroups[i];
                WeaponMount? mount = group.SelectedMount;
                for (int index = 0; index < group.HardpointIndices.Count; index++)
                {
                    loadout.weapons[group.HardpointIndices[index]] = mount!;
                }
            }
            return loadout;
        }
    }

    internal sealed class AirHardpointGroup
    {
        private int selectedIndex = -1;

        internal AirHardpointGroup(string label, List<int> hardpointIndices, List<WeaponMount> mounts, int physicalMountCount)
        {
            Label = label;
            HardpointIndices = hardpointIndices;
            Mounts = mounts;
            PhysicalMountCount = physicalMountCount;
        }

        internal string Label { get; }
        internal List<int> HardpointIndices { get; }
        internal List<WeaponMount> Mounts { get; }
        internal int PhysicalMountCount { get; }
        internal WeaponMount? SelectedMount => selectedIndex >= 0 && selectedIndex < Mounts.Count ? Mounts[selectedIndex] : null;

        internal void Select(int index) => selectedIndex = index >= 0 && index < Mounts.Count ? index : -1;
        internal void Clear() => selectedIndex = -1;
    }

    internal sealed class AirbaseOption
    {
        internal AirbaseOption(Airbase airbase, string label, float distance, bool ready)
        {
            Airbase = airbase;
            Label = label;
            Distance = distance;
            Ready = ready;
        }

        internal Airbase Airbase { get; }
        internal string Label { get; }
        internal float Distance { get; }
        internal bool Ready { get; }
    }

    private sealed class PendingAreaSelection
    {
        internal PendingAreaSelection(AirMissionOption option, Airbase airbase)
        {
            Option = option;
            Airbase = airbase;
        }

        internal AirMissionOption Option { get; }
        internal Airbase Airbase { get; }
    }

    private sealed class PendingAircraftSpawn
    {
        internal PendingAircraftSpawn(FactionHQ hq, AirMissionOption option, GlobalPosition areaCenter, float radius, float expiresAt)
        {
            Hq = hq;
            Option = option;
            AreaCenter = areaCenter;
            Radius = radius;
            ExpiresAt = expiresAt;
        }

        internal FactionHQ Hq { get; }
        internal AirMissionOption Option { get; }
        internal GlobalPosition AreaCenter { get; }
        internal float Radius { get; }
        internal float ExpiresAt { get; }
    }

    private sealed class AirMission
    {
        internal AirMission(AirCommandMode mode, GlobalPosition areaCenter, float radius)
        {
            Mode = mode;
            AreaCenter = areaCenter;
            Radius = radius;
        }

        internal AirCommandMode Mode { get; }
        internal GlobalPosition AreaCenter { get; }
        internal float Radius { get; }
    }
}
