using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace NuclearOptionCommander;

internal static class CommanderVehicleDefinitionDumper
{
    private const int CollectionPreviewLimit = 24;
    private static bool dumped;

    internal static void DumpAllGroundVehicles()
    {
        if (dumped)
        {
            return;
        }

        dumped = true;
        VehicleDefinition[] allDefinitions = Resources.FindObjectsOfTypeAll<VehicleDefinition>();
        List<VehicleDefinition> groundVehicles = new();

        for (int i = 0; i < allDefinitions.Length; i++)
        {
            VehicleDefinition definition = allDefinitions[i];
            if (CommanderGameAccess.IsSpawnableVehicleDefinition(definition))
            {
                groundVehicles.Add(definition);
            }
        }

        groundVehicles.Sort(static (left, right) => string.Compare(
            CommanderGameAccess.GetVehicleLabel(left),
            CommanderGameAccess.GetVehicleLabel(right),
            StringComparison.OrdinalIgnoreCase));

        CommanderPlugin.Log.LogInfo($"Commander ground vehicle dump begin: count={groundVehicles.Count}");
        for (int i = 0; i < groundVehicles.Count; i++)
        {
            DumpVehicleDefinition(i, groundVehicles[i]);
        }

        CommanderPlugin.Log.LogInfo("Commander ground vehicle dump end.");
    }

    private static void DumpVehicleDefinition(int index, VehicleDefinition definition)
    {
        CommanderPlugin.Log.LogInfo(
            $"[Vehicle {index}] name={CommanderGameAccess.GetVehicleLabel(definition)} " +
            $"category={CommanderGameAccess.GetVehicleCategoryLabel(definition)} asset={definition.name}");

        foreach (FieldInfo field in EnumerateInstanceFields(definition.GetType()))
        {
            object? value = ReadField(field, definition);
            CommanderPlugin.Log.LogInfo($"  field {field.DeclaringType?.Name}.{field.Name} ({field.FieldType.Name}) = {FormatValue(value)}");
        }

        foreach (PropertyInfo property in EnumerateReadableProperties(definition.GetType()))
        {
            object? value = ReadProperty(property, definition);
            CommanderPlugin.Log.LogInfo($"  property {property.DeclaringType?.Name}.{property.Name} ({property.PropertyType.Name}) = {FormatValue(value)}");
        }

        DumpPrefabComponents(definition.unitPrefab);
    }

    private static IEnumerable<FieldInfo> EnumerateInstanceFields(Type type)
    {
        for (Type? current = type; current != null && current != typeof(object); current = current.BaseType)
        {
            FieldInfo[] fields = current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            for (int i = 0; i < fields.Length; i++)
            {
                yield return fields[i];
            }
        }
    }

    private static IEnumerable<PropertyInfo> EnumerateReadableProperties(Type type)
    {
        for (Type? current = type; current != null && current != typeof(object); current = current.BaseType)
        {
            PropertyInfo[] properties = current.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (property.CanRead && property.GetIndexParameters().Length == 0)
                {
                    yield return property;
                }
            }
        }
    }

    private static object? ReadField(FieldInfo field, VehicleDefinition definition)
    {
        try
        {
            return field.GetValue(definition);
        }
        catch (Exception exception)
        {
            return $"<read error: {exception.GetType().Name}>";
        }
    }

    private static object? ReadProperty(PropertyInfo property, VehicleDefinition definition)
    {
        try
        {
            return property.GetValue(definition);
        }
        catch (Exception exception)
        {
            return $"<read error: {exception.GetType().Name}>";
        }
    }

    private static void DumpPrefabComponents(GameObject? prefab)
    {
        if (prefab == null)
        {
            CommanderPlugin.Log.LogInfo("  prefab = <none>");
            return;
        }

        Component[] components = prefab.GetComponents<Component>();
        List<string> componentNames = new();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != null)
            {
                componentNames.Add(components[i].GetType().FullName ?? components[i].GetType().Name);
            }
        }

        CommanderPlugin.Log.LogInfo($"  prefab name={prefab.name} active={prefab.activeSelf} componentCount={componentNames.Count}");
        CommanderPlugin.Log.LogInfo($"  prefab components=[{string.Join(", ", componentNames)}]");
    }

    private static string FormatValue(object? value)
    {
        if (value == null)
        {
            return "<null>";
        }

        if (value is string text)
        {
            return $"\"{text}\"";
        }

        Type valueType = value.GetType();
        if (valueType.IsPrimitive || valueType.IsEnum || value is decimal || value is Vector2 || value is Vector3 || value is Vector4 || value is Quaternion || value is Color || value is Rect)
        {
            return value.ToString() ?? valueType.Name;
        }

        if (value is UnityEngine.Object unityObject)
        {
            return $"<{unityObject.GetType().Name} name=\"{unityObject.name}\">";
        }

        if (value is IEnumerable enumerable)
        {
            return FormatCollection(enumerable, valueType);
        }

        return value.ToString() ?? valueType.FullName ?? valueType.Name;
    }

    private static string FormatCollection(IEnumerable collection, Type collectionType)
    {
        List<string> values = new();
        int count = 0;
        foreach (object? entry in collection)
        {
            count++;
            if (values.Count < CollectionPreviewLimit)
            {
                values.Add(FormatValue(entry));
            }
        }

        string suffix = count > CollectionPreviewLimit ? $", ... +{count - CollectionPreviewLimit}" : string.Empty;
        return $"<{collectionType.Name} count={count} [{string.Join(", ", values)}{suffix}]>";
    }
}
