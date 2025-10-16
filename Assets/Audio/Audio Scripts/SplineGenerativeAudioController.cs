using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
public class SplineGenerativeAudioController : MonoBehaviour
{
    [Header("Environment Features")]
    [Tooltip("Define object tags and their corresponding FMOD Events.")]
    public List<FeatureSound> featureSounds = new List<FeatureSound>();

    [System.Serializable]
    public class FeatureSound
    {
        public string tag;
        public EventReference fmodEvent;
        [Range(0f, 1f)]
        public float maxVolume = 0.7f;
    }

    [Header("FMOD Event: Base Stream")]
    [SerializeField] private EventReference streamEvent;
    [Range(0f, 1f)] public float baseStreamVolume = 0.8f;

    [Header("Detection Area")]
    [Range(0.1f, 5000f)]
    public float detectionArea = 50f;
    public bool drawGizmos = true;

    [Header("Fades")]
    public float fadeSpeed = 3f;

    private Rigidbody rb;
    private SphereCollider sphere;

    // Stream sound
    private EventInstance streamInst;
    private float streamVol;
    private bool streamCreated;

    // Feature object FMOD instances
    // Key: Collider instanceID. Value: Per-tag dictionary of FMOD event instance
    private Dictionary<Collider, Dictionary<string, EventInstance>> activeFeatureInstances = new Dictionary<Collider, Dictionary<string, EventInstance>>();

    void Awake()
    {
        sphere = GetComponent<SphereCollider>();
        sphere.isTrigger = true;

        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        ApplyAreaToCollider();
        CreateStreamInstance();
    }

    void OnEnable()
    {
        streamVol = baseStreamVolume;
        SetVolume(streamInst, streamVol);
        EnsureStartedIfAudible(streamInst, streamVol);

        // Prime: detect all objects already in area
        var hits = Physics.OverlapSphere(transform.position, GetWorldRadius(), ~0, QueryTriggerInteraction.Collide);
        foreach (var h in hits)
            TryStartFeatureEvents(h);
    }

    void Update()
    {
        Update3DAttributes();

        streamVol = MoveVolumeToward(streamInst, streamVol, baseStreamVolume);
        HandleStartStop(streamInst, streamVol, baseStreamVolume);

        // Update 3D positions for all active feature instances
        foreach (var entry in activeFeatureInstances)
        {
            var col = entry.Key;
            if (!col || !col.gameObject.activeInHierarchy)
                continue;
            foreach (var pair in entry.Value)
            {
                var inst = pair.Value;
                if (inst.isValid())
                    inst.set3DAttributes(RuntimeUtils.To3DAttributes(col.gameObject.transform));
            }
        }
    }

    void OnDisable()
    {
        StopAllInstances(true);
        // Release and clear all feature object instances
        foreach (var dict in activeFeatureInstances.Values)
            foreach (var inst in dict.Values)
                if (inst.isValid())
                    inst.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        activeFeatureInstances.Clear();
    }

    void OnDestroy()
    {
        ReleaseAllInstances();
    }

    void OnTriggerEnter(Collider other)
    {
        TryStartFeatureEvents(other);
    }

    void OnTriggerExit(Collider other)
    {
        TryStopFeatureEvents(other);
    }

    // --- Feature Sound Control ---

    void TryStartFeatureEvents(Collider col)
    {
        foreach (var feature in featureSounds)
        {
            if (col.CompareTag(feature.tag))
            {
                if (!activeFeatureInstances.TryGetValue(col, out var taggedInstances))
                {
                    taggedInstances = new Dictionary<string, EventInstance>();
                    activeFeatureInstances[col] = taggedInstances;
                }
                // Only create if not already playing for this tag
                if (!taggedInstances.ContainsKey(feature.tag))
                {
                    var inst = RuntimeManager.CreateInstance(feature.fmodEvent);
                    inst.set3DAttributes(RuntimeUtils.To3DAttributes(col.gameObject.transform));
                    inst.setVolume(feature.maxVolume);
                    inst.start();
                    taggedInstances[feature.tag] = inst;
                }
            }
        }
    }

    void TryStopFeatureEvents(Collider col)
    {
        if (activeFeatureInstances.TryGetValue(col, out var taggedInstances))
        {
            foreach (var inst in taggedInstances.Values)
            {
                if (inst.isValid())
                    inst.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            }
            activeFeatureInstances.Remove(col);
        }
    }

    // --- Stream Base Sound ---

    private void CreateStreamInstance()
    {
        if (!streamCreated && !streamEvent.IsNull)
        {
            streamInst = RuntimeManager.CreateInstance(streamEvent);
            streamCreated = true;
            Update3DAttributes();
        }
    }

    private void Update3DAttributes()
    {
        if (streamCreated)
            streamInst.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject, rb));
    }

    private float MoveVolumeToward(EventInstance inst, float current, float target)
    {
        if (!IsValid(inst)) return 0f;
        float next = Mathf.MoveTowards(current, Mathf.Clamp01(target), fadeSpeed * Time.deltaTime);
        if (!Mathf.Approximately(next, current))
            SetVolume(inst, next);
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
                inst.start();
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
    }

    private void ReleaseAllInstances()
    {
        if (streamCreated) { streamInst.release(); streamCreated = false; }
        // Also release all feature instances
        foreach (var dict in activeFeatureInstances.Values)
            foreach (var inst in dict.Values)
                if (inst.isValid())
                    inst.release();
        activeFeatureInstances.Clear();
    }

    private static bool IsValid(EventInstance inst)
    {
        return inst.isValid();
    }

    // --- Area Utilities ---

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

        Gizmos.color = new Color(0f, 1f, 1f, 0.08f);
        Gizmos.DrawSphere(transform.position, r);

        Gizmos.color = new Color(0f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, r);
    }
#endif
}