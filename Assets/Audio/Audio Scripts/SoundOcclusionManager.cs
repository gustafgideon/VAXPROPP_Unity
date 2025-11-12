using UnityEngine;
using System.Collections.Generic;
using FMODUnity;
using FMOD.Studio;

[System.Serializable]
public class MaterialOcclusionSetting
{
    public string materialName = "Default";

    [Range(0f, 1f)]
    [Tooltip("Volume multiplier when occluded by this material (0 = silent, 1 = unchanged). Ignored if fullyOcclude = true.")]
    public float volumeMultiplier = 0.7f;

    [Range(0f, 1f)]
    [Tooltip("Lowpass parameter value (0 = no lowpass, 1 = full lowpass). Ignored if fullyOcclude = true.")]
    public float lowpassValue = 0.5f;

    [Tooltip("If true, volume forced to 0 and lowpass forced to 1 when this material occludes.")]
    public bool fullyOcclude = false;
}

public class SoundOcclusionManager : MonoBehaviour
{
    public static SoundOcclusionManager Instance { get; private set; }

    [Header("Listener Settings")]
    [SerializeField] private bool usePlayerTag = true;
    [SerializeField] private string playerTag = "Player";

    [Header("Occlusion Ray")]
    [SerializeField] private LayerMask occlusionLayers = ~0;
    [SerializeField] private float maxCheckDistance = 60f;
    [SerializeField] private float rayStartOffset = 0.05f;
    [SerializeField] private bool singleHit = true;

    [Header("Update")]
    [SerializeField, Tooltip("Occlusion recalculations per second.")]
    private float updatesPerSecond = 10f;

    [SerializeField, Tooltip("If true, the manager will scan for new emitters automatically.")]
    private bool autoScanForEmitters = true;

    [SerializeField, Tooltip("Seconds between automatic full scans if autoScanForEmitters is enabled.")]
    private float emitterRescanInterval = 5f;

    [Header("FMOD Parameter Names")]
    [SerializeField] private string volumeParameterName = "Volume";
    [SerializeField] private string lowpassParameterName = "Lowpass";

    [Header("Material Settings")]
    [SerializeField] private MaterialOcclusionSetting[] materialSettings = {
        new MaterialOcclusionSetting { materialName = "Default", volumeMultiplier = 0.85f, lowpassValue = 0.3f, fullyOcclude = false },
        new MaterialOcclusionSetting { materialName = "Wood", volumeMultiplier = 0.75f, lowpassValue = 0.5f, fullyOcclude = false },
        new MaterialOcclusionSetting { materialName = "Concrete", volumeMultiplier = 0.6f, lowpassValue = 0.65f, fullyOcclude = false },
        new MaterialOcclusionSetting { materialName = "Glass", volumeMultiplier = 0.9f, lowpassValue = 0.2f, fullyOcclude = false },
        new MaterialOcclusionSetting { materialName = "Metal", volumeMultiplier = 0.5f, lowpassValue = 0.7f, fullyOcclude = false }
    };

    [Header("Lookup Mode")]
    [SerializeField] private bool useOcclusionMaterialComponent = true;
    [SerializeField] private bool fallbackToColliderTag = true;

    [Header("Debug")]
    [SerializeField] private bool debugRays = false;
    [SerializeField] private bool debugLog = false;

    private Transform listenerTransform;
    private readonly List<StudioEventEmitter> emitters = new List<StudioEventEmitter>();
    private readonly Dictionary<string, MaterialOcclusionSetting> materialLookup = new Dictionary<string, MaterialOcclusionSetting>();

    private float updateTimer;
    private float scanTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildMaterialLookup();
    }

    private void Start()
    {
        FindListener();
        FullScanEmitters();
    }

    private void Update()
    {
        if (listenerTransform == null)
        {
            FindListener();
            if (listenerTransform == null) return;
        }

        // Periodic occlusion update
        updateTimer += Time.deltaTime;
        if (updateTimer >= (1f / Mathf.Max(0.01f, updatesPerSecond)))
        {
            updateTimer = 0f;
            UpdateOcclusions();
        }

        // Periodic emitter scanning
        if (autoScanForEmitters)
        {
            scanTimer += Time.deltaTime;
            if (scanTimer >= emitterRescanInterval)
            {
                scanTimer = 0f;
                FullScanEmitters();
            }
        }
    }

    private void BuildMaterialLookup()
    {
        materialLookup.Clear();
        foreach (var m in materialSettings)
        {
            if (!materialLookup.ContainsKey(m.materialName))
                materialLookup.Add(m.materialName, m);
        }

        if (!materialLookup.ContainsKey("Default"))
        {
            materialLookup.Add("Default", new MaterialOcclusionSetting
            {
                materialName = "Default",
                volumeMultiplier = 0.85f,
                lowpassValue = 0.3f,
                fullyOcclude = false
            });
        }
    }

    private void FindListener()
    {
        listenerTransform = null;

        if (usePlayerTag)
        {
            var player = GameObject.FindGameObjectWithTag(playerTag);
            if (player) listenerTransform = player.transform;
        }

        if (listenerTransform == null && Camera.main != null)
            listenerTransform = Camera.main.transform;

        if (listenerTransform == null)
        {
            var anyCam = FindObjectOfType<Camera>();
            if (anyCam) listenerTransform = anyCam.transform;
        }

        if (debugLog && listenerTransform != null)
            Debug.Log($"[SoundOcclusionManager] Listener set to {listenerTransform.name}");
    }

    public void RegisterEmitter(StudioEventEmitter emitter)
    {
        if (emitter == null || emitters.Contains(emitter)) return;
        emitters.Add(emitter);
        if (debugLog) Debug.Log($"[SoundOcclusionManager] Registered emitter {emitter.name}");
    }

    public void UnregisterEmitter(StudioEventEmitter emitter)
    {
        if (emitters.Remove(emitter) && debugLog)
            Debug.Log($"[SoundOcclusionManager] Unregistered emitter {emitter.name}");
    }

    private void FullScanEmitters()
    {
        var found = FindObjectsOfType<StudioEventEmitter>(true);
        int added = 0;
        foreach (var e in found)
        {
            if (!emitters.Contains(e))
            {
                emitters.Add(e);
                added++;
            }
        }
        if (debugLog)
            Debug.Log($"[SoundOcclusionManager] Full scan: {found.Length} emitters found, {added} newly added.");
        CleanupNulls();
    }

    private void CleanupNulls()
    {
        for (int i = emitters.Count - 1; i >= 0; i--)
        {
            if (emitters[i] == null)
                emitters.RemoveAt(i);
        }
    }

    private void UpdateOcclusions()
    {
        if (listenerTransform == null) return;

        Vector3 listenerPos = listenerTransform.position;
        CleanupNulls();

        foreach (var emitter in emitters)
        {
            if (emitter == null) continue;

            // Only process playing instances
            if (!emitter.IsPlaying())
            {
                // Optional: Could reset parameters if desired
                continue;
            }

            ApplyOcclusionToEmitter(emitter, listenerPos);
        }
    }

    private void ApplyOcclusionToEmitter(StudioEventEmitter emitter, Vector3 listenerPos)
    {
        Vector3 soundPos = emitter.transform.position;
        float distance = Vector3.Distance(listenerPos, soundPos);

        if (distance > maxCheckDistance)
        {
            // Beyond range: treat as clear
            SetEmitterParameters(emitter, 1f, 0f);
            return;
        }

        Vector3 dir = (soundPos - listenerPos).normalized;
        Vector3 origin = listenerPos + dir * rayStartOffset;
        float rayLength = Mathf.Max(0f, distance - rayStartOffset);

        // Single or multi-hit
        if (singleHit)
        {
            if (Physics.Raycast(origin, dir, out RaycastHit hit, rayLength, occlusionLayers))
            {
                ProcessHit(emitter, hit, origin, soundPos);
            }
            else
            {
                ClearOcclusion(emitter, listenerPos, soundPos);
            }
        }
        else
        {
            var hits = Physics.RaycastAll(origin, dir, rayLength, occlusionLayers);
            if (hits.Length == 0)
            {
                ClearOcclusion(emitter, listenerPos, soundPos);
            }
            else
            {
                // Use closest valid hit
                RaycastHit? chosen = null;
                float bestDist = float.MaxValue;
                foreach (var h in hits)
                {
                    float hd = (h.point - origin).sqrMagnitude;
                    if (hd < bestDist)
                    {
                        bestDist = hd;
                        chosen = h;
                    }
                }
                if (chosen.HasValue)
                    ProcessHit(emitter, chosen.Value, origin, soundPos);
                else
                    ClearOcclusion(emitter, listenerPos, soundPos);
            }
        }
    }

    private void ProcessHit(StudioEventEmitter emitter, RaycastHit hit, Vector3 origin, Vector3 soundPos)
    {
        if (debugRays)
        {
            Debug.DrawLine(origin, hit.point, Color.red, 0.05f);
            Debug.DrawLine(hit.point, soundPos, Color.yellow, 0.05f);
        }

        var setting = ResolveMaterial(hit.collider);

        if (setting.fullyOcclude)
        {
            SetEmitterParameters(emitter, 0f, 1f);
            if (debugLog)
                Debug.Log($"[SoundOcclusionManager] Fully occluding {emitter.name} via {hit.collider.name} ({setting.materialName})");
            return;
        }

        SetEmitterParameters(emitter, setting.volumeMultiplier, setting.lowpassValue);

        if (debugLog)
        {
            Debug.Log($"[SoundOcclusionManager] Occluding {emitter.name} | Mat:{setting.materialName} Vol:{setting.volumeMultiplier:F2} LP:{setting.lowpassValue:F2}");
        }
    }

    private void ClearOcclusion(StudioEventEmitter emitter, Vector3 listenerPos, Vector3 soundPos)
    {
        SetEmitterParameters(emitter, 1f, 0f);
        if (debugRays)
            Debug.DrawLine(listenerPos, soundPos, Color.green, 0.05f);
    }

    private MaterialOcclusionSetting ResolveMaterial(Collider col)
    {
        if (useOcclusionMaterialComponent)
        {
            var comp = col.GetComponent<OcclusionMaterial>();
            if (comp != null && materialLookup.TryGetValue(comp.MaterialName, out var s1))
                return s1;
        }

        if (fallbackToColliderTag && materialLookup.TryGetValue(col.tag, out var s2))
            return s2;

        return materialLookup["Default"];
    }

    private void SetEmitterParameters(StudioEventEmitter emitter, float volumeValue, float lowpassValue)
    {
        var inst = emitter.EventInstance;
        volumeValue = Mathf.Clamp01(volumeValue);
        lowpassValue = Mathf.Clamp01(lowpassValue);
        inst.setParameterByName(volumeParameterName, volumeValue);
        inst.setParameterByName(lowpassParameterName, lowpassValue);
    }

    public MaterialOcclusionSetting GetMaterialSetting(string name)
    {
        return materialLookup.TryGetValue(name, out var m) ? m : materialLookup["Default"];
    }

    private void OnDrawGizmosSelected()
    {
        if (listenerTransform == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(listenerTransform.position, maxCheckDistance);
    }
}