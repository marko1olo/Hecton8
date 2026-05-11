#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HectonUIBuilder.cs - One-click NASA-Punk HUD hierarchy generator.
/// Tools / Hecton / Generate HUD Hierarchy
///
/// Generates the full HUD_Master_V2 structure inside the active Canvas.
/// All operations are Undo-registered (Ctrl+Z safe).
/// </summary>
public static class HectonUIBuilder
{
    // ------------------------------------------------------------------
    // COLORS - NASA-Punk palette
    // ------------------------------------------------------------------

    private static readonly Color ColorBrightCyan  = new Color(0.00f, 0.898f, 1.00f, 1.000f); // #00E5FF
    private static readonly Color ColorDarkCyan    = new Color(0.00f, 0.898f, 1.00f, 0.300f); // #00E5FF 30%
    private static readonly Color ColorLightGray   = new Color(0.75f, 0.750f, 0.75f, 1.000f); // #BFBFBF
    private static readonly Color ColorTransparent = new Color(0.00f, 0.000f, 0.00f, 0.000f); // fully clear

    // Gauge definitions: name, top-label text, value text
    private static readonly (string objName, string labelText, string valueText)[] GaugeDefs =
    {
        ("Gauge_O2",  "O2",  "88%"),
        ("Gauge_PWR", "PWR", "91%"),
        ("Gauge_HLT", "HLT", "75%"),
    };

    // ------------------------------------------------------------------
    // MENU ENTRY
    // ------------------------------------------------------------------

    [MenuItem("Tools/Hecton/Generate HUD Hierarchy", priority = 1)]
    private static void GenerateHUD()
    {
        // -- Locate target Canvas --------------------------------------
        Canvas targetCanvas = FindTargetCanvas();
        if (targetCanvas == null)
        {
            EditorUtility.DisplayDialog(
                "Hecton HUD Builder",
                "No Canvas found in the scene.\n\nPlease create or select a Canvas first.",
                "OK");
            return;
        }

        // -- Guard: don't double-generate -----------------------------
        if (targetCanvas.transform.Find("HUD_Master_V2") != null)
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "Hecton HUD Builder",
                "HUD_Master_V2 already exists inside the Canvas.\n\nDelete it and regenerate?",
                "Regenerate", "Cancel");

            if (!overwrite) return;

            GameObject existing = targetCanvas.transform.Find("HUD_Master_V2").gameObject;
            Undo.DestroyObjectImmediate(existing);
        }

        // -- Build -----------------------------------------------------
        BuildHUDMaster(targetCanvas.transform);

        Debug.Log("[HectonUIBuilder] HUD_Master_V2 generated successfully inside: "
                  + targetCanvas.gameObject.name);
    }

    [MenuItem("Tools/Hecton/Generate HUD Hierarchy", validate = true)]
    private static bool ValidateGenerateHUD()
    {
        // Menu item is always available; dialog handles edge cases at runtime
        return true;
    }

    // ------------------------------------------------------------------
    // TOP-LEVEL: HUD_Master_V2
    // ------------------------------------------------------------------

    private static void BuildHUDMaster(Transform canvasTransform)
    {
        // Root - stretch to fill entire canvas
        RectTransform master = CreateRect("HUD_Master_V2", canvasTransform);
        SetAnchorsAndStretch(master,
            anchorMin: Vector2.zero,
            anchorMax: Vector2.one,
            offsetMin: Vector2.zero,
            offsetMax: Vector2.zero);

        // Root must NOT have a visible Image background
        // (no Image component added - canvas group only if needed later)

        BuildLeftGauges(master);
        BuildRightTelemetry(master);

        // Select the new root in the hierarchy for convenience
        Selection.activeGameObject = master.gameObject;
    }

    // ------------------------------------------------------------------
    // LEFT GAUGES PANEL
    // ------------------------------------------------------------------

    private static void BuildLeftGauges(RectTransform parent)
    {
        // -- Panel -----------------------------------------------------
        RectTransform panel = CreateRect("Left_Gauges", parent);

        // Anchor: Bottom-Left, Pivot: 0,0
        SetAnchorPivotAndPos(panel,
            anchorMin : new Vector2(0f, 0f),
            anchorMax : new Vector2(0f, 0f),
            pivot     : new Vector2(0f, 0f),
            anchoredPos: new Vector2(50f, 50f),
            size       : new Vector2(450f, 150f));

        // No background Image on panel

        // -- HorizontalLayoutGroup -------------------------------------
        HorizontalLayoutGroup hlg = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
        Undo.RegisterCreatedObjectUndo(panel.gameObject, "Add HLG");

        hlg.childAlignment         = TextAnchor.MiddleLeft;
        hlg.spacing                = 20f;
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = false;
        hlg.childScaleWidth        = false;
        hlg.childScaleHeight       = false;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        hlg.padding                = new RectOffset(0, 0, 0, 0);

        // -- 3 Gauges --------------------------------------------------
        for (int i = 0; i < GaugeDefs.Length; i++)
        {
            var def = GaugeDefs[i];
            BuildGauge(panel, def.objName, def.labelText, def.valueText);
        }
    }

    // ------------------------------------------------------------------
    // SINGLE GAUGE PREFAB
    // ------------------------------------------------------------------

    private static void BuildGauge(
        RectTransform parent,
        string        gaugeName,
        string        labelText,
        string        valueText)
    {
        // -- Gauge root (120 x 150) ------------------------------------
        RectTransform gauge = CreateRect(gaugeName, parent);
        gauge.sizeDelta = new Vector2(120f, 150f);
        // No background Image

        // -- Label_Top -------------------------------------------------
        RectTransform labelTop = CreateRect("Label_Top", gauge);
        SetAnchorPivotAndPos(labelTop,
            anchorMin   : new Vector2(0.5f, 1f),
            anchorMax   : new Vector2(0.5f, 1f),
            pivot       : new Vector2(0.5f, 1f),
            anchoredPos : Vector2.zero,
            size        : new Vector2(120f, 24f));

        TextMeshProUGUI tmpLabelTop = labelTop.gameObject.AddComponent<TextMeshProUGUI>();
        tmpLabelTop.text            = labelText;
        tmpLabelTop.fontSize        = 18f;
        tmpLabelTop.alignment       = TextAlignmentOptions.Center;
        tmpLabelTop.color           = ColorBrightCyan;

        // -- Ring_BG ---------------------------------------------------
        RectTransform ringBg = CreateRect("Ring_BG", gauge);
        SetAnchorPivotAndPos(ringBg,
            anchorMin   : new Vector2(0.5f, 0.5f),
            anchorMax   : new Vector2(0.5f, 0.5f),
            pivot       : new Vector2(0.5f, 0.5f),
            anchoredPos : Vector2.zero,
            size        : new Vector2(100f, 100f));

        Image imgRingBg   = ringBg.gameObject.AddComponent<Image>();
        imgRingBg.color   = ColorDarkCyan;
        imgRingBg.type    = Image.Type.Simple;

        // -- Ring_Fill -------------------------------------------------
        RectTransform ringFill = CreateRect("Ring_Fill", gauge);
        SetAnchorPivotAndPos(ringFill,
            anchorMin   : new Vector2(0.5f, 0.5f),
            anchorMax   : new Vector2(0.5f, 0.5f),
            pivot       : new Vector2(0.5f, 0.5f),
            anchoredPos : Vector2.zero,
            size        : new Vector2(100f, 100f));

        Image imgRingFill          = ringFill.gameObject.AddComponent<Image>();
        imgRingFill.color          = ColorBrightCyan;
        imgRingFill.type           = Image.Type.Filled;
        imgRingFill.fillMethod     = Image.FillMethod.Radial360;
        imgRingFill.fillOrigin     = (int)Image.Origin360.Top;
        imgRingFill.fillAmount     = 0.88f; // Default ~88%
        imgRingFill.fillClockwise  = true;

        // -- Icon_Top --------------------------------------------------
        RectTransform iconTop = CreateRect("Icon_Top", gauge);
        SetAnchorPivotAndPos(iconTop,
            anchorMin   : new Vector2(0.5f, 0.5f),
            anchorMax   : new Vector2(0.5f, 0.5f),
            pivot       : new Vector2(0.5f, 0.5f),
            anchoredPos : new Vector2(0f, 25f),
            size        : new Vector2(20f, 20f));

        Image imgIconTop   = iconTop.gameObject.AddComponent<Image>();
        imgIconTop.color   = ColorBrightCyan;
        // Sprite left intentionally null - user assigns at build time

        // -- Text_Value ------------------------------------------------
        RectTransform textValue = CreateRect("Text_Value", gauge);
        SetAnchorPivotAndPos(textValue,
            anchorMin   : new Vector2(0.5f, 0.5f),
            anchorMax   : new Vector2(0.5f, 0.5f),
            pivot       : new Vector2(0.5f, 0.5f),
            anchoredPos : Vector2.zero,
            size        : new Vector2(100f, 40f));

        TextMeshProUGUI tmpValue   = textValue.gameObject.AddComponent<TextMeshProUGUI>();
        tmpValue.text              = valueText;
        tmpValue.fontSize          = 28f;
        tmpValue.fontStyle         = FontStyles.Bold;
        tmpValue.alignment         = TextAlignmentOptions.Center;
        tmpValue.color             = ColorBrightCyan;

        // -- Icon_Bot --------------------------------------------------
        RectTransform iconBot = CreateRect("Icon_Bot", gauge);
        SetAnchorPivotAndPos(iconBot,
            anchorMin   : new Vector2(0.5f, 0.5f),
            anchorMax   : new Vector2(0.5f, 0.5f),
            pivot       : new Vector2(0.5f, 0.5f),
            anchoredPos : new Vector2(0f, -25f),
            size        : new Vector2(20f, 20f));

        Image imgIconBot   = iconBot.gameObject.AddComponent<Image>();
        imgIconBot.color   = ColorBrightCyan;
    }

    // ------------------------------------------------------------------
    // RIGHT TELEMETRY PANEL
    // ------------------------------------------------------------------

    private static void BuildRightTelemetry(RectTransform parent)
    {
        // -- Panel -----------------------------------------------------
        RectTransform panel = CreateRect("Right_Telemetry", parent);

        // Anchor: Bottom-Right, Pivot: 1,0
        SetAnchorPivotAndPos(panel,
            anchorMin   : new Vector2(1f, 0f),
            anchorMax   : new Vector2(1f, 0f),
            pivot       : new Vector2(1f, 0f),
            anchoredPos : new Vector2(-50f, 50f),
            size        : new Vector2(400f, 120f));

        // No background Image on panel

        // -- VerticalLayoutGroup ---------------------------------------
        VerticalLayoutGroup vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();

        vlg.childAlignment         = TextAnchor.MiddleRight;
        vlg.spacing                = 5f;
        vlg.childControlWidth      = true;   // Requirement: Control Child Size Width
        vlg.childControlHeight     = false;
        vlg.childScaleWidth        = false;
        vlg.childScaleHeight       = false;
        vlg.childForceExpandWidth  = false;
        vlg.childForceExpandHeight = false;
        vlg.padding                = new RectOffset(0, 0, 0, 0);

        // -- Text_Depth ------------------------------------------------
        RectTransform depthRect = CreateRect("Text_Depth", panel);
        depthRect.sizeDelta      = new Vector2(400f, 46f);

        TextMeshProUGUI tmpDepth = depthRect.gameObject.AddComponent<TextMeshProUGUI>();
        tmpDepth.text            = "DEPTH: 1,428 m";
        tmpDepth.fontSize        = 36f;
        tmpDepth.alignment       = TextAlignmentOptions.Right;
        tmpDepth.color           = ColorBrightCyan;

        // -- Deco_Line -------------------------------------------------
        // Width driven by VLG (Control Child Width = true), explicit height = 4
        RectTransform decoLine  = CreateRect("Deco_Line", panel);
        decoLine.sizeDelta      = new Vector2(300f, 4f); // VLG overrides width

        Image imgDeco   = decoLine.gameObject.AddComponent<Image>();
        imgDeco.color   = ColorBrightCyan;
        // Sprite left null - user assigns a jagged line sprite here

        // -- Text_Pressure ---------------------------------------------
        RectTransform pressureRect = CreateRect("Text_Pressure", panel);
        pressureRect.sizeDelta     = new Vector2(400f, 32f);

        TextMeshProUGUI tmpPressure = pressureRect.gameObject.AddComponent<TextMeshProUGUI>();
        tmpPressure.text            = "PRESSURE: 2.5 atm";
        tmpPressure.fontSize        = 24f;
        tmpPressure.alignment       = TextAlignmentOptions.Right;
        tmpPressure.color           = ColorLightGray;
    }

    // ------------------------------------------------------------------
    // HELPERS
    // ------------------------------------------------------------------

    /// <summary>
    /// Creates a named GameObject with a RectTransform, registers it with Undo,
    /// and parents it correctly. Returns the RectTransform.
    /// This is the single creation point - keeps all build methods clean.
    /// </summary>
    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));

        // Register before parenting so Undo captures the correct state
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");

        // Use Undo-aware parenting
        Undo.SetTransformParent(go.transform, parent, $"Parent {name}");

        // Reset local transform - critical after parenting
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale    = Vector3.one;

        return go.GetComponent<RectTransform>();
    }

    /// <summary>
    /// Sets anchor min/max + stretch offsets (for full-stretch assignments).
    /// </summary>
    private static void SetAnchorsAndStretch(
        RectTransform rt,
        Vector2       anchorMin,
        Vector2       anchorMax,
        Vector2       offsetMin,
        Vector2       offsetMax)
    {
        rt.anchorMin  = anchorMin;
        rt.anchorMax  = anchorMax;
        rt.offsetMin  = offsetMin;
        rt.offsetMax  = offsetMax;
        rt.pivot      = new Vector2(0.5f, 0.5f);
    }

    /// <summary>
    /// Sets anchor, pivot, anchored position, and size delta.
    /// Used for all non-stretch panels and child elements.
    /// </summary>
    private static void SetAnchorPivotAndPos(
        RectTransform rt,
        Vector2       anchorMin,
        Vector2       anchorMax,
        Vector2       pivot,
        Vector2       anchoredPos,
        Vector2       size)
    {
        rt.anchorMin      = anchorMin;
        rt.anchorMax      = anchorMax;
        rt.pivot          = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta      = size;
    }

    /// <summary>
    /// Finds the best Canvas target:
    /// 1. Canvas on the currently selected GameObject.
    /// 2. Canvas that is a parent of the selected object.
    /// 3. Any Canvas in the scene.
    /// Returns null if none found.
    /// </summary>
    private static Canvas FindTargetCanvas()
    {
        // Priority 1: selected object is a Canvas
        if (Selection.activeGameObject != null)
        {
            Canvas selected = Selection.activeGameObject.GetComponent<Canvas>();
            if (selected != null) return selected;

            // Priority 2: selected object is inside a Canvas
            Canvas parentCanvas = Selection.activeGameObject.GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                // Walk up to find the root Canvas (in case of nested canvases)
                Canvas root = parentCanvas;
                while (root.transform.parent != null)
                {
                    Canvas higher = root.transform.parent.GetComponentInParent<Canvas>();
                    if (higher == null) break;
                    root = higher;
                }
                return root;
            }
        }

        // Priority 3: any Canvas in scene
        Canvas fallback = Object.FindAnyObjectByType<Canvas>();
        return fallback;
    }
}
#endif
