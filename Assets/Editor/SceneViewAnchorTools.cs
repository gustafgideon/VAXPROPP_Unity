#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SceneViewAnchorTools
{
    private static SceneView GetView()
    {
        var sv = SceneView.lastActiveSceneView;
        if (sv == null && SceneView.sceneViews != null && SceneView.sceneViews.Count > 0)
            sv = SceneView.sceneViews[0] as SceneView;
        return sv;
    }

    private static string NextAnchorName()
    {
        var anchors = Object.FindObjectsOfType<SceneViewAnchor>(true);
        int maxIndex = 0;
        foreach (var a in anchors)
        {
            var n = a.gameObject.name;
            // Try to parse trailing number
            int idx;
            if (n.StartsWith("SV Anchor ") && int.TryParse(n.Substring("SV Anchor ".Length), out idx))
                maxIndex = Mathf.Max(maxIndex, idx);
        }
        return $"SV Anchor {maxIndex + 1:00}";
    }

    [MenuItem("GameObject/Scene View/Create Anchor At Scene View", priority = 0)]
    public static void CreateAnchorAtSceneView()
    {
        var sv = GetView();
        if (sv == null) { Debug.LogWarning("No SceneView available."); return; }

        var go = new GameObject(NextAnchorName());
        Undo.RegisterCreatedObjectUndo(go, "Create Scene View Anchor");
        var t = go.transform;
        t.position = sv.pivot;
        t.rotation = sv.rotation;

        var anchor = go.AddComponent<SceneViewAnchor>();
        anchor.DisplayName = go.name;

        // Optional: add a disabled Camera component to store projection settings
        var cam = go.AddComponent<Camera>();
        cam.enabled = false;
        cam.orthographic = sv.orthographic;
        cam.orthographicSize = sv.size;
#if UNITY_2019_1_OR_NEWER
        try
        {
            cam.fieldOfView = sv.cameraSettings.fieldOfView;
        }
        catch { /* Fallback: leave default FOV */ }
#endif

        Selection.activeObject = go;
        Debug.Log($"Created anchor '{go.name}' at current Scene view.");
    }

    [MenuItem("GameObject/Scene View/Create Anchor From Selected Camera", priority = 1)]
    public static void CreateAnchorFromSelectedCamera()
    {
        var cam = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<Camera>() : null;
        if (!cam) { Debug.LogWarning("Select a Camera in the Hierarchy first."); return; }

        var go = new GameObject(NextAnchorName());
        Undo.RegisterCreatedObjectUndo(go, "Create Scene View Anchor From Camera");
        var t = go.transform;
        t.position = cam.transform.position;
        t.rotation = cam.transform.rotation;

        var anchor = go.AddComponent<SceneViewAnchor>();
        anchor.DisplayName = go.name;

        var storeCam = go.AddComponent<Camera>();
        storeCam.enabled = false;
        storeCam.orthographic = cam.orthographic;
        storeCam.orthographicSize = cam.orthographicSize;
        storeCam.fieldOfView = cam.fieldOfView;

        Selection.activeObject = go;
        Debug.Log($"Created anchor '{go.name}' from selected Camera.");
    }

    [MenuItem("Tools/Scene View/Align Scene View To Selected Anchor %#&a", priority = 2050)]
    public static void AlignSceneViewToSelectedAnchor()
    {
        if (!(Selection.activeGameObject && Selection.activeGameObject.TryGetComponent<SceneViewAnchor>(out var anchor)))
        {
            Debug.LogWarning("Select a GameObject with a SceneViewAnchor component.");
            return;
        }
        SceneViewCameraSwitcher.JumpToAnchor(anchor);
    }

    [MenuItem("Tools/Scene View/Align Selected Anchor To Scene View %#&s", priority = 2051)]
    public static void AlignSelectedAnchorToSceneView()
    {
        var sv = GetView();
        if (sv == null) { Debug.LogWarning("No SceneView available."); return; }

        if (!(Selection.activeGameObject && Selection.activeGameObject.TryGetComponent<SceneViewAnchor>(out var anchor)))
        {
            Debug.LogWarning("Select a GameObject with a SceneViewAnchor component.");
            return;
        }

        Undo.RecordObject(anchor.transform, "Align Anchor To Scene View");
        anchor.transform.position = sv.pivot;
        anchor.transform.rotation = sv.rotation;

        if (anchor.MatchCameraIfPresent && anchor.TryGetComponent<Camera>(out var cam))
        {
            Undo.RecordObject(cam, "Align Anchor Camera To Scene View");
            cam.orthographic = sv.orthographic;
            cam.orthographicSize = sv.size;
#if UNITY_2019_1_OR_NEWER
            try
            {
                cam.fieldOfView = sv.cameraSettings.fieldOfView;
            }
            catch { /* ignore */ }
#endif
        }

        Debug.Log($"Aligned anchor '{anchor.gameObject.name}' to current Scene view.");
    }
}
#endif