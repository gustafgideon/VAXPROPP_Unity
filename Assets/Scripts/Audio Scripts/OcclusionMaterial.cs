using UnityEngine;

public class OcclusionMaterial : MonoBehaviour
{
    [Header("Material Occlusion Properties")]
    [SerializeField] private string materialName = "Default";
    [Tooltip("Higher values cause more occlusion")]
    [Range(0f, 3f)]
    [SerializeField] private float occlusionMultiplier = 1f;
    [Tooltip("How much sound passes through (0 = blocks all, 1 = blocks none)")]
    [Range(0f, 1f)]
    [SerializeField] private float transmissionFactor = 0.1f;
    
    [Header("Visual Feedback")]
    [SerializeField] private bool showMaterialInfo = false;
    
    public string MaterialName => materialName;
    public float OcclusionMultiplier => occlusionMultiplier;
    public float TransmissionFactor => transmissionFactor;
    
    private void Start()
    {
        if (showMaterialInfo)
        {
            Debug.Log($"{gameObject.name} - Material: {materialName}, Occlusion: {occlusionMultiplier}, Transmission: {transmissionFactor}");
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (showMaterialInfo)
        {
            Gizmos.color = Color.Lerp(Color.green, Color.red, occlusionMultiplier / 3f);
            
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
            }
        }
    }
}