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

    [Header("FMOD Events")]
    [SerializeField] private EventReference streamEvent;       // Base stream loop
    [SerializeField] private EventReference rockSplashEvent;   // Rock splash loop

    [Header("Volumes")]
    [Range(0f, 1f)] public float baseStreamVolume = 0.8f;
    [Range(0f, 1f)] public float maxRockVolume = 0.7f;

    [Header("Rock scaling")]
    [Tooltip("How many overlapping rock colliders are needed to reach full rock volume.")]
    [Min(1f)] public float rockCountForMax = 3f;

    [Header("Fades")]
    [Tooltip("How quickly volumes move toward their targets (units per second).")]
    public float fadeSpeed = 3f;

    [Header("Detection Area")]
    [Tooltip("WORLD-SPACE area of the detection sphere (A = 4πr²). This drives the SphereCollider radius.")]
    [Range(0.1f, 5000f)]
    public float detectionArea = 50f;

    [Header("Gizmos")]
    [Tooltip("If enabled, draws a simple gizmo sphere representing the detection area.")]
    public bool drawGizmos = true;

    [Header("Debug logging")]
    [Tooltip("Log rock counts to the Console when they change or on enable.")]
    public bool logCountsToConsole = true;

    // State
    private int rockCount;

    private Rigidbody rb;
    private SphereCollider sphere;

    // FMOD instances
    private EventInstance streamInst;
    private EventInstance rockInst;

    // Cached volumes
    private float streamVol;
    private float rockVol;

    private bool streamCreated;
    private bool rockCreated;

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
        // Prime counts if starting inside volumes (check every collider)
        var hits = Physics.OverlapSphere(transform.position, GetWorldRadius(), ~0, QueryTriggerInteraction.Collide);
        rockCount = 0;
        foreach (var h in hits)
        {
            if (IsFeature(h, rockTag)) rockCount++;
        }

        // Log initial state (just the rock count)
        LogCounts();

        // Initial volumes
        streamVol = baseStreamVolume;
        rockVol = (rockCount > 0) ? maxRockVolume * RockDensityFactor() : 0f;

        SetVolume(streamInst, streamVol);
        SetVolume(rockInst, rockVol);

        EnsureStartedIfAudible(streamInst, streamVol);
        EnsureStartedIfAudible(rockInst, rockVol);
    }

    void Update()
    {
        // Keep FMOD 3D attributes in sync
        Update3DAttributes();

        float targetStream = baseStreamVolume;

        // Rock volume purely from density (how many rocks are inside)
        float targetRocks = (rockCount > 0) ? maxRockVolume * RockDensityFactor() : 0f;

        streamVol = MoveVolumeToward(streamInst, streamVol, targetStream);
        rockVol = MoveVolumeToward(rockInst, rockVol, targetRocks);

        HandleStartStop(streamInst, streamVol, targetStream);
        HandleStartStop(rockInst, rockVol, targetRocks);
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
        if (IsFeature(other, rockTag))
        {
            rockCount++;
            LogCounts();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (IsFeature(other, rockTag))
        {
            rockCount = Mathf.Max(0, rockCount - 1);
            LogCounts();
        }
    }

    private float RockDensityFactor()
    {
        // Normalize rock count into 0..1 based on rockCountForMax
        return Mathf.Clamp01(rockCount / Mathf.Max(1f, rockCountForMax));
    }

    private void CreateInstances()
    {
        if (!streamCreated && streamEvent.IsNull == false)
        {
            streamInst = RuntimeManager.CreateInstance(streamEvent);
            streamCreated = true;
        }
        if (!rockCreated && rockSplashEvent.IsNull == false)
        {
            rockInst = RuntimeManager.CreateInstance(rockSplashEvent);
            rockCreated = true;
        }

        Update3DAttributes();
    }

    private void Update3DAttributes()
    {
        if (streamCreated)
        {
            streamInst.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject, rb));
        }
        if (rockCreated)
        {
            rockInst.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject, rb));
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

        if (streamCreated) streamInst.stop(mode);
        if (rockCreated) rockInst.stop(mode);
    }

    private void ReleaseAllInstances()
    {
        if (streamCreated) { streamInst.release(); streamCreated = false; }
        if (rockCreated) { rockInst.release(); rockCreated = false; }
    }

    private static bool IsValid(EventInstance inst)
    {
        return inst.isValid();
    }

    private bool IsFeature(Collider col, string tagName)
    {
        return !string.IsNullOrEmpty(tagName) && col.CompareTag(tagName);
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

    // Console logging helper (prints only the rock count)
    private void LogCounts()
    {
        if (!logCountsToConsole) return;
        Debug.Log($"[SplineGenerativeAudio] Rocks: {rockCount}", this);
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

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        DrawGizmoVolume();
    }

    private void DrawGizmoVolume()
    {
        float r = GetWorldRadius();

        // Fill (fixed subtle cyan)
        Gizmos.color = new Color(0f, 1f, 1f, 0.08f);
        Gizmos.DrawSphere(transform.position, r);

        // Wire (fixed cyan)
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, r);

        // Optional emphasis for rocks state (fixed greenish wire)
        if (rockCount > 0)
        {
            Gizmos.color = new Color(0.1f, 1f, 0.6f, 1f);
            Gizmos.DrawWireSphere(transform.position, r * 1.04f);
        }
    }
#endif
}