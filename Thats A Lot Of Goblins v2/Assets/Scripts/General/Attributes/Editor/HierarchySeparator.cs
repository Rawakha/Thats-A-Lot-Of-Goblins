#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Unity.Hierarchy;
using Unity.Hierarchy.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// Put this script in Assets/Editor. Name separator objects "--- Header".
[InitializeOnLoad]
public static class HierarchySeparator
{
    public const string Prefix = "---";
    private static readonly Color Background = new Color(0.14f, 0.14f, 0.14f, 1f);
    private static readonly Dictionary<HierarchyViewItem, SeparatorRow> Rows = new();

    static HierarchySeparator()
    {
        HierarchyWindow.BindViewItem += OnBindViewItem;
        HierarchyWindow.UnbindViewItem += OnUnbindViewItem;
        EditorApplication.hierarchyChanged += QueueRefresh;
        EditorApplication.delayCall += RefreshOpenWindows;
        AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
    }

    private static void QueueRefresh()
    {
        EditorApplication.delayCall -= RefreshOpenWindows;
        EditorApplication.delayCall += RefreshOpenWindows;
    }

    private static void RefreshOpenWindows()
    {
        foreach (HierarchyWindow window in Resources.FindObjectsOfTypeAll<HierarchyWindow>())
        {
            if (window.View == null)
                continue;

            window.View.Query<HierarchyViewItem>().ForEach(item => OnBindViewItem(window, window.View, item));
        }
    }

    private static void OnBindViewItem(HierarchyWindow window, HierarchyView view, HierarchyViewItem item)
    {
        RestoreRow(item);

        if (item.Handler is not HierarchyGameObjectHandler handler)
            return;

        GameObject obj = handler.GetGameObject(item.Node);
        if (obj == null || !obj.name.StartsWith(Prefix, StringComparison.Ordinal))
            return;

        Rows.Add(item, new SeparatorRow(item, obj.name.Substring(Prefix.Length).Trim()));
    }

    private static void OnUnbindViewItem(HierarchyWindow window, HierarchyView view, HierarchyViewItem item)
    {
        RestoreRow(item);
    }

    private static void RestoreRow(HierarchyViewItem item)
    {
        if (!Rows.TryGetValue(item, out SeparatorRow row))
            return;

        row.Restore();
        Rows.Remove(item);
    }

    private static void Cleanup()
    {
        HierarchyWindow.BindViewItem -= OnBindViewItem;
        HierarchyWindow.UnbindViewItem -= OnUnbindViewItem;
        EditorApplication.hierarchyChanged -= QueueRefresh;
        EditorApplication.delayCall -= RefreshOpenWindows;

        foreach (SeparatorRow row in Rows.Values)
            row.Restore();

        Rows.Clear();
    }

    private sealed class SeparatorRow
    {
        private readonly HierarchyViewItem item;
        private readonly VisualElement container;
        private readonly Label title;
        private readonly StyleColor background;
        private readonly StyleColor nameColor;
        private readonly StyleEnum<DisplayStyle> iconDisplay;
        private readonly StyleEnum<DisplayStyle> overlayDisplay;

        public SeparatorRow(HierarchyViewItem item, string text)
        {
            this.item = item;
            container = item.RowContainer;
            background = container.style.backgroundColor;
            nameColor = item.Name.style.color;
            iconDisplay = item.Icon.style.display;
            overlayDisplay = item.OverlayIcon.style.display;

            // Draw the title across the whole row, independent of indentation.
            // Keep Unity's original name label for selection and renaming.
            title = new Label(text) { pickingMode = PickingMode.Ignore };
            title.style.position = Position.Absolute;
            title.style.left = 0f;
            title.style.right = 0f;
            title.style.top = 0f;
            title.style.bottom = 0f;
            title.style.marginLeft = 0f;
            title.style.marginRight = 0f;
            title.style.paddingLeft = 0f;
            title.style.paddingRight = 0f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.color = Color.white;
            title.style.overflow = Overflow.Hidden;
            container.Add(title);

            container.style.backgroundColor = Background;
            item.Name.style.color = Color.clear;
            item.Icon.style.display = DisplayStyle.None;
            item.OverlayIcon.style.display = DisplayStyle.None;
            container.RegisterCallback<FocusInEvent>(OnFocusIn);
            container.RegisterCallback<FocusOutEvent>(OnFocusOut);
        }

        private void OnFocusIn(FocusInEvent evt)
        {
            if (evt.target is not VisualElement target || (target is not TextField && target.GetFirstAncestorOfType<TextField>() == null))
                return;

            title.style.display = DisplayStyle.None;
            item.Name.style.color = nameColor;
        }

        private void OnFocusOut(FocusOutEvent evt)
        {
            title.style.display = DisplayStyle.Flex;
            item.Name.style.color = Color.clear;
        }

        public void Restore()
        {
            container.UnregisterCallback<FocusInEvent>(OnFocusIn);
            container.UnregisterCallback<FocusOutEvent>(OnFocusOut);
            title.RemoveFromHierarchy();
            container.style.backgroundColor = background;
            item.Name.style.color = nameColor;
            item.Icon.style.display = iconDisplay;
            item.OverlayIcon.style.display = overlayDisplay;
        }
    }
}
#endif
