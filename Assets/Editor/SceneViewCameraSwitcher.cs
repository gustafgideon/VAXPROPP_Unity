#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SceneViewCameraSwitcher
{
    private const string PrefCurrentIndex = "SceneViewCamSwitcher.CurrentIndex";

    private static List<SceneViewAnchor> GetAnchorsSorted()
    {
        // Include inactive anchors; sort by GameObject name so Alt+1..9 is predictable
        return Object.FindObjectsOfType<SceneViewAnchor>(true)
                     .OrderBy(a => a.gameObject.name)
                     .ToList();
    }

    private static SceneView GetView()
    {
        var sv = SceneView.lastActiveSceneView;
        if (sv == null && SceneView.sceneViews != null && SceneView.sceneViews.Count > 0)
            sv = SceneView.sceneViews[0] as SceneView;
        return sv;
    }

    internal static void JumpToAnchor(SceneViewAnchor anchor)
    {
        if (!anchor) return;
        var sv = GetView();
        if (sv == null) { Debug.LogWarning("No SceneView available."); return; }

        bool matchedProjection = false;
        if (anchor.MatchCameraIfPresent && anchor.TryGetComponent<Camera>(out var cam))
        {
            sv.orthographic = cam.orthographic;
            if (cam.orthographic)
            {
                sv.size = cam.orthographicSize;
            }
#if UNITY_2019_1_OR_NEWER
            // Try to apply FOV if supported
            try
            {
                var settings = sv.cameraSettings;
                settings.fieldOfView = cam.fieldOfView;
                sv.cameraSettings = settings;
                matchedProjection = true;
            }
            catch { /* Fallback: not supported in this Unity version */ }
#endif
        }

        // LookAt sets pivot, rotation, and size (size already applied above if ortho)
        sv.LookAt(anchor.transform.position, anchor.transform.rotation, sv.size, sv.orthographic);
        sv.Repaint();

        var label = string.IsNullOrEmpty(anchor.DisplayName) ? anchor.gameObject.name : anchor.DisplayName;
        Debug.Log($"Scene View -> {label}{(matchedProjection ? " (matched projection)" : "")}");
    }

    private static void SetCurrentIndex(int idx) => EditorPrefs.SetInt(PrefCurrentIndex, idx);
    private static int GetCurrentIndex() => EditorPrefs.GetInt(PrefCurrentIndex, 0);

    [MenuItem("Tools/Scene View/Next Anchor &c", priority = 2000)]
    public static void NextAnchor()
    {
        var anchors = GetAnchorsSorted();
        if (anchors.Count == 0) { Debug.LogWarning("No SceneViewAnchor found in the scene."); return; }
        int idx = GetCurrentIndex();
        idx = (idx + 1) % anchors.Count;
        SetCurrentIndex(idx);
        JumpToAnchor(anchors[idx]);
    }

    [MenuItem("Tools/Scene View/Previous Anchor &v", priority = 2001)]
    public static void PrevAnchor()
    {
        var anchors = GetAnchorsSorted();
        if (anchors.Count == 0) { Debug.LogWarning("No SceneViewAnchor found in the scene."); return; }
        int idx = GetCurrentIndex();
        idx = (idx - 1 + anchors.Count) % anchors.Count;
        SetCurrentIndex(idx);
        JumpToAnchor(anchors[idx]);
    }

    // Direct slots 1..9 (Alt+1..9)
    [MenuItem("Tools/Scene View/Go To Anchor/1 &1", priority = 2010)] public static void Go1() => GoToSlot(0);
    [MenuItem("Tools/Scene View/Go To Anchor/2 &2", priority = 2011)] public static void Go2() => GoToSlot(1);
    [MenuItem("Tools/Scene View/Go To Anchor/3 &3", priority = 2012)] public static void Go3() => GoToSlot(2);
    [MenuItem("Tools/Scene View/Go To Anchor/4 &4", priority = 2013)] public static void Go4() => GoToSlot(3);
    [MenuItem("Tools/Scene View/Go To Anchor/5 &5", priority = 2014)] public static void Go5() => GoToSlot(4);
    [MenuItem("Tools/Scene View/Go To Anchor/6 &6", priority = 2015)] public static void Go6() => GoToSlot(5);
    [MenuItem("Tools/Scene View/Go To Anchor/7 &7", priority = 2016)] public static void Go7() => GoToSlot(6);
    [MenuItem("Tools/Scene View/Go To Anchor/8 &8", priority = 2017)] public static void Go8() => GoToSlot(7);
    [MenuItem("Tools/Scene View/Go To Anchor/9 &9", priority = 2018)] public static void Go9() => GoToSlot(8);

    private static void GoToSlot(int slot)
    {
        var anchors = GetAnchorsSorted();
        if (anchors.Count == 0) { Debug.LogWarning("No SceneViewAnchor found in the scene."); return; }
        if (slot < 0 || slot >= anchors.Count) { Debug.LogWarning($"No anchor for slot {slot + 1}. Found {anchors.Count} anchor(s)."); return; }
        SetCurrentIndex(slot);
        JumpToAnchor(anchors[slot]);
    }
}
#endif