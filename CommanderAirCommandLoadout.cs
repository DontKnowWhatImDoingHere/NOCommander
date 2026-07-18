using System;
using System.Collections.Generic;
using NuclearOption.SavedMission;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed partial class CommanderAirCommandService
{
    private static AirMissionOption? CreateVariableLoadoutOption(
        AircraftDefinition definition,
        FactionHQ hq,
        AirCommandMode mode)
    {
        Aircraft? aircraft = definition.unitPrefab.GetComponent<Aircraft>();
        HardpointSet[]? sets = aircraft?.weaponManager?.hardpointSets;
        if (sets == null || sets.Length == 0)
        {
            return null;
        }

        List<AirHardpointGroup> groups = new();
        for (int i = 0; i < sets.Length; i++)
        {
            List<int> indices = new() { i };
            string label = GetHardpointSetName(sets[i], i);
            if (i + 1 < sets.Length && sets[i + 1].SymmetryWithPrev)
            {
                indices.Add(i + 1);
                if (!string.IsNullOrWhiteSpace(sets[i + 1].SymmetryName))
                {
                    label = sets[i + 1].SymmetryName;
                }
                i++;
            }

            List<WeaponMount> mounts = GetCommonCombatMounts(sets, indices, hq);
            int physicalMounts = 0;
            for (int index = 0; index < indices.Count; index++)
            {
                physicalMounts += Mathf.Max(sets[indices[index]].hardpoints?.Count ?? 0, 1);
            }
            groups.Add(new AirHardpointGroup(label, indices, mounts, physicalMounts));
        }

        AirMissionOption option = new(definition, mode, sets, groups);
        return option;
    }

    private void BuildWeaponOptions()
    {
        weaponOptions.Clear();
        Dictionary<string, int> catalogIndex = new(StringComparer.OrdinalIgnoreCase);
        for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
        {
            AirMissionOption option = options[optionIndex];
            for (int groupIndex = 0; groupIndex < option.HardpointGroups.Count; groupIndex++)
            {
                List<WeaponMount> mounts = option.HardpointGroups[groupIndex].Mounts;
                for (int mountIndex = 0; mountIndex < mounts.Count; mountIndex++)
                {
                    WeaponMount mount = mounts[mountIndex];
                    string key = GetWeaponTypeKey(mount);
                    if (!catalogIndex.TryGetValue(key, out int existingIndex))
                    {
                        catalogIndex[key] = weaponOptions.Count;
                        weaponOptions.Add(mount);
                    }
                    else if (GetRackStoreCount(mount) > GetRackStoreCount(weaponOptions[existingIndex]))
                    {
                        weaponOptions[existingIndex] = mount;
                    }
                }
            }
        }

        weaponOptions.Sort((left, right) =>
        {
            float leftScore = ScoreMount(left, selectedMode);
            float rightScore = ScoreMount(right, selectedMode);
            bool leftSuitable = leftScore > 0f;
            bool rightSuitable = rightScore > 0f;
            if (leftSuitable != rightSuitable)
            {
                return leftSuitable ? -1 : 1;
            }
            int scoreCompare = rightScore.CompareTo(leftScore);
            return scoreCompare != 0
                ? scoreCompare
                : string.Compare(GetMountName(left), GetMountName(right), StringComparison.OrdinalIgnoreCase);
        });
    }

    private int FindFirstSuitableWeaponIndex()
    {
        for (int i = 0; i < weaponOptions.Count; i++)
        {
            if (ScoreMount(weaponOptions[i], selectedMode) > 0f)
            {
                return i;
            }
        }
        return -1;
    }

    private void ApplySelectedWeaponsAndSort()
    {
        AircraftDefinition? selectedDefinition = SelectedOption?.Definition;
        for (int i = 0; i < options.Count; i++)
        {
            ConfigureOptionLoadout(options[i], SelectedPrimaryWeapon, SelectedSecondaryWeapon);
        }

        options.Sort((left, right) =>
        {
            int primary = GetPrimaryWeaponCount(right).CompareTo(GetPrimaryWeaponCount(left));
            if (primary != 0) return primary;
            int secondary = GetSecondaryWeaponCount(right).CompareTo(GetSecondaryWeaponCount(left));
            return secondary != 0
                ? secondary
                : string.Compare(GetAircraftLabel(left.Definition), GetAircraftLabel(right.Definition), StringComparison.OrdinalIgnoreCase);
        });

        selectedOptionIndex = 0;
        if (selectedDefinition != null)
        {
            int preserved = options.FindIndex(option => ReferenceEquals(option.Definition, selectedDefinition));
            if (preserved >= 0 && GetPrimaryWeaponCount(options[preserved]) > 0)
            {
                selectedOptionIndex = preserved;
            }
        }
        RefreshAirbases();
    }

    private static void ConfigureOptionLoadout(
        AirMissionOption option,
        WeaponMount? primary,
        WeaponMount? secondary)
    {
        for (int i = 0; i < option.HardpointGroups.Count; i++)
        {
            option.HardpointGroups[i].Clear();
        }
        if (primary == null)
        {
            return;
        }

        List<int> primaryGroups = FindMaximumWeaponGroups(option, primary, Array.Empty<int>());
        for (int i = 0; i < primaryGroups.Count; i++)
        {
            SelectEquivalentMount(option.HardpointGroups[primaryGroups[i]], primary);
        }

        if (secondary == null)
        {
            return;
        }

        List<int> secondaryGroups = FindMaximumWeaponGroups(option, secondary, primaryGroups);
        for (int i = 0; i < secondaryGroups.Count; i++)
        {
            SelectEquivalentMount(option.HardpointGroups[secondaryGroups[i]], secondary);
        }
    }

    private static List<int> FindMaximumWeaponGroups(
        AirMissionOption option,
        WeaponMount weaponType,
        IReadOnlyList<int> lockedGroups)
    {
        List<int> candidates = new();
        for (int i = 0; i < option.HardpointGroups.Count; i++)
        {
            AirHardpointGroup group = option.HardpointGroups[i];
            if (group.SelectedMount != null || FindEquivalentMountIndex(group, weaponType) < 0)
            {
                continue;
            }

            bool conflictsWithLocked = false;
            for (int locked = 0; locked < lockedGroups.Count; locked++)
            {
                if (GroupsConflict(option.HardpointSets, group, option.HardpointGroups[lockedGroups[locked]]))
                {
                    conflictsWithLocked = true;
                    break;
                }
            }
            if (!conflictsWithLocked)
            {
                candidates.Add(i);
            }
        }

        // Aircraft have few logical station groups, so an exact search is cheap
        // and avoids combined bays accidentally reducing the primary store count.
        List<int> best = new();
        int bestCapacity = -1;
        FindMaximumWeaponGroupsRecursive(
            option, weaponType, candidates, 0, new List<int>(), 0, ref bestCapacity, ref best);
        return best;
    }

    private static void FindMaximumWeaponGroupsRecursive(
        AirMissionOption option,
        WeaponMount weaponType,
        List<int> candidates,
        int candidateIndex,
        List<int> selected,
        int capacity,
        ref int bestCapacity,
        ref List<int> best)
    {
        if (candidateIndex >= candidates.Count)
        {
            if (capacity > bestCapacity)
            {
                bestCapacity = capacity;
                best = new List<int>(selected);
            }
            return;
        }

        FindMaximumWeaponGroupsRecursive(
            option, weaponType, candidates, candidateIndex + 1, selected, capacity, ref bestCapacity, ref best);

        int groupIndex = candidates[candidateIndex];
        AirHardpointGroup group = option.HardpointGroups[groupIndex];
        for (int i = 0; i < selected.Count; i++)
        {
            if (GroupsConflict(option.HardpointSets, group, option.HardpointGroups[selected[i]]))
            {
                return;
            }
        }

        selected.Add(groupIndex);
        FindMaximumWeaponGroupsRecursive(
            option,
            weaponType,
            candidates,
            candidateIndex + 1,
            selected,
            capacity + GetGroupWeaponCapacity(group, weaponType),
            ref bestCapacity,
            ref best);
        selected.RemoveAt(selected.Count - 1);
    }

    private int GetPrimaryWeaponCount(AirMissionOption option) => CountMountedType(option, SelectedPrimaryWeapon);
    private int GetSecondaryWeaponCount(AirMissionOption option) => CountMountedType(option, SelectedSecondaryWeapon);

    private static int CountMountedType(AirMissionOption option, WeaponMount? requested)
    {
        if (requested == null) return 0;
        int count = 0;
        for (int i = 0; i < option.HardpointGroups.Count; i++)
        {
            AirHardpointGroup group = option.HardpointGroups[i];
            if (SameMountType(group.SelectedMount, requested))
            {
                int storesPerMount = group.SelectedMount?.info?.gun == true
                    ? 1
                    : Mathf.Max(group.SelectedMount?.ammo ?? 1, 1);
                count += group.PhysicalMountCount * storesPerMount;
            }
        }
        return count;
    }

    private static void SelectEquivalentMount(AirHardpointGroup group, WeaponMount requested)
    {
        group.Select(FindEquivalentMountIndex(group, requested));
    }

    private static int FindEquivalentMountIndex(AirHardpointGroup group, WeaponMount requested)
    {
        string key = GetWeaponTypeKey(requested);
        int bestIndex = -1;
        int bestStores = -1;
        for (int i = 0; i < group.Mounts.Count; i++)
        {
            WeaponMount candidate = group.Mounts[i];
            if (string.Equals(GetWeaponTypeKey(candidate), key, StringComparison.OrdinalIgnoreCase)
                && GetRackStoreCount(candidate) > bestStores)
            {
                bestIndex = i;
                bestStores = GetRackStoreCount(candidate);
            }
        }
        return bestIndex;
    }

    private static bool SameMountType(WeaponMount? left, WeaponMount? right)
    {
        return left != null && right != null
            && string.Equals(GetWeaponTypeKey(left), GetWeaponTypeKey(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string GetWeaponTypeKey(WeaponMount mount)
    {
        WeaponInfo? info = mount.info;
        if (!string.IsNullOrWhiteSpace(info?.weaponName)) return info!.weaponName;
        if (!string.IsNullOrWhiteSpace(info?.shortName)) return info!.shortName;
        if (!string.IsNullOrWhiteSpace(info?.name)) return info!.name;
        if (!string.IsNullOrWhiteSpace(mount.name)) return mount.name;
        return GetMountName(mount);
    }

    private static int GetRackStoreCount(WeaponMount mount)
    {
        return mount.info?.gun == true ? 1 : Mathf.Max(mount.ammo, 1);
    }

    private static int GetGroupWeaponCapacity(AirHardpointGroup group, WeaponMount weaponType)
    {
        int mountIndex = FindEquivalentMountIndex(group, weaponType);
        return mountIndex < 0
            ? 0
            : group.PhysicalMountCount * GetRackStoreCount(group.Mounts[mountIndex]);
    }

    internal bool IsWeaponSuitable(WeaponMount mount) => ScoreMount(mount, selectedMode) > 0f;

    internal string GetMissionWeaponLabel(WeaponMount mount)
    {
        return $"{GetWeaponTypeName(mount)}  | score {ScoreMount(mount, selectedMode):F2}";
    }

    private static string GetWeaponTypeName(WeaponMount mount)
    {
        WeaponInfo? info = mount.info;
        if (!string.IsNullOrWhiteSpace(info?.weaponName)) return info!.weaponName;
        if (!string.IsNullOrWhiteSpace(info?.shortName)) return info!.shortName;
        return GetMountName(mount);
    }

    private static List<WeaponMount> GetCommonCombatMounts(
        HardpointSet[] sets,
        List<int> indices,
        FactionHQ hq)
    {
        List<WeaponMount> result = new();
        List<WeaponMount>? firstOptions = sets[indices[0]].weaponOptions;
        if (firstOptions == null)
        {
            return result;
        }

        for (int mountIndex = 0; mountIndex < firstOptions.Count; mountIndex++)
        {
            WeaponMount? mount = firstOptions[mountIndex];
            if (mount == null
                || mount.Cargo
                || mount.Troops
                || mount.info?.cargo == true
                || mount.info?.troops == true
                || !WeaponChecker.MountAllowedHQ(mount, hq))
            {
                continue;
            }

            bool availableOnEveryMirroredSet = true;
            for (int index = 1; index < indices.Count; index++)
            {
                if (!sets[indices[index]].weaponOptions.Contains(mount))
                {
                    availableOnEveryMirroredSet = false;
                    break;
                }
            }

            if (availableOnEveryMirroredSet && !result.Contains(mount))
            {
                result.Add(mount);
            }
        }

        result.Sort(static (left, right) =>
            string.Compare(GetMountName(left), GetMountName(right), StringComparison.OrdinalIgnoreCase));
        return result;
    }

    private static void AutoConfigureRoleLoadout(AirMissionOption option)
    {
        for (int groupIndex = 0; groupIndex < option.HardpointGroups.Count; groupIndex++)
        {
            AirHardpointGroup group = option.HardpointGroups[groupIndex];
            int bestIndex = -1;
            float bestScore = 0f;
            for (int mountIndex = 0; mountIndex < group.Mounts.Count; mountIndex++)
            {
                float score = ScoreMount(group.Mounts[mountIndex], option.Mode) * group.PhysicalMountCount;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = mountIndex;
                }
            }

            if (bestIndex >= 0)
            {
                SelectHardpointMount(option, groupIndex, bestIndex);
            }
        }
    }

    internal void SelectHardpointMount(int groupIndex, int mountIndex)
    {
        AirMissionOption? option = SelectedOption;
        if (option != null)
        {
            SelectHardpointMount(option, groupIndex, mountIndex);
        }
    }

    private static void SelectHardpointMount(AirMissionOption option, int groupIndex, int mountIndex)
    {
        if (groupIndex < 0 || groupIndex >= option.HardpointGroups.Count)
        {
            return;
        }

        AirHardpointGroup selected = option.HardpointGroups[groupIndex];
        selected.Select(mountIndex);
        if (selected.SelectedMount == null)
        {
            return;
        }

        for (int otherIndex = 0; otherIndex < option.HardpointGroups.Count; otherIndex++)
        {
            if (otherIndex != groupIndex
                && GroupsConflict(option.HardpointSets, selected, option.HardpointGroups[otherIndex]))
            {
                option.HardpointGroups[otherIndex].Clear();
            }
        }
    }

    internal string GetHardpointButtonLabel(AirMissionOption option, AirHardpointGroup group)
    {
        string mount = group.SelectedMount == null ? "EMPTY" : GetMountName(group.SelectedMount);
        string mirror = group.PhysicalMountCount > 1 ? $"  [MIRRORED x{group.PhysicalMountCount}]" : string.Empty;
        string excludes = GetConflictSummary(option, group);
        return $"{group.Label}{mirror}\n{mount}{excludes}";
    }

    internal string GetHardpointMountLabel(WeaponMount mount)
    {
        WeaponInfo? info = mount.info;
        List<string> tags = new();
        if (mount.radar || mount.prefab?.GetComponentInChildren<Radar>(true) != null) tags.Add("RADAR");
        if (info?.jammer == true) tags.Add("ECM");
        if (info != null && IsAradWeapon(info)) tags.Add("ARAD");
        if (info?.effectiveness.antiAir > 0.05f) tags.Add("A/A");
        if (info?.effectiveness.antiSurface > 0.05f) tags.Add("A/G");
        string suffix = tags.Count > 0 ? $"  [{string.Join(", ", tags)}]" : string.Empty;
        return GetMountName(mount) + suffix;
    }

    private static string GetConflictSummary(AirMissionOption option, AirHardpointGroup group)
    {
        List<string> names = new();
        for (int i = 0; i < option.HardpointGroups.Count; i++)
        {
            AirHardpointGroup other = option.HardpointGroups[i];
            if (!ReferenceEquals(group, other) && GroupsConflict(option.HardpointSets, group, other))
            {
                names.Add(other.Label);
            }
        }
        return names.Count == 0 ? string.Empty : $"  | excludes {string.Join(" / ", names)}";
    }

    private static bool GroupsConflict(HardpointSet[] sets, AirHardpointGroup first, AirHardpointGroup second)
    {
        for (int i = 0; i < first.HardpointIndices.Count; i++)
        {
            for (int j = 0; j < second.HardpointIndices.Count; j++)
            {
                int firstIndex = first.HardpointIndices[i];
                int secondIndex = second.HardpointIndices[j];
                if (ContainsHardpointIndex(sets[firstIndex].precludingHardpointSets, secondIndex)
                    || ContainsHardpointIndex(sets[secondIndex].precludingHardpointSets, firstIndex))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool ContainsHardpointIndex(List<byte>? indices, int value)
    {
        return indices != null && value <= byte.MaxValue && indices.Contains((byte)value);
    }

    private bool ValidateSelectedLoadout(
        AirMissionOption option,
        Loadout loadout,
        Airbase airbase,
        FactionHQ hq,
        out string error)
    {
        if (SelectedPrimaryWeapon == null || GetPrimaryWeaponCount(option) <= 0)
        {
            error = "Select a primary weapon supported by the selected aircraft.";
            return false;
        }

        for (int i = 0; i < loadout.weapons.Count; i++)
        {
            WeaponMount? mount = loadout.weapons[i];
            if (mount == null)
            {
                continue;
            }

            HardpointSet set = option.HardpointSets[i];
            if (!WeaponChecker.MountAllowedHardpoint(mount, set)
                || !WeaponChecker.MountAllowedConflict(set, loadout)
                || !WeaponChecker.MountAllowedHQ(mount, hq)
                || !WeaponChecker.MountAllowedAirbase(mount, airbase)
                || !WeaponChecker.MountAllowedNuclear(mount, set, airbase, null!, hq))
            {
                error = $"{GetMountName(mount)} is not available on {GetHardpointSetName(set, i)} at this airbase.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static float ScoreMount(WeaponMount mount, AirCommandMode mode)
    {
        Loadout loadout = new();
        loadout.weapons.Add(mount);
        return ScoreLoadout(loadout, mode);
    }

    private static string GetHardpointSetName(HardpointSet set, int index)
    {
        return !string.IsNullOrWhiteSpace(set.name) ? set.name : $"Hardpoint {index + 1}";
    }

    private static string GetMountName(WeaponMount mount)
    {
        if (!string.IsNullOrWhiteSpace(mount.mountName)) return mount.mountName;
        WeaponInfo? info = mount.info;
        if (info != null && !string.IsNullOrWhiteSpace(info.weaponName)) return info.weaponName;
        if (!string.IsNullOrWhiteSpace(mount.jsonKey)) return mount.jsonKey;
        return mount.name;
    }
}
