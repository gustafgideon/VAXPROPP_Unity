using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

[System.Serializable]
public class SimpleMaterialSetting
{
    public string materialName = "Default";

    // 0 = clear (loud), 1 = fully occluded (silent) if your FMOD curve is set that way.
    [Range(0f, 1f)]
    public float volume = 0f;

    // 0 = no lowpass, 1 = strong lowpass.
    [Range(0f, 1f)]
    public float lowpass = 0f;
}

[DisallowMultipleComponent]
public class SoundOcclusionManager : MonoBehaviour
{
    public static SoundOcclusionManager Instance { get; private set; }
    
    
    [Header("Raycast")]
    [SerializeField] private LayerMask occlusionLayers = ~0;
    [SerializeField] private float maxCheckDistance = 60f;
    private bool usePlayerTag = true;
    [SerializeField] private string playerTag = "Player";
    [SerializeField, Tooltip("Occlusion recalculations per second")]
    private float updatesPerSecond = 10f;
    [SerializeField] private bool debugDraw = false;

    [Header("Material Settings")]
    [SerializeField]
    private SimpleMaterialSetting[] materials =
    {
        new SimpleMaterialSetting { materialName = "Default",  volume = 0f,   lowpass = 0f },
        new SimpleMaterialSetting { materialName = "Wood",     volume = 0.5f, lowpass = 0.5f },
        new SimpleMaterialSetting { materialName = "Concrete", volume = 0.7f, lowpass = 0.65f },
        new SimpleMaterialSetting { materialName = "Glass",    volume = 0.2f, lowpass = 0.2f },
        new SimpleMaterialSetting { materialName = "Metal",    volume = 0.8f, lowpass = 0.7f },
    };

    private readonly Dictionary<string, SimpleMaterialSetting> _lookup =
        new Dictionary<string, SimpleMaterialSetting>();

    private readonly List<StudioEventEmitter> _emitters = new List<StudioEventEmitter>();

    private class EmitterParams { public float v; public float lp; public bool initialized; }
    private readonly Dictionary<StudioEventEmitter, EmitterParams> _smoothed =
        new Dictionary<StudioEventEmitter, EmitterParams>();

    private Transform _listener;
    private float _updateTimer;
    private float _rescanTimer;

    private const float RayStartOffset = 0.05f;
    private const float RescanInterval = 2f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildLookup();
    }

    private void Start()
    {
        ResolveListener();
        RescanEmitters();
    }

    private void Update()
    {
        if (_listener == null)
        {
            ResolveListener();
            if (_listener == null) return;
        }

        _updateTimer += Time.deltaTime;
        if (_updateTimer >= (1f / Mathf.Max(0.01f, updatesPerSecond)))
        {
            float dt = _updateTimer;
            _updateTimer = 0f;
            UpdateOcclusionForEmitters(dt);
        }

        _rescanTimer += Time.deltaTime;
        if (_rescanTimer >= RescanInterval)
        {
            _rescanTimer = 0f;
            RescanEmitters();
        }
    }

    private void ResolveListener()
    {
        
        // 2. Player tag search
        if (usePlayerTag)
        {
            var player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null)
            {
                _listener = player.transform;
                return;
            }
        }

        // 3. Fallback to main camera
        if (Camera.main != null)
        {
            _listener = Camera.main.transform;
            return;
        }

        // 4. Any camera
        var anyCam = FindObjectOfType<Camera>();
        if (anyCam != null) _listener = anyCam.transform;
    }

    private void BuildLookup()
    {
        _lookup.Clear();
        foreach (var m in materials)
        {
            if (string.IsNullOrWhiteSpace(m.materialName)) continue;
            if (_lookup.ContainsKey(m.materialName)) continue;
            _lookup.Add(m.materialName, m);
        }

        if (!_lookup.ContainsKey("Default"))
        {
            _lookup.Add("Default", new SimpleMaterialSetting
            {
                materialName = "Default",
                volume = 0f,
                lowpass = 0f
            });
        }
    }

    private void RescanEmitters()
    {
        _emitters.Clear();
        var found = FindObjectsOfType<StudioEventEmitter>(true);
        _emitters.AddRange(found);
        CleanupNulls();
    }

    private void CleanupNulls()
    {
        for (int i = _emitters.Count - 1; i >= 0; i--)
        {
            if (_emitters[i] == null)
            {
                _smoothed.Remove(_emitters[i]);
                _emitters.RemoveAt(i);
            }
        }

        var toRemove = new List<StudioEventEmitter>();
        foreach (var kv in _smoothed)
        {
            if (kv.Key == null || !_emitters.Contains(kv.Key)) toRemove.Add(kv.Key);
        }
        foreach (var e in toRemove) _smoothed.Remove(e);
    }

    private void UpdateOcclusionForEmitters(float dt)
    {
        if (_listener == null) return;

        Vector3 listenerPos = _listener.position;
        CleanupNulls();

        foreach (var emitter in _emitters)
        {
            if (emitter == null || !emitter.IsPlaying()) continue;
            ApplyOcclusion(emitter, listenerPos, dt);
        }
    }

    private void ApplyOcclusion(StudioEventEmitter emitter, Vector3 listenerPos, float dt)
    {
        Vector3 soundPos = emitter.transform.position;
        float distance = Vector3.Distance(listenerPos, soundPos);

        float targetV;
        float targetLP;

        if (distance > maxCheckDistance)
        {
            targetV = 0f;
            targetLP = 0f;
            if (debugDraw) Debug.DrawLine(listenerPos, soundPos, Color.gray, 0.05f);
        }
        else
        {
            Vector3 dir = (soundPos - listenerPos).normalized;
            Vector3 origin = listenerPos + dir * RayStartOffset;
            float rayLength = Mathf.Max(0f, distance - RayStartOffset);

            if (Physics.Raycast(origin, dir, out RaycastHit hit, rayLength, occlusionLayers, QueryTriggerInteraction.UseGlobal))
            {
                var comp = hit.collider.GetComponent<OcclusionMaterial>();
                SimpleMaterialSetting s = GetSetting(comp != null ? comp.MaterialName : "Default");
                targetV = s.volume;
                targetLP = s.lowpass;

                if (debugDraw)
                {
                    Debug.DrawLine(origin, hit.point, Color.red, 0.05f);
                    Debug.DrawLine(hit.point, soundPos, Color.yellow, 0.05f);
                }
            }
            else
            {
                targetV = 0f;
                targetLP = 0f;
                if (debugDraw) Debug.DrawLine(listenerPos, soundPos, Color.green, 0.05f);
            }
        }
    }

    private SimpleMaterialSetting GetSetting(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return _lookup["Default"];
        return _lookup.TryGetValue(name, out var s) ? s : _lookup["Default"];
    }

    

#if UNITY_EDITOR
    private void OnValidate()
    {
        BuildLookup();
    }
#endif
}