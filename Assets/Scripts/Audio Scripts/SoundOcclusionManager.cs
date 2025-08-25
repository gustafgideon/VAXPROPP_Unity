using UnityEngine;
using System.Collections.Generic;
using FMODUnity;

[System.Serializable]
public class MaterialOcclusionProperties
{
    public string materialName = "Default";
    public float occlusionMultiplier = 1.0f;
    [Range(0f, 1f)]
    public float transmissionFactor = 0.1f;
}

public class SoundOcclusionManager : MonoBehaviour
{
    [Header("Occlusion Settings")]
    [SerializeField] private float maxOcclusionDistance = 50f;
    [SerializeField] private float updateFrequency = 10f;
    [SerializeField] private LayerMask occlusionLayers = -1;
    [SerializeField] private bool usePlayerTag = true;
    [SerializeField] private string playerTag = "Player";
    
    [Header("Material Properties")]
    [SerializeField] private MaterialOcclusionProperties[] materialProperties = {
        new MaterialOcclusionProperties { materialName = "Default", occlusionMultiplier = 1.0f, transmissionFactor = 0.1f },
        new MaterialOcclusionProperties { materialName = "Wood", occlusionMultiplier = 0.8f, transmissionFactor = 0.2f },
        new MaterialOcclusionProperties { materialName = "Concrete", occlusionMultiplier = 1.2f, transmissionFactor = 0.05f },
        new MaterialOcclusionProperties { materialName = "Glass", occlusionMultiplier = 0.3f, transmissionFactor = 0.7f },
        new MaterialOcclusionProperties { materialName = "Metal", occlusionMultiplier = 1.5f, transmissionFactor = 0.02f }
    };
    
    [Header("Debug")]
    [SerializeField] private bool showDebugRays = false;
    [SerializeField] private bool showDebugInfo = false;
    
    private static SoundOcclusionManager instance;
    private Transform listenerTransform;
    private List<OccludableFMODEvent> registeredSounds = new List<OccludableFMODEvent>();
    private Dictionary<string, MaterialOcclusionProperties> materialLookup = new Dictionary<string, MaterialOcclusionProperties>();
    private float updateTimer;
    
    public static SoundOcclusionManager Instance => instance;
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeMaterialLookup();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        FindListener();
        
        if (showDebugInfo)
        {
            Debug.Log($"SoundOcclusionManager started. Listener found: {(listenerTransform != null ? listenerTransform.name : "NULL")}");
            Debug.Log($"Occlusion Layers: {occlusionLayers.value}");
        }
    }
    
    private void Update()
    {
        // Try to find listener if we don't have one
        if (listenerTransform == null)
        {
            FindListener();
        }
        
        updateTimer += Time.deltaTime;
        if (updateTimer >= 1f / updateFrequency)
        {
            updateTimer = 0f;
            UpdateOcclusion();
        }
    }
    
    private void InitializeMaterialLookup()
    {
        materialLookup.Clear();
        foreach (var material in materialProperties)
        {
            if (!materialLookup.ContainsKey(material.materialName))
            {
                materialLookup.Add(material.materialName, material);
            }
        }
    }
    
    private void FindListener()
    {
        listenerTransform = null;
        
        // Try to find by player tag first
        if (usePlayerTag)
        {
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null)
            {
                listenerTransform = player.transform;
                if (showDebugInfo)
                {
                    Debug.Log($"Found player by tag: {player.name}");
                }
            }
        }
        
        // Fallback to Camera.main
        if (listenerTransform == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                listenerTransform = mainCamera.transform;
                if (showDebugInfo)
                {
                    Debug.Log($"Using main camera as listener: {mainCamera.name}");
                }
            }
        }
        
        // Last resort: find any camera
        if (listenerTransform == null)
        {
            Camera anyCamera = FindObjectOfType<Camera>();
            if (anyCamera != null)
            {
                listenerTransform = anyCamera.transform;
                if (showDebugInfo)
                {
                    Debug.Log($"Using any camera as listener: {anyCamera.name}");
                }
            }
        }
        
        if (listenerTransform == null && showDebugInfo)
        {
            Debug.LogWarning("SoundOcclusionManager: Could not find listener position!");
        }
    }
    
    public void RegisterSound(OccludableFMODEvent sound)
    {
        if (!registeredSounds.Contains(sound))
        {
            registeredSounds.Add(sound);
            if (showDebugInfo)
            {
                Debug.Log($"Registered sound: {sound.name}");
            }
        }
    }
    
    public void UnregisterSound(OccludableFMODEvent sound)
    {
        if (registeredSounds.Remove(sound) && showDebugInfo)
        {
            Debug.Log($"Unregistered sound: {sound.name}");
        }
    }
    
    private void UpdateOcclusion()
    {
        if (listenerTransform == null)
        {
            return;
        }
        
        Vector3 listenerPosition = listenerTransform.position;
        
        for (int i = registeredSounds.Count - 1; i >= 0; i--)
        {
            if (registeredSounds[i] == null)
            {
                registeredSounds.RemoveAt(i);
                continue;
            }
            
            CalculateOcclusion(registeredSounds[i], listenerPosition);
        }
    }
    
    private void CalculateOcclusion(OccludableFMODEvent occludableSound, Vector3 listenerPosition)
    {
        Vector3 soundPosition = occludableSound.transform.position;
        float distance = Vector3.Distance(listenerPosition, soundPosition);
        
        if (distance > maxOcclusionDistance)
        {
            occludableSound.SetOcclusionValue(0f);
            return;
        }
        
        Vector3 direction = (soundPosition - listenerPosition).normalized;
        
        // Use a slightly offset start position to avoid hitting the listener's collider
        Vector3 rayStart = listenerPosition + direction * 0.1f;
        float rayDistance = distance - 0.1f;
        
        if (showDebugInfo)
        {
            Debug.Log($"Checking occlusion for {occludableSound.name} - Distance: {distance:F2}");
        }
        
        RaycastHit[] hits = Physics.RaycastAll(rayStart, direction, rayDistance, occlusionLayers);
        
        float totalOcclusion = 0f;
        int occluderCount = 0;
        
        foreach (RaycastHit hit in hits)
        {
            // Skip if we hit the sound source itself or its children
            if (hit.collider.transform == occludableSound.transform || 
                hit.collider.transform.IsChildOf(occludableSound.transform) ||
                occludableSound.transform.IsChildOf(hit.collider.transform))
            {
                continue;
            }
            
            // Get material properties
            MaterialOcclusionProperties materialProps = GetMaterialProperties(hit.collider);
            
            // Calculate occlusion based on material
            float hitOcclusion = materialProps.occlusionMultiplier * (1f - materialProps.transmissionFactor);
            totalOcclusion += hitOcclusion;
            occluderCount++;
            
            if (showDebugInfo)
            {
                Debug.Log($"Hit occluder: {hit.collider.name}, Material: {materialProps.materialName}, Occlusion: {hitOcclusion:F2}");
            }
            
            if (showDebugRays)
            {
                Debug.DrawLine(rayStart, hit.point, Color.red, 0.1f);
                Debug.DrawLine(hit.point, soundPosition, Color.yellow, 0.1f);
            }
        }
        
        if (showDebugRays && occluderCount == 0)
        {
            Debug.DrawLine(listenerPosition, soundPosition, Color.green, 0.1f);
        }
        
        // Clamp and apply occlusion
        float normalizedOcclusion = Mathf.Clamp01(totalOcclusion);
        occludableSound.SetOcclusionValue(normalizedOcclusion);
        
        if (showDebugInfo)
        {
            Debug.Log($"Final occlusion for {occludableSound.name}: {normalizedOcclusion:F2} (Occluders: {occluderCount})");
        }
    }
    
    private MaterialOcclusionProperties GetMaterialProperties(Collider collider)
    {
        // Try to get material from OcclusionMaterial component first
        OcclusionMaterial occMaterial = collider.GetComponent<OcclusionMaterial>();
        if (occMaterial != null && materialLookup.ContainsKey(occMaterial.MaterialName))
        {
            return materialLookup[occMaterial.MaterialName];
        }
        
        // Return default material properties
        return materialLookup.ContainsKey("Default") ? materialLookup["Default"] : materialProperties[0];
    }
    
    public MaterialOcclusionProperties GetMaterialPropertiesByName(string materialName)
    {
        return materialLookup.ContainsKey(materialName) ? materialLookup[materialName] : materialProperties[0];
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!showDebugRays || listenerTransform == null) return;
        
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(listenerTransform.position, maxOcclusionDistance);
        
        foreach (var sound in registeredSounds)
        {
            if (sound != null)
            {
                Gizmos.color = Color.Lerp(Color.green, Color.red, sound.CurrentOcclusionValue);
                Gizmos.DrawLine(listenerTransform.position, sound.transform.position);
            }
        }
    }
}