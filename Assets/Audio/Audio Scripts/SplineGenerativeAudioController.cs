using UnityEngine;
using FMODUnity;
using FMOD.Studio;

[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
public class SplineGenerativeAudioController : MonoBehaviour
{
    [Header("Environment tagging")]
    [Tooltip("Tag applied to rock colliders in the river.")]
    public string rockTag = "Rock_Water";
    [Tooltip("Tag applied to an invisible trigger volume that marks a waterfall area.")]
    public string waterfallTag = "Waterfall";

    [Header("FMOD Events (must be 3D and looping)")]
    [SerializeField] private EventReference streamEvent;       // Base stream/river loop
    [SerializeField] private EventReference rockSplashEvent;   // Rock splash overlay
    [SerializeField] private EventReference waterfallEvent;    // Waterfall loop

    [Header("Volumes")]
    [Range(0f, 1f)] public float baseRiverVolume = 0.8f;
    [Range(0f, 1f)] public float riverVolumeInWaterfall = 0.3f;
    [Range(0f, 1f)] public float maxRockVolume = 0.7f;
    [Range(0f, 1f)] public float maxWaterfallVolume = 1.0f;

    [Header("Rock behavior")]
    [Tooltip("Minimum rock splash level whenever inside rock colliders, even if stationary.")]
    [Range(0f, 1f)] public float rockPresenceFloor = 0.18f;
    [Tooltip("How many overlapping rocks are needed to reach full density (1.0).")]
    [Min(1f)] public float rockCountForMax = 3f;

    [Header("Constant Flow (no player speed)")]
    [Tooltip("River flow speed used to drive splash intensity.")]
    public float constantFlowSpeed = 2.5f;
    [Tooltip("Flow speed at which rock splash reaches maximum intensity.")]
    public float speedForMaxRockSplash = 6f;
    [Tooltip("How quickly volumes move toward their targets (units per second).")]
    public float fadeSpeed = 3f;

    [Header("Detection Area")]
    [Tooltip("WORLD-SPACE area of the detection sphere (A = 4πr²). This drives the SphereCollider radius.")]
    [Range(0.1f, 5000f)]
    public float detectionArea = 50f;

    [Header("Optional filtering")]
    [Tooltip("Limit trigger checks to these layers (leave as Everything to include all).")]
    public LayerMask featureLayers = ~0;

    [Header("Gizmos")]
    public bool drawGizmos = true;
    public bool drawWhenNotSelected = false;
    public Color gizmoFillColor = new Color(0f, 1f, 1f, 0.08f);
    public Color gizmoWireColor = new Color(0f, 0.8f, 1f, 0.9f);
    public Color gizmoWaterfallWireColor = new Color(0.1f, 0.4f, 1f, 1f);
    public Color gizmoRocksWireColor = new Color(0.1f, 1f, 0.6f, 1f);

    // State
    private int rockCount;
    private int waterfallCount;

    private Rigidbody rb;
    private SphereCollider sphere;

    // FMOD instances
    private EventInstance riverInst;
    private EventInstance rockInst;
    private EventInstance waterfallInst;

    // Cached volumes
    private float riverVol;
    private float rockVol;
    private float waterfallVol;

    private bool riverCreated;
    private bool rockCreated;
    private bool waterfallCreated;

    void Awake()
    {
        sphere = GetComponent<SphereCollider>();
        sphere.isTrigger = true;

        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Initialize collider radius from detectionArea
        ApplyAreaToCollider();

        CreateInstances();
    }

    void OnEnable()
    {
        // Prime counts if starting inside volumes
        var hits = Physics.OverlapSphere(transform.position, GetWorldRadius(), featureLayers, QueryTriggerInteraction.Collide);
        rockCount = 0;
        waterfallCount = 0;
        foreach (var h in hits)
        {
            if (IsFeature(h, rockTag)) rockCount++;
            if (IsFeature(h, waterfallTag)) waterfallCount++;
        }

        // Initial volumes
        riverVol = baseRiverVolume;
        rockVol = (rockCount > 0) ? maxRockVolume * Mathf.Clamp01(rockPresenceFloor) * RockDensityFactor() : 0f;
        waterfallVol = (waterfallCount > 0) ? maxWaterfallVolume : 0f;

        SetVolume(riverInst, riverVol);
        SetVolume(rockInst, rockVol);
        SetVolume(waterfallInst, waterfallVol);

        EnsureStartedIfAudible(riverInst, riverVol);
        EnsureStartedIfAudible(rockInst, rockVol);
        EnsureStartedIfAudible(waterfallInst, waterfallVol);
    }

    void Update()
    {
        // Keep FMOD 3D attributes in sync
        Update3DAttributes();

        bool inWaterfall = waterfallCount > 0;

        float targetRiver = inWaterfall ? riverVolumeInWaterfall : baseRiverVolume;

        // Only constant flow is used
        float flowFactor = Mathf.Clamp01(constantFlowSpeed / Mathf.Max(0.001f, speedForMaxRockSplash));
        float density = RockDensityFactor();

        // Presence floor keeps splashes audible when inside rocks
        float presenceFloor = (rockCount > 0) ? Mathf.Clamp01(rockPresenceFloor) : 0f;

        // Choose the higher of the floor or the flow-based intensity, then scale by density and max level
        float targetRocks = maxRockVolume * density * Mathf.Max(presenceFloor, flowFactor);

        float targetWaterfall = inWaterfall ? maxWaterfallVolume : 0f;

        riverVol = MoveVolumeToward(riverInst, riverVol, targetRiver);
        rockVol = MoveVolumeToward(rockInst, rockVol, targetRocks);
        waterfallVol = MoveVolumeToward(waterfallInst, waterfallVol, targetWaterfall);

        HandleStartStop(riverInst, riverVol, targetRiver);
        HandleStartStop(rockInst, rockVol, targetRocks);
        HandleStartStop(waterfallInst, waterfallVol, targetWaterfall);
    }

    void OnDisable()
    {
        StopAllInstances(true);
    }

    void OnDestroy()
    {
        ReleaseAllInstances();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsOnFeatureLayers(other)) return;

        if (IsFeature(other, rockTag)) rockCount++;
        if (IsFeature(other, waterfallTag)) waterfallCount++;
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsOnFeatureLayers(other)) return;

        if (IsFeature(other, rockTag)) rockCount = Mathf.Max(0, rockCount - 1);
        if (IsFeature(other, waterfallTag)) waterfallCount = Mathf.Max(0, waterfallCount - 1);
    }

    private float RockDensityFactor()
    {
        // Normalize rock count into 0..1 based on rockCountForMax
        return Mathf.Clamp01(rockCount / Mathf.Max(1f, rockCountForMax));
    }

    private void CreateInstances()
    {
        if (!riverCreated && streamEvent.IsNull == false)
        {
            riverInst = RuntimeManager.CreateInstance(streamEvent);
            riverCreated = true;
        }
        if (!rockCreated && rockSplashEvent.IsNull == false)
        {
            rockInst = RuntimeManager.CreateInstance(rockSplashEvent);
            rockCreated = true;
        }
        if (!waterfallCreated && waterfallEvent.IsNull == false)
        {
            waterfallInst = RuntimeManager.CreateInstance(waterfallEvent);
            waterfallCreated = true;
        }

        Update3DAttributes();
    }

    private void Update3DAttributes()
    {
        if (riverCreated)
        {
            riverInst.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject, rb));
        }
        if (rockCreated)
        {
            rockInst.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject, rb));
        }
        if (waterfallCreated)
        {
            waterfallInst.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject, rb));
        }
    }

    private float MoveVolumeToward(EventInstance inst, float current, float target)
    {
        if (!IsValid(inst)) return 0f;

        float next = Mathf.MoveTowards(current, Mathf.Clamp01(target), fadeSpeed * Time.deltaTime);
        if (!Mathf.Approximately(next, current))
        {
            SetVolume(inst, next);
        }
        return next;
    }

    private void SetVolume(EventInstance inst, float vol)
    {
        if (!IsValid(inst)) return;
        inst.setVolume(Mathf.Clamp01(vol));
    }

    private void EnsureStartedIfAudible(EventInstance inst, float vol)
    {
        if (!IsValid(inst)) return;
        if (vol > 0.001f)
        {
            inst.getPlaybackState(out PLAYBACK_STATE state);
            if (state != PLAYBACK_STATE.PLAYING && state != PLAYBACK_STATE.STARTING)
            {
                inst.start();
            }
        }
    }

    private void HandleStartStop(EventInstance inst, float currentVol, float targetVol)
    {
        if (!IsValid(inst)) return;

        inst.getPlaybackState(out PLAYBACK_STATE state);

        if (targetVol > 0.001f && (state != PLAYBACK_STATE.PLAYING && state != PLAYBACK_STATE.STARTING))
        {
            inst.start();
            return;
        }

        if (targetVol <= 0.001f && currentVol <= 0.001f && (state == PLAYBACK_STATE.PLAYING || state == PLAYBACK_STATE.STARTING))
        {
            inst.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        }
    }

    private void StopAllInstances(bool allowFadeout)
    {
        var mode = allowFadeout ? FMOD.Studio.STOP_MODE.ALLOWFADEOUT : FMOD.Studio.STOP_MODE.IMMEDIATE;

        if (riverCreated) riverInst.stop(mode);
        if (rockCreated) rockInst.stop(mode);
        if (waterfallCreated) waterfallInst.stop(mode);
    }

    private void ReleaseAllInstances()
    {
        if (riverCreated) { riverInst.release(); riverCreated = false; }
        if (rockCreated) { rockInst.release(); rockCreated = false; }
        if (waterfallCreated) { waterfallInst.release(); waterfallCreated = false; }
    }

    private static bool IsValid(EventInstance inst)
    {
        return inst.isValid();
    }

    private bool IsFeature(Collider col, string tagName)
    {
        return !string.IsNullOrEmpty(tagName) && col.CompareTag(tagName);
    }

    private bool IsOnFeatureLayers(Collider col)
    {
        return (featureLayers.value & (1 << col.gameObject.layer)) != 0;
    }

    // Convert the inspector "area" to a world-space radius, then to local collider radius
    private void ApplyAreaToCollider()
    {
        if (!sphere) sphere = GetComponent<SphereCollider>();
        if (!sphere) return;

        float worldRadius = Mathf.Sqrt(Mathf.Max(0.0001f, detectionArea) / (4f * Mathf.PI));
        float maxAxisScale = GetMaxLossyScale();
        float localRadius = worldRadius / Mathf.Max(0.0001f, maxAxisScale);
        sphere.radius = localRadius;
    }

    private float GetWorldRadius()
    {
        // Derived directly from detectionArea to avoid drift from transform scaling
        return Mathf.Sqrt(Mathf.Max(0.0001f, detectionArea) / (4f * Mathf.PI));
    }

    private float GetMaxLossyScale()
    {
        var ls = transform.lossyScale;
        return Mathf.Max(ls.x, Mathf.Max(ls.y, ls.z));
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Keep collider radius in sync with the area slider during edit time
        if (!sphere) sphere = GetComponent<SphereCollider>();
        if (sphere)
        {
            sphere.isTrigger = true;
            ApplyAreaToCollider();
        }
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        if (!drawWhenNotSelected && !UnityEditor.Selection.Contains(gameObject.GetInstanceID())) return;
        DrawGizmoVolume();
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        DrawGizmoVolume();
    }

    private void DrawGizmoVolume()
    {
        float r = GetWorldRadius();

        // Fill
        Gizmos.color = gizmoFillColor;
        Gizmos.DrawSphere(transform.position, r);

        // Wire (base)
        Gizmos.color = gizmoWireColor;
        Gizmos.DrawWireSphere(transform.position, r);

        // Optional emphasis for states
        if (waterfallCount > 0)
        {
            Gizmos.color = gizmoWaterfallWireColor;
            Gizmos.DrawWireSphere(transform.position, r * 1.02f);
        }
        if (rockCount > 0)
        {
            Gizmos.color = gizmoRocksWireColor;
            Gizmos.DrawWireSphere(transform.position, r * 1.04f);
        }

        // Label
        var pos = transform.position + Vector3.up * (r + 0.1f);
        string label = $"Detection r={r:0.##}m  A={detectionArea:0.#}m²\nRocks:{rockCount}  Waterfall:{waterfallCount}";
        var style = new GUIStyle(UnityEditor.EditorStyles.helpBox);
        style.alignment = TextAnchor.MiddleCenter;
        UnityEditor.Handles.Label(pos, label, style);
    }
#endif
}