using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
public class SplineGenerativeAudioController : MonoBehaviour
{
    [Header("Spline Main Audio")]
    [Tooltip("Core sounds of this spline object. All play automatically while this component is active.")]
    public List<SplineMainSound> splineMainAudio = new List<SplineMainSound>();

    [System.Serializable]
    public class SplineMainSound
    {
        public EventReference fmodEvent;
    }

    [Header("Spline Proximity Audio")]
    [Tooltip("Detail sounds triggered by tagged objects within the spline's detection radius.")]
    public List<ProximitySound> splineProximityAudio = new List<ProximitySound>();

    [System.Serializable]
    public class ProximitySound
    {
        public string tag;
        public EventReference fmodEvent;
    }

    [Header("Detection Area")]
    [Tooltip("Detection radius in meters.")]
    [Min(0.1f)] public float detectionRadius = 5f;

    [Header("Debug")]
    public bool debugShowContents = false;
    public bool logContentsToConsole = false;

    private Rigidbody rb;
    private SphereCollider sphere;

    private List<EventInstance> mainInstances = new List<EventInstance>();

    private Dictionary<Collider, Dictionary<string, EventInstance>> activeProximityInstances
        = new Dictionary<Collider, Dictionary<string, EventInstance>>();

    private List<Collider> _collidersInside = new List<Collider>();
    private string _lastInsideDigest = "";
    private HashSet<string> _proximityTagSet = new HashSet<string>();

    void OnValidate()
    {
        BuildProximityTagSet();
        if (!sphere) sphere = GetComponent<SphereCollider>();
        if (sphere)
        {
            sphere.isTrigger = true;
            ApplyRadiusToCollider();
        }
    }

    void Awake()
    {
        BuildProximityTagSet();

        sphere = GetComponent<SphereCollider>();
        sphere.isTrigger = true;

        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        ApplyRadiusToCollider();
        CreateMainInstances();
    }

    void OnEnable()
    {
        BuildProximityTagSet();

        foreach (var inst in mainInstances)
        {
            if (inst.isValid())
            {
                inst.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject, rb));
                inst.start();
            }
        }

        var hits = Physics.OverlapSphere(transform.position, detectionRadius, ~0, QueryTriggerInteraction.Collide);
        foreach (var h in hits)
            TryStartProximityEvents(h);

        if (debugShowContents || logContentsToConsole)
            UpdateInsideCacheAndMaybeLog();
    }

    void Update()
    {
        foreach (var inst in mainInstances)
            if (inst.isValid())
                inst.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject, rb));

        foreach (var entry in activeProximityInstances)
        {
            var col = entry.Key;
            if (!col || !col.gameObject.activeInHierarchy) continue;
            foreach (var pair in entry.Value)
                if (pair.Value.isValid())
                    pair.Value.set3DAttributes(RuntimeUtils.To3DAttributes(col.gameObject.transform));
        }

        if (debugShowContents || logContentsToConsole)
            UpdateInsideCacheAndMaybeLog();
    }

    void OnDisable()
    {
        foreach (var inst in mainInstances)
            if (inst.isValid())
                inst.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);

        foreach (var dict in activeProximityInstances.Values)
            foreach (var inst in dict.Values)
                if (inst.isValid())
                    inst.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);

        activeProximityInstances.Clear();
    }

    void OnDestroy()
    {
        foreach (var inst in mainInstances)
            if (inst.isValid())
                inst.release();
        mainInstances.Clear();

        foreach (var dict in activeProximityInstances.Values)
            foreach (var inst in dict.Values)
                if (inst.isValid())
                    inst.release();
        activeProximityInstances.Clear();
    }

    void OnTriggerEnter(Collider other) => TryStartProximityEvents(other);
    void OnTriggerExit(Collider other) => TryStopProximityEvents(other);

    // --- Main Audio ---

    private void CreateMainInstances()
    {
        mainInstances.Clear();
        foreach (var sound in splineMainAudio)
        {
            if (sound.fmodEvent.IsNull) continue;
            var inst = RuntimeManager.CreateInstance(sound.fmodEvent);
            inst.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject, rb));
            mainInstances.Add(inst);
        }
    }

    // --- Proximity Audio ---

    void TryStartProximityEvents(Collider col)
    {
        foreach (var proximity in splineProximityAudio)
        {
            if (string.IsNullOrEmpty(proximity.tag)) continue;
            if (!col.CompareTag(proximity.tag)) continue;

            if (!activeProximityInstances.TryGetValue(col, out var taggedInstances))
            {
                taggedInstances = new Dictionary<string, EventInstance>();
                activeProximityInstances[col] = taggedInstances;
            }

            if (!taggedInstances.ContainsKey(proximity.tag))
            {
                var inst = RuntimeManager.CreateInstance(proximity.fmodEvent);
                inst.set3DAttributes(RuntimeUtils.To3DAttributes(col.gameObject.transform));
                inst.start();
                taggedInstances[proximity.tag] = inst;
            }
        }
    }

    void TryStopProximityEvents(Collider col)
    {
        if (!activeProximityInstances.TryGetValue(col, out var taggedInstances)) return;
        foreach (var inst in taggedInstances.Values)
            if (inst.isValid())
                inst.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        activeProximityInstances.Remove(col);
    }

    // --- Collider Setup ---

    private void ApplyRadiusToCollider()
    {
        if (!sphere) sphere = GetComponent<SphereCollider>();
        if (!sphere) return;
        float maxScale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        sphere.radius = detectionRadius / Mathf.Max(0.0001f, maxScale);
    }

    // --- Debug ---

    private void UpdateInsideCacheAndMaybeLog()
    {
        var hits = Physics.OverlapSphere(transform.position, detectionRadius, ~0, QueryTriggerInteraction.Collide);
        _collidersInside.Clear();
        foreach (var c in hits)
            if (c != null && _proximityTagSet.Contains(c.gameObject.tag))
                _collidersInside.Add(c);

        _collidersInside.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
        var sb = new System.Text.StringBuilder();
        foreach (var c in _collidersInside)
            if (c) sb.Append(c.gameObject.name).Append("|");
        string digest = sb.ToString();

        if (digest != _lastInsideDigest)
        {
            if (logContentsToConsole)
            {
                if (_collidersInside.Count == 0)
                    Debug.Log($"[SplineGenerativeAudio] Detection radius ({detectionRadius}m) now contains: (none)", this);
                else
                {
                    Debug.Log($"[SplineGenerativeAudio] Detection radius ({detectionRadius}m) contains {_collidersInside.Count} proximity object(s):", this);
                    foreach (var c in _collidersInside)
                        Debug.Log($"  - {c.gameObject.name} (tag={c.gameObject.tag})", this);
                }
            }
            _lastInsideDigest = digest;
        }
    }

    private void BuildProximityTagSet()
    {
        _proximityTagSet.Clear();
        if (splineProximityAudio == null) return;
        foreach (var p in splineProximityAudio)
            if (!string.IsNullOrEmpty(p.tag))
                _proximityTagSet.Add(p.tag);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!debugShowContents) return;

        var hits = Physics.OverlapSphere(transform.position, detectionRadius, ~0, QueryTriggerInteraction.Collide);
        Gizmos.color = new Color(1f, 0.5f, 0.7f, 0.9f);
        foreach (var c in hits)
        {
            if (c == null || !_proximityTagSet.Contains(c.gameObject.tag)) continue;
            Vector3 p = c.bounds.center;
            Gizmos.DrawSphere(p, 0.08f);
            Handles.color = new Color(1f, 0.9f, 0.2f, 1f);
            Handles.Label(p + Vector3.up * 0.12f, $"{c.gameObject.name} [{c.gameObject.tag}]");
        }
    }
#endif
}