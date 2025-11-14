using UnityEngine;

/// <summary>
/// Tag any Transform with this to use it as a Scene View jump anchor.
/// Optionally add a disabled Camera to this GameObject to store projection settings
/// (orthographic/perspective and size/FOV) that Scene View can copy when jumping.
/// </summary>
public class SceneViewAnchor : MonoBehaviour
{
    [Tooltip("Optional label shown in menus/logs.")]
    public string DisplayName;

    [Tooltip("If a Camera component exists on this object, copy its orthographic/perspective and size/FOV to the Scene view.")]
    public bool MatchCameraIfPresent = true;
}