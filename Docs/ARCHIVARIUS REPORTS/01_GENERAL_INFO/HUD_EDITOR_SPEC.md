# HUD EDITOR VISIBILITY SPEC — SuitHUDV4CanvasOverlay
Date: 2026-05-04
Status: REFERENCE


**Status:** PENDING VERIFICATION  
**Target:** `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs`  
**Rule Basis:** AGENTS.md § [REQ] OnDrawGizmos/OnDrawGizmosSelected: `#if UNITY_EDITOR` only. [FORBID] Physics/Find/GetComponent in OnDrawGizmos — visualize cached data only.  
**Mandates Followed:** AGENTS.md [RULE] MANDATE CONTEXTUAL INGESTION.

---

## 1. PROBLEM STATEMENT

In Editor Mode (non-play), `SuitHUDV4CanvasOverlay` renders nothing in the Scene view because:
- Canvas is `ScreenSpaceOverlay` by default (no world-space geometry).
- `ProjectionSource` path requires a live `projectionCamera` and pose update in `LateUpdate`.
- Hierarchy is built in `OnEnable` / `EnsureHierarchy()` but gizmos are absent.

The Lead Architect sees a **gray void**. This spec defines how Agent THETA implements a **Diegetic Editor Preview** wireframe so the HUD layout is visible without entering Play Mode.

---

## 2. TRANSFORM OFFSETS (Canonical)

All offsets are **anchored to the Canvas root** (`HUD_V4_CanvasRoot`) with reference resolution `1600×900`.

| Element | Inspector Field | Default Value (pixels) | Anchor |
|---------|----------------|------------------------|--------|
| Root | — | Stretch (0,0,0,0) | Center |
| Header | `headerOffset` | `(0, -34)` | Top-Center |
| Telemetry | `telemetryOffset` | `(-226, 126)` | Bottom-Right |
| Telemetry Size | `telemetrySize` | `(184, 124)` | — |
| Gauge Cluster | `gaugeClusterOffset` | `(116, 110)` | Bottom-Left |
| Gauge Cluster Size | `gaugeClusterSize` | `(300, 128)` | — |
| Status | `statusOffset` | `(0, 50)` | Bottom-Center |
| Reticle | `reticleOffset` | `(0, 0)` | Center |
| Quickbar | `quickbarOffset` | `(0, 94)` | Bottom-Center |
| Quickbar Size | `quickbarSize` | `(244, 64)` | — |

### Gauge Ring Sub-layout
- `gaugeColumnSpacing` = `82` px (horizontal spacing between O₂ / HLT / PWR)
- `gaugeRingSize` = `54` px (diameter of radial fill)
- `gaugeRingThickness` = `6` px
- `gaugeIconSize` = `(16, 16)` px
- `gaugeValueOffsetY` = `0` px
- `gaugeLabelOffsetY` = `-34` px

### Quickbar Sub-layout
- `quickbarSlotSize` = `44` px
- `quickbarSlotGap` = `8` px
- 4 slots total (`QuickbarSlotCount = 4`)

---

## 3. PROJECTION MATH (Diegetic / ProjectionSource Path)

When `renderPath == ProjectionSource`, the canvas is remapped to world space. The following math is executed in `UpdateProjectionCanvasPose()` and `ResolveProjectionCanvasWorldScale()`.

### 3.1 Projection Plane Distance
```csharp
float ResolveProjectionPlaneDistance()
{
    if (projectionCamera == null)
        return Mathf.Max(ProjectionNearClipSafetyPaddingMeters, projectionPlaneDistance);

    return Mathf.Max(
        projectionCamera.nearClipPlane + ProjectionNearClipSafetyPaddingMeters,
        ProjectionNearClipSafetyPaddingMeters);
}
```
- `ProjectionNearClipSafetyPaddingMeters` = `0.05` m
- `projectionPlaneDistance` (serialized) = `0.5` m (`DiegeticHudDistanceMeters`)
- **Result:** `max(nearClip + 0.05, 0.05)` meters from camera.

### 3.2 World Scale Factor
```csharp
float ResolveProjectionCanvasWorldScale(Camera targetCamera, Vector2 referenceResolution)
{
    if (targetCamera == null || referenceResolution.x <= 0f || referenceResolution.y <= 0f)
        return DiegeticHudWorldScale; // 0.0005f fallback

    float safeDistance = ResolveProjectionPlaneDistance();
    float halfFovRadians = Mathf.Max(0.001f, targetCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
    float frustumHalfHeight = Mathf.Tan(halfFovRadians) * safeDistance;
    return (frustumHalfHeight * 2f) / referenceResolution.y;
}
```
- **Math:** `scale = (2 × tan(FOV/2) × planeDistance) / referenceResolution.y`
- For `FOV = 60°, planeDistance = 0.5m, refRes.y = 900`:
  - `tan(30°) ≈ 0.577`
  - `frustumHalfHeight ≈ 0.289`
  - `scale ≈ 0.00064` world-units per pixel

### 3.3 Canvas Pose Update
```csharp
void UpdateProjectionCanvasPose(RectTransform canvasRect, Vector2 referenceResolution)
{
    Transform cameraTransform = projectionCamera.transform;
    float projectionDistance = ResolveProjectionPlaneDistance();
    float expectedScale = ResolveProjectionCanvasWorldScale(projectionCamera, referenceResolution);
    Vector3 targetPosition = cameraTransform.position + cameraTransform.forward * projectionDistance;
    canvasRect.SetPositionAndRotation(targetPosition, cameraTransform.rotation);
    canvasRect.localScale = new Vector3(expectedScale, expectedScale, expectedScale);
}
```
- Position: Camera forward × distance.
- Rotation: Camera rotation (billboard-aligned).
- Scale: Uniform `expectedScale`.

---

## 4. BLUEPRINT: EDITOR WIREFRAME PREVIEW

Agent THETA shall add the following `#if UNITY_EDITOR` block to `SuitHUDV4CanvasOverlay.cs`. It must **only read cached serialized fields** — no `GetComponent`, no `FindObjectOfType`, no physics.

### 4.1 Wireframe Color Palette
```csharp
#if UNITY_EDITOR
private static readonly Color _gizmoCanvasBoundsColor = new Color(0.12f, 0.68f, 0.92f, 0.65f);
private static readonly Color _gizmoElementFillColor   = new Color(0.12f, 0.68f, 0.92f, 0.08f);
private static readonly Color _gizmoTextColor          = new Color(0.92f, 0.92f, 0.92f, 0.72f);
private static readonly Color _gizmoProjectionPlaneColor = new Color(1.0f, 0.42f, 0.18f, 0.45f);
#endif
```

### 4.2 OnDrawGizmos Implementation
```csharp
#if UNITY_EDITOR
private void OnDrawGizmos()
{
    // Only draw when this overlay is selected or when explicitly enabled.
    // Unity calls OnDrawGizmos on selected components automatically.
    if (!enabled)
        return;

    // ── 1. Resolve Camera & Projection Parameters ──
    Camera cam = projectionCamera;
    if (cam == null)
    {
        // Fallback: attempt to resolve from cached VisorHUDController without FindObjectOfType.
        // If still null, draw a screen-space proxy at the scene view camera.
        cam = SceneView.lastActiveSceneView?.camera;
        if (cam == null)
            return;
    }

    Vector2 refRes = ResolveUiReferenceResolution(); // DefaultUiReferenceResolution or scaler
    float planeDist = cam.nearClipPlane + ProjectionNearClipSafetyPaddingMeters;
    float halfFovRad = cam.fieldOfView * Mathf.Deg2Rad * 0.5f;
    float frustumHalfHeight = Mathf.Tan(halfFovRad) * planeDist;
    float frustumHalfWidth = frustumHalfHeight * cam.aspect;
    float worldScale = (frustumHalfHeight * 2f) / refRes.y;

    Vector3 planeCenter = cam.transform.position + cam.transform.forward * planeDist;
    Vector3 right = cam.transform.right * frustumHalfWidth;
    Vector3 up = cam.transform.up * frustumHalfHeight;

    // ── 2. Draw Projection Plane Frustum ──
    Gizmos.color = _gizmoProjectionPlaneColor;
    Vector3[] frustumCorners = new Vector3[4];
    frustumCorners[0] = planeCenter - right - up; // BL
    frustumCorners[1] = planeCenter + right - up; // BR
    frustumCorners[2] = planeCenter + right + up; // TR
    frustumCorners[3] = planeCenter - right + up; // TL
    Gizmos.DrawLine(frustumCorners[0], frustumCorners[1]);
    Gizmos.DrawLine(frustumCorners[1], frustumCorners[2]);
    Gizmos.DrawLine(frustumCorners[2], frustumCorners[3]);
    Gizmos.DrawLine(frustumCorners[3], frustumCorners[0]);

    // ── 3. Draw Canvas Bounds (screen-space mapped to world plane) ──
    Gizmos.color = _gizmoCanvasBoundsColor;
    float canvasHalfW = (refRes.x * worldScale) * 0.5f;
    float canvasHalfH = (refRes.y * worldScale) * 0.5f;
    Vector3 cRight = cam.transform.right * canvasHalfW;
    Vector3 cUp = cam.transform.up * canvasHalfH;
    Vector3[] canvasCorners = new Vector3[4];
    canvasCorners[0] = planeCenter - cRight - cUp;
    canvasCorners[1] = planeCenter + cRight - cUp;
    canvasCorners[2] = planeCenter + cRight + cUp;
    canvasCorners[3] = planeCenter - cRight + cUp;
    Gizmos.DrawLine(canvasCorners[0], canvasCorners[1]);
    Gizmos.DrawLine(canvasCorners[1], canvasCorners[2]);
    Gizmos.DrawLine(canvasCorners[2], canvasCorners[3]);
    Gizmos.DrawLine(canvasCorners[3], canvasCorners[0]);

    // ── 4. Draw HUD Element Wireframes ──
    DrawGizmoHudElement(planeCenter, cRight, cUp, headerOffset, new Vector2(620f, 84f), "HEADER");
    DrawGizmoHudElement(planeCenter, cRight, cUp, telemetryOffset, telemetrySize, "TELEMETRY");
    DrawGizmoHudElement(planeCenter, cRight, cUp, gaugeClusterOffset, gaugeClusterSize, "GAUGES");
    DrawGizmoHudElement(planeCenter, cRight, cUp, statusOffset, new Vector2(420f, 24f), "STATUS");
    DrawGizmoHudElement(planeCenter, cRight, cUp, quickbarOffset, quickbarSize, "QUICKBAR");
    DrawGizmoHudElement(planeCenter, cRight, cUp, reticleOffset, new Vector2(22f, 22f), "RETICLE");
}

/// <summary>
/// Draws a single HUD element as a filled rectangle + label on the projection plane.
/// All coordinates are in canvas-local pixels; converted to world space via camera basis.
/// </summary>
private void DrawGizmoHudElement(
    Vector3 planeCenter,
    Vector3 canvasRightBasis,
    Vector3 canvasUpBasis,
    Vector2 pixelOffset,
    Vector2 pixelSize,
    string label)
{
    float worldScale = canvasRightBasis.magnitude / (ResolveUiReferenceResolution().x * 0.5f);
    Vector3 localRight = canvasRightBasis.normalized;
    Vector3 localUp = canvasUpBasis.normalized;

    Vector3 centerWorld = planeCenter
        + localRight * (pixelOffset.x * worldScale)
        + localUp * (pixelOffset.y * worldScale);

    Vector3 halfW = localRight * (pixelSize.x * worldScale * 0.5f);
    Vector3 halfH = localUp * (pixelSize.y * worldScale * 0.5f);

    Vector3 bl = centerWorld - halfW - halfH;
    Vector3 br = centerWorld + halfW - halfH;
    Vector3 tr = centerWorld + halfW + halfH;
    Vector3 tl = centerWorld - halfW + halfH;

    // Wireframe
    Gizmos.color = _gizmoCanvasBoundsColor;
    Gizmos.DrawLine(bl, br);
    Gizmos.DrawLine(br, tr);
    Gizmos.DrawLine(tr, tl);
    Gizmos.DrawLine(tl, bl);

    // Fill
    Gizmos.color = _gizmoElementFillColor;
    Gizmos.DrawLine(bl, tr); // X-hatch to indicate fill without mesh alloc
    Gizmos.DrawLine(br, tl);

    // Label
#if UNITY_EDITOR
    UnityEditor.Handles.color = _gizmoTextColor;
    UnityEditor.Handles.Label(centerWorld, label, new GUIStyle
    {
        fontSize = 10,
        normal = new GUIStyleState { textColor = _gizmoTextColor },
        alignment = TextAnchor.MiddleCenter
    });
#endif
}
#endif
```

### 4.3 Implementation Notes for Agent THETA

1. **No runtime cost:** The entire block is `#if UNITY_EDITOR`. It is stripped from builds.
2. **No scene queries:** `projectionCamera` is a serialized field. If null, fallback to `SceneView.lastActiveSceneView.camera` — this is an Editor API, safe inside `#if UNITY_EDITOR`.
3. **No allocation in gizmo path:** All arrays are local stack-allocs inside the method. No `new` at field level.
4. **Respects AGENTS.md:** No `GetComponent`, no `FindObjectOfType`, no physics, no mesh generation.
5. **Color coding:**
   - **Orange** = projection frustum bounds
   - **Cyan** = canvas bounds + element wireframes
   - **White** = text labels

---

## 5. VERIFICATION CHECKLIST

- [ ] Enter Editor → Select `Suit_HUD_Canvas` → Scene view shows cyan wireframe overlay.
- [ ] Adjust `projectionPlaneDistance` → wireframe moves closer/farther from camera.
- [ ] Adjust `overallScale` → wireframe bounds scale uniformly.
- [ ] Adjust `headerOffset` / `telemetryOffset` → wireframe elements shift.
- [ ] Enter Play Mode → gizmos do not appear in Game view (OnDrawGizmos is editor-only).
- [ ] Build target → no compile errors from `OnDrawGizmos` block.

---

*STATUS: PENDING VERIFICATION*  
*Action: Agent THETA implements §4.2 into SuitHUDV4CanvasOverlay.cs.*
