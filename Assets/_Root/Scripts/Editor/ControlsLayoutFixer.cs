using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls Panel Layout Fixer — v6
///
/// Fixes the index-shifting bug: all children are snapshotted into lists
/// BEFORE any reparenting occurs, so moving objects mid-loop can't skip entries.
///
/// SOURCE hierarchy:
///   Content
///     └── Texts
///           └── Movement (group)
///                 └── Move Forward (label, TMP_Text)
///                       └── input (TMP_InputField)
///                             └── Text Area
///                 └── Move Backward
///                       └── input
///           └── Combat
///           └── Accessories
///
/// RESULT:
///   Content
///     └── Texts   (hidden)
///     └── Rows    (new)
///           └── Header_Movement
///           └── Row_Move Forward
///                 └── LabelCell (50%)
///                 └── InputCell (50%)
///           ...
/// </summary>
public class ControlsLayoutFixer : EditorWindow
{
    [MenuItem("Tools/Fix Controls Panel Layout")]
    public static void ShowWindow()
    {
        var w = GetWindow<ControlsLayoutFixer>("Controls Layout Fixer");
        w.minSize = new Vector2(360, 500);
        w.Show();
    }

    private GameObject contentObject;
    private GameObject textsContainer;

    private float headerRowHeight = 48f;
    private float rowHeight       = 52f;
    private float rowSpacing      = 2f;
    private float groupSpacing    = 20f;
    private int   padLeft         = 20;
    private int   padRight        = 20;
    private int   padTop          = 16;
    private int   padBottom       = 16;

    private void OnEnable()  { }
    private void OnDisable() { }

    private void OnGUI()
    {
        if (this == null) return;

        GUILayout.Label("Controls Panel Layout Fixer  v6", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "Snapshots all children before processing — no index-shift bugs.\n" +
            "NON-DESTRUCTIVE: Texts hidden, never deleted.\n" +
            "Ctrl+Z to undo everything.",
            MessageType.Info
        );

        EditorGUILayout.Space();
        contentObject  = ObjField("Content Object",  contentObject);
        textsContainer = ObjField("Texts Container", textsContainer);

        EditorGUILayout.Space();
        GUILayout.Label("Layout Settings", EditorStyles.boldLabel);
        headerRowHeight = EditorGUILayout.FloatField("Header Row Height", headerRowHeight);
        rowHeight       = EditorGUILayout.FloatField("Row Height",        rowHeight);
        rowSpacing      = EditorGUILayout.FloatField("Row Spacing",       rowSpacing);
        groupSpacing    = EditorGUILayout.FloatField("Group Spacing",     groupSpacing);

        EditorGUILayout.Space();
        GUILayout.Label("Content Padding", EditorStyles.boldLabel);
        padLeft   = EditorGUILayout.IntField("Left",   padLeft);
        padRight  = EditorGUILayout.IntField("Right",  padRight);
        padTop    = EditorGUILayout.IntField("Top",    padTop);
        padBottom = EditorGUILayout.IntField("Bottom", padBottom);

        EditorGUILayout.Space();

        bool ready = contentObject != null && textsContainer != null;
        EditorGUI.BeginDisabledGroup(!ready);
        if (GUILayout.Button("Build / Rebuild Rows", GUILayout.Height(42)))
            BuildRows();
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space();
        EditorGUI.BeginDisabledGroup(contentObject == null);
        if (GUILayout.Button("Reset All Scales Only", GUILayout.Height(28)))
            ResetAllScales(contentObject);
        EditorGUI.EndDisabledGroup();

        if (!ready)
            EditorGUILayout.HelpBox("Assign Content and Texts to enable Build.", MessageType.Warning);
    }

    // -------------------------------------------------------------------------

    private void BuildRows()
    {
        if (contentObject == null || textsContainer == null) return;

        Undo.RegisterFullObjectHierarchyUndo(contentObject,  "Build Control Rows");
        Undo.RegisterFullObjectHierarchyUndo(textsContainer, "Build Control Rows");

        ResetAllScales(contentObject);

        // Remove any previously generated Rows container
        Transform existing = contentObject.transform.Find("Rows");
        if (existing != null)
            Undo.DestroyObjectImmediate(existing.gameObject);

        // ---- SNAPSHOT everything BEFORE touching the hierarchy ----
        // groups[i] = (groupTransform, List of (labelTransform, inputTransform))
        var groups = new List<(Transform group, List<(Transform label, Transform input)> rows)>();

        for (int g = 0; g < textsContainer.transform.childCount; g++)
        {
            Transform group = textsContainer.transform.GetChild(g);
            var rows = new List<(Transform, Transform)>();

            for (int i = 0; i < group.childCount; i++)
            {
                Transform labelContainer = group.GetChild(i);
                Transform inputContainer = FindInputContainer(labelContainer);
                rows.Add((labelContainer, inputContainer));
            }

            groups.Add((group, rows));
        }
        // ---- End snapshot ----

        // Create Rows container
        GameObject rowsGO = new GameObject("Rows");
        Undo.RegisterCreatedObjectUndo(rowsGO, "Rows Container");
        rowsGO.transform.SetParent(contentObject.transform, false);
        rowsGO.transform.localScale = Vector3.one;
        SetStretchTopRect(rowsGO);

        VerticalLayoutGroup rowsVLG = rowsGO.AddComponent<VerticalLayoutGroup>();
        rowsVLG.childControlWidth      = true;
        rowsVLG.childControlHeight     = true;
        rowsVLG.childForceExpandWidth  = true;
        rowsVLG.childForceExpandHeight = false;
        rowsVLG.childAlignment         = TextAnchor.UpperLeft;
        rowsVLG.spacing                = rowSpacing;
        rowsVLG.padding                = new RectOffset(padLeft, padRight, padTop, padBottom);

        ContentSizeFitter rowsCSF = rowsGO.AddComponent<ContentSizeFitter>();
        rowsCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        rowsCSF.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        int rowCount = 0;
        bool firstGroup = true;

        // Now process the snapshot safely
        foreach (var (group, rows) in groups)
        {
            float topPad = firstGroup ? 0f : groupSpacing;
            GameObject header = MakeHeaderRow(group.name, topPad);
            header.transform.SetParent(rowsGO.transform, false);
            firstGroup = false;

            foreach (var (labelTf, inputTf) in rows)
            {
                GameObject row = MakeDataRow(labelTf, inputTf);
                row.transform.SetParent(rowsGO.transform, false);
                rowCount++;
            }
        }

        // Hide original Texts container
        Undo.RecordObject(textsContainer, "Hide Texts");
        textsContainer.SetActive(false);

        SetupContent();
        EditorUtility.SetDirty(contentObject);

        Debug.Log($"[ControlsLayoutFixer] Done — {rowCount} rows built.");
        EditorUtility.DisplayDialog("Done",
            $"{rowCount} rows created.\n\nTexts hidden (not deleted).\nCtrl+Z to undo.", "OK");
    }

    // -------------------------------------------------------------------------

    private GameObject MakeHeaderRow(string title, float topPadding)
    {
        GameObject row = new GameObject($"Header_{title}");
        Undo.RegisterCreatedObjectUndo(row, "Header Row");
        row.transform.localScale = Vector3.one;
        SetStretchTopRect(row);

        LayoutElement le = row.AddComponent<LayoutElement>();
        le.minHeight       = headerRowHeight + topPadding;
        le.preferredHeight = headerRowHeight + topPadding;
        le.flexibleHeight  = 0f;
        le.flexibleWidth   = 1f;

        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = true;
        hlg.childForceExpandHeight = true;
        hlg.padding                = new RectOffset(0, 0, (int)topPadding, 0);
        hlg.childAlignment         = TextAnchor.MiddleCenter;

        GameObject labelGO = new GameObject(title);
        Undo.RegisterCreatedObjectUndo(labelGO, "Header Label");
        labelGO.transform.SetParent(row.transform, false);
        labelGO.transform.localScale = Vector3.one;
        labelGO.AddComponent<RectTransform>();
        TextMeshProUGUI tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = title;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        tmp.fontSize  = 32f;

        return row;
    }

    private GameObject MakeDataRow(Transform labelTf, Transform inputTf)
    {
        string rowName = labelTf != null ? labelTf.name : "Unknown";

        GameObject row = new GameObject($"Row_{rowName}");
        Undo.RegisterCreatedObjectUndo(row, "Data Row");
        row.transform.localScale = Vector3.one;
        SetStretchTopRect(row);

        LayoutElement rowLE = row.AddComponent<LayoutElement>();
        rowLE.minHeight       = rowHeight;
        rowLE.preferredHeight = rowHeight;
        rowLE.flexibleHeight  = 0f;
        rowLE.flexibleWidth   = 1f;

        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = true;
        hlg.childForceExpandHeight = true;
        hlg.spacing                = 0f;
        hlg.padding                = new RectOffset(0, 0, 0, 0);
        hlg.childAlignment         = TextAnchor.MiddleLeft;

        // LEFT CELL — label (50%)
        GameObject labelCell = MakeCell("LabelCell", row.transform);
        if (labelTf != null)
        {
            // Temporarily detach input so it doesn't travel with label
            if (inputTf != null)
            {
                Undo.SetTransformParent(inputTf, row.transform, "Detach Input Temporarily");
                inputTf.SetParent(row.transform, false);
            }

            Undo.SetTransformParent(labelTf, labelCell.transform, "Reparent Label");
            labelTf.SetParent(labelCell.transform, false);
            labelTf.localScale = Vector3.one;
            FillRect(labelTf.GetComponent<RectTransform>());

            TMP_Text tmp = labelTf.GetComponent<TMP_Text>();
            if (tmp != null) tmp.alignment = TextAlignmentOptions.MidlineLeft;
        }

        // RIGHT CELL — input (50%)
        GameObject inputCell = MakeCell("InputCell", row.transform);
        if (inputTf != null)
        {
            Undo.SetTransformParent(inputTf, inputCell.transform, "Reparent Input");
            inputTf.SetParent(inputCell.transform, false);
            inputTf.localScale = Vector3.one;
            FillRect(inputTf.GetComponent<RectTransform>());

            TMP_InputField tmpIF = inputTf.GetComponent<TMP_InputField>();
            if (tmpIF != null && tmpIF.textComponent != null)
                tmpIF.textComponent.alignment = TextAlignmentOptions.Center;
        }
        else
        {
            Debug.LogWarning($"[ControlsLayoutFixer] No input found for '{rowName}'");
        }

        return row;
    }

    private static GameObject MakeCell(string cellName, Transform parent)
    {
        GameObject cell = new GameObject(cellName);
        Undo.RegisterCreatedObjectUndo(cell, cellName);
        cell.transform.SetParent(parent, false);
        cell.transform.localScale = Vector3.one;

        RectTransform rt = cell.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        VerticalLayoutGroup vlg = cell.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = true;
        vlg.padding                = new RectOffset(4, 4, 4, 4);

        LayoutElement le = cell.AddComponent<LayoutElement>();
        le.flexibleWidth  = 1f;
        le.flexibleHeight = 1f;

        return cell;
    }

    private void SetupContent()
    {
        VerticalLayoutGroup vlg = GetOrAdd<VerticalLayoutGroup>(contentObject);
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment         = TextAnchor.UpperLeft;
        vlg.spacing                = 0f;
        vlg.padding                = new RectOffset(0, 0, 0, 0);

        ContentSizeFitter csf = GetOrAdd<ContentSizeFitter>(contentObject);
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform rt = contentObject.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin  = new Vector2(0f, 1f);
            rt.anchorMax  = new Vector2(1f, 1f);
            rt.pivot      = new Vector2(0.5f, 1f);
            rt.localScale = Vector3.one;
            rt.offsetMin  = new Vector2(0f, rt.offsetMin.y);
            rt.offsetMax  = new Vector2(0f, 0f);
        }
    }

    // -------------------------------------------------------------------------

    private static Transform FindInputContainer(Transform labelContainer)
    {
        for (int i = 0; i < labelContainer.childCount; i++)
        {
            Transform child = labelContainer.GetChild(i);
            if (child.GetComponent<TMP_InputField>() != null) return child;
            if (child.GetComponent<InputField>()     != null) return child;
            if (child.name.ToLower().Contains("input"))       return child;
        }
        return null;
    }

    private static void SetStretchTopRect(GameObject go)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void FillRect(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void ResetAllScales(GameObject root)
    {
        if (root == null) return;
        Undo.RecordObject(root.transform, "Reset Scale");
        root.transform.localScale = Vector3.one;
        foreach (Transform c in root.transform) ResetAllScales(c.gameObject);
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        if (c == null) c = Undo.AddComponent<T>(go);
        return c;
    }

    private static GameObject ObjField(string label, GameObject current)
        => (GameObject)EditorGUILayout.ObjectField(label, current, typeof(GameObject), true);
}