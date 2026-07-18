using UnityEngine;

namespace NuclearOptionCommander;

internal static class CommanderUiScale
{
    private const float BaselineScale = 1.5f;

    internal static float DisplayScale => CommanderSettings.UiScale;
    internal static float Scale => DisplayScale / BaselineScale;
    internal static float Width => Screen.width / Scale;
    internal static float Height => Screen.height / Scale;

    internal static Matrix4x4 Begin()
    {
        Matrix4x4 previous = GUI.matrix;
        GUI.matrix = previous * Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));
        return previous;
    }

    internal static void End(Matrix4x4 previous)
    {
        GUI.matrix = previous;
    }

    internal static Vector2 ScreenToGui(Vector2 screenPoint)
    {
        return new Vector2(screenPoint.x / Scale, (Screen.height - screenPoint.y) / Scale);
    }

    internal static Vector2 GuiToScreen(Vector2 guiPoint)
    {
        return new Vector2(guiPoint.x * Scale, Screen.height - guiPoint.y * Scale);
    }

    internal static void Step(int direction)
    {
        float next = Mathf.Round((CommanderSettings.UiScale + direction * 0.25f) * 4f) * 0.25f;
        CommanderSettings.UiScale = Mathf.Clamp(next, 0.5f, 1.5f);
    }
}
