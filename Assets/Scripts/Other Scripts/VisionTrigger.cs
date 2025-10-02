using UnityEngine;

[RequireComponent(typeof(Collider))]
public class VisionTrigger : MonoBehaviour
{
    [Header("Vision Settings")]
    public string visionPresetName = "Pixelated";
    
    [Header("Trigger Settings")]
    public string triggerTag = "Player";
    public bool restoreOnExit = true;
    public string restorePresetName = "Normal";

    void OnValidate()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(triggerTag)) return;
        if (VisionManager.Instance == null) return;
        
        VisionManager.Instance.SetVision(visionPresetName);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(triggerTag)) return;
        if (!restoreOnExit) return;
        if (VisionManager.Instance == null) return;
        
        VisionManager.Instance.SetVision(restorePresetName);
    }
}