using UnityEngine;
using System.Collections.Generic;
using FMODUnity;

[System.Serializable]
public class MaterialOcclusionProperties
{
    public string materialName = "Default";
    public float occlusionMultiplier = 1.0f;
    [Range(0f, 1f)]
    public float transmissionFactor = 0.1f; // How much sound passes through
}

public class SoundOcclusionManager : MonoBehaviour
{
    [Header("Occlusion Settings")]
    [SerializeField] private float maxOcclusionDistance = 50f;
    [SerializeField] private float updateFrequency = 10f; // Updates per second
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
    private Transform playerTransform;
    private Camera playerCamera;
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
        FindPlayer();
    }
    
    private void Update()
    {
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
    
    private void FindPlayer()
    {
        if (usePlayerTag)
        {
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null)
            {
                playerTransform = player.transform;
                playerCamera = player.GetComponentInChildren<Camera>();
            }
        }
        
        if (playerTransform == null)
        {
            playerCamera = Camera.main;
            if (playerCamera != null)
            {
                playerTransform = playerCamera.transform;
            }
        }
        
        if (playerTransform == null)
        {
            Debug.LogWarning("SoundOcclusionManager: Could not find player. Please ensure player has the correct tag or Camera.main is set.");
        }
    }
    
    public void RegisterSound(OccludableFMODEvent sound)
    {
        if (!registeredSounds.Contains(sound))
        {
            registeredSounds.Add(sound);
        }
    }
    
    public void UnregisterSound(OccludableFMODEvent sound)
    {
        registeredSounds.Remove(sound);
    }
    
    private void UpdateOcclusion()
    {
        if (playerTransform == null)
        {
            FindPlayer();
            return;
        }
        
        Vector3 listenerPosition = playerCamera != null ? playerCamera.transform.position : playerTransform.position;
        
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
        RaycastHit[] hits = Physics.RaycastAll(listenerPosition, direction, distance, occlusionLayers);
        
        float totalOcclusion = 0f;
        int occluderCount = 0;
        
        foreach (RaycastHit hit in hits)
        {
            // Skip if we hit the sound source itself
            if (hit.collider.transform == occludableSound.transform || 
                hit.collider.transform.IsChildOf(occludableSound.transform))
                continue;
            
            // Get material properties
            MaterialOcclusionProperties materialProps = GetMaterialProperties(hit.collider);
            
            // Calculate occlusion based on material and hit angle
            float hitOcclusion = CalculateHitOcclusion(hit, direction, materialProps);
            totalOcclusion += hitOcclusion;
            occluderCount++;
            
            if (showDebugRays)
            {
                Debug.DrawLine(listenerPosition, hit.point, Color.red, 0.1f);
                Debug.DrawLine(hit.point, soundPosition, Color.yellow, 0.1f);
            }
        }
        
        if (showDebugRays && occluderCount == 0)
        {
            Debug.DrawLine(listenerPosition, soundPosition, Color.green, 0.1f);
        }
        
        // Normalize occlusion value and apply
        float normalizedOcclusion = Mathf.Clamp01(totalOcclusion);
        occludableSound.SetOcclusionValue(normalizedOcclusion);
        
        if (showDebugInfo && occluderCount > 0)
        {
            Debug.Log($"Sound: {occludableSound.name}, Occluders: {occluderCount}, Occlusion: {normalizedOcclusion:F2}");
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
        
        // Fallback to renderer material name
        Renderer renderer = collider.GetComponent<Renderer>();
        if (renderer != null && renderer.material != null)
        {
            string materialName = renderer.material.name.Replace(" (Instance)", "");
            if (materialLookup.ContainsKey(materialName))
            {
                return materialLookup[materialName];
            }
        }
        
        // Return default material properties
        return materialLookup.ContainsKey("Default") ? materialLookup["Default"] : materialProperties[0];
    }
    
    private float CalculateHitOcclusion(RaycastHit hit, Vector3 direction, MaterialOcclusionProperties material)
    {
        // Calculate angle factor (perpendicular hits cause more occlusion)
        float angle = Vector3.Angle(direction, hit.normal);
        float angleFactor = Mathf.Cos(Mathf.Deg2Rad * angle);
        
        // Base occlusion modified by material properties and angle
        float hitOcclusion = material.occlusionMultiplier * angleFactor;
        
        // Reduce occlusion based on transmission factor
        hitOcclusion *= (1f - material.transmissionFactor);
        
        return hitOcclusion;
    }
    
    public MaterialOcclusionProperties GetMaterialPropertiesByName(string materialName)
    {
        return materialLookup.ContainsKey(materialName) ? materialLookup[materialName] : materialProperties[0];
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!showDebugRays || playerTransform == null) return;
        
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(playerTransform.position, maxOcclusionDistance);
        
        foreach (var sound in registeredSounds)
        {
            if (sound != null)
            {
                Gizmos.color = Color.Lerp(Color.green, Color.red, sound.CurrentOcclusionValue);
                Gizmos.DrawLine(playerTransform.position, sound.transform.position);
            }
        }
    }
}
