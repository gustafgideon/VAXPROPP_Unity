using UnityEngine;

/// <summary>
/// Attach to trigger colliders. When an object with the configured tag enters the trigger,
/// this will call VisionManager.SetVisionByName(visionName).
/// </summary>
[RequireComponent(typeof(Collider))]
public class VisionTrigger : MonoBehaviour
{
    [Tooltip("Name of the vision preset to switch to (must match a VisionManager vision.name).")]
    public string visionName;

    [Tooltip("Tag on the object that will trigger the vision change (usually the Player).")]
    public string triggerTag = "Player";

    [Tooltip("If true, vision is restored to default (VisionManager default) on exit.")]
    public bool restoreOnExit = true;

    [Tooltip("Optional: the vision to restore to on exit. Leave empty to use VisionManager default.")]
    public string restoreVisionName;

    void Reset()
    {
        // ensure collider is trigger
        if (TryGetComponent(out Collider c))
            c.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!string.IsNullOrEmpty(triggerTag) && !other.CompareTag(triggerTag)) return;
        if (string.IsNullOrEmpty(visionName)) return;

        if (VisionManager.Instance != null)
            VisionManager.Instance.SetVisionByName(visionName);
        else
            Debug.LogWarning("VisionTrigger: No VisionManager.Instance found in scene.");
    }

    void OnTriggerExit(Collider other)
    {
        if (!string.IsNullOrEmpty(triggerTag) && !other.CompareTag(triggerTag)) return;
        if (!restoreOnExit) return;

        if (VisionManager.Instance == null)
        {
            Debug.LogWarning("VisionTrigger: No VisionManager.Instance found in scene.");
            return;
        }

        if (!string.IsNullOrEmpty(restoreVisionName))
            VisionManager.Instance.SetVisionByName(restoreVisionName);
        else
            // go back to VisionManager default (index 0 or defaultVisionName)
            VisionManager.Instance.SetVision(0);
    }
}
