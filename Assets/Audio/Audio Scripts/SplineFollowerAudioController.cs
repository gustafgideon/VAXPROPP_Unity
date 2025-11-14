using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using FMODUnity;
using FMOD.Studio;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
public class SplineFollowerAudioController : MonoBehaviour
{
    // Spline follower section

    public enum Mode { Zone, FollowSpline }

    private Texture2D speakerIconTexture;
    public Transform Player;
    private string PlayerTag = "Player";

    [Header("Spline Settings")]
    public Mode Behavior = Mode.Zone;
    public SplineContainer PathContainer;

    [Tooltip("Index of the spline in the container to follow.")]
    [Min(0)]
    private int SplineIndex = 0;

    [Tooltip("Mirror the Closed flag from the selected spline (recommended).")]
    public bool UseSplineClosedFlag = true;

    [SerializeField] private bool ClosedLoop = true;

    [Min(8)]
    private int Samples = 512;

    [Tooltip("When in Zone mode, match the player's rotation while inside.")]
    private bool MatchPlayerRotationInside = false;

    [Min(0.1f)]
    public float detectionRadius = 15f;
    
    [Tooltip("If true, use only XZ (horizontal) to compute nearest point so jumping doesn't affect attachment.")]
    private bool ProjectDriverHorizontally = true;

    [Tooltip("If your SplineContainer.Evaluate returns local-space positions, keep this true (default). If your version returns world-space, set to false.")]
    private bool EvaluateReturnsLocal = false;

    [Tooltip("Orient the follower along the spline tangent.")]
    private bool OrientToSpline = true;

    [Tooltip("Smooth time for position; set to 0 for exact (snappy) attachment to spline.")]
    [Min(0f)]
    private float PositionSmoothTime = 0.08f;

    [Tooltip("Rotation damping when orienting to spline; 0 for instant.")]
    [Min(0f)]
    private float RotationDamping = 12f;

    [FormerlySerializedAs("streamEvent")] [Header("FMOD")]
    public EventReference audioEvent;
    private float baseAudioVolume = 1f;
    public List<FeatureEvents> featureEvents = new List<FeatureEvents>();

    [System.Serializable]
    public class FeatureEvents
    {
        public string tag;
        public EventReference featureEvent;
        [Range(0f, 1f)]
        private float maxVolume = 1f;
    }

    [Header("Debug")]
    public bool debugShowContents = false;
    public bool logContentsToConsole = false;
    public bool RunInEditMode = true;
    private bool drawGizmos = false;
    public bool InvertInsideSign = false;

    private float fadeSpeed = 3f;

    // --- Spline follower internals ---
    const float Epsilon = 1e-6f;
    static readonly Vector3 WorldUp = Vector3.up;

    Vector3[] _pts;
    int _segCount;
    Vector3 _posVel;
    Quaternion _rotCurrent = Quaternion.identity;
    int _lastProcessedFrame = -1;
    bool _wasAttachedLastFrame = false;

    // --- Audio internals ---
    Rigidbody rb;
    SphereCollider sphere;
    EventInstance audioInst;
    float audioVol;
    bool audioCreated;

    Dictionary<Collider, Dictionary<string, EventInstance>> activeFeatureInstances = new Dictionary<Collider, Dictionary<string, EventInstance>>();
    List<Collider> _collidersInside = new List<Collider>();
    string _lastInsideDigest = "";
    HashSet<string> _featureTagSet = new HashSet<string>();

    // --- Unity lifecycle ---
    void OnValidate()
    {
        if (PathContainer == null)
            PathContainer = GetComponentInParent<SplineContainer>();
        SplineIndex = Mathf.Max(0, SplineIndex);
        Samples = Mathf.Max(8, Samples);

        // Mirror closed flag if requested
        if (PathContainer != null && SplineIndex >= 0 && SplineIndex < PathContainer.Splines.Count && UseSplineClosedFlag)
            ClosedLoop = PathContainer.Splines[SplineIndex].Closed;

        // Audio setup
        BuildFeatureTagSet();

        if (!sphere) sphere = GetComponent<SphereCollider>();
        if (sphere) { sphere.isTrigger = true; ApplyRadiusToCollider(); }

        Rebuild();
    }

    void Awake()
    {
        BuildFeatureTagSet();
        sphere = GetComponent<SphereCollider>();
        sphere.isTrigger = true;
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        ApplyRadiusToCollider();
        CreateAudioInstance();
    }

    void OnEnable()
    {
        TryAutoAssignPlayerIfMissing();
        BuildFeatureTagSet();

        audioVol = baseAudioVolume;
        SetVolume(audioInst, audioVol);
        EnsureStartedIfAudible(audioInst, audioVol);

        var hits = Physics.OverlapSphere(transform.position, detectionRadius, ~0, QueryTriggerInteraction.Collide);
        foreach (var h in hits) TryStartFeatureEvents(h);

        if (debugShowContents || logContentsToConsole)
            UpdateInsideCacheAndMaybeLog();

        Rebuild();
        _rotCurrent = transform.rotation;
        _lastProcessedFrame = -1;
        _wasAttachedLastFrame = false;
    }

    void OnDisable()
    {
        _pts = null;
        _segCount = 0;
        _posVel = Vector3.zero;
        _lastProcessedFrame = -1;
        _wasAttachedLastFrame = false;

        StopAllInstances(true);
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

    void Update()
    {
        // Spline position update
        if (!Application.isPlaying)
        {
            if (!RunInEditMode) return;
            Step(0f, editorTick: true);
        }
        else
        {
            TryStepOncePerFrame();
            Update3DAttributes();

            audioVol = MoveVolumeToward(audioInst, audioVol, baseAudioVolume);
            HandleStartStop(audioInst, audioVol, baseAudioVolume);

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

            if (debugShowContents || logContentsToConsole)
                UpdateInsideCacheAndMaybeLog();
        }
    }

    void LateUpdate()
    {
        if (Application.isPlaying)
            TryStepOncePerFrame();
    }

    // --- Spline movement ---
    void TryStepOncePerFrame()
    {
        if (_lastProcessedFrame == Time.frameCount) return;
        Step(Time.deltaTime, editorTick: false);
        _lastProcessedFrame = Time.frameCount;
    }

    void Step(float dt, bool editorTick)
    {
        if (PathContainer == null) return;

        if (!IsAlive(Player))
        {
            TryAutoAssignPlayerIfMissing();
            if (!IsAlive(Player)) return;
        }

        if (editorTick) Rebuild();
        if (_pts == null || _segCount <= 0)
        {
            Rebuild();
            if (_pts == null || _segCount <= 0) return;
        }

        // Mirror closed flag if requested (keeps behavior consistent when editing)
        if (UseSplineClosedFlag && PathContainer != null && SplineIndex >= 0 && SplineIndex < PathContainer.Splines.Count)
            ClosedLoop = PathContainer.Splines[SplineIndex].Closed;

        Vector3 driverPos = Player.position;
        int bestSeg = 0; float bestU = 0f; float bestD2 = float.MaxValue;

        for (int i = 0; i < _segCount; i++)
        {
            Vector3 a = _pts[i], b = _pts[i + 1];

            float u;
            float d2;

            if (ProjectDriverHorizontally)
            {
                // Project in XZ only so vertical motion (jumping) doesn't affect attachment
                Vector2 aXZ = new Vector2(a.x, a.z);
                Vector2 bXZ = new Vector2(b.x, b.z);
                Vector2 abXZ = bXZ - aXZ;
                float denomXZ = Mathf.Max(Epsilon, Vector2.Dot(abXZ, abXZ));
                Vector2 pMinusA = new Vector2(driverPos.x - a.x, driverPos.z - a.z);
                u = Mathf.Clamp01(Vector2.Dot(pMinusA, abXZ) / denomXZ);

                Vector2 qXZ = aXZ + u * abXZ;
                // Distance in XZ only
                Vector2 driverXZ = new Vector2(driverPos.x, driverPos.z);
                d2 = (driverXZ - qXZ).sqrMagnitude;
            }
            else
            {
                Vector3 ab = b - a;
                float denom = Mathf.Max(Epsilon, ab.sqrMagnitude);
                u = Mathf.Clamp01(Vector3.Dot(driverPos - a, ab) / denom);
                Vector3 q = a + u * ab;
                d2 = (driverPos - q).sqrMagnitude;
            }

            if (d2 < bestD2) { bestD2 = d2; bestSeg = i; bestU = u; }
        }

        Vector3 pa = _pts[bestSeg], pb = _pts[bestSeg + 1], projPos = Vector3.Lerp(pa, pb, bestU);
        Vector3 tan = pb - pa;
        if (tan.sqrMagnitude <= Epsilon) tan = transform.forward; else tan.Normalize();

        bool attachedToPlayer = false;

        if (Behavior == Mode.Zone)
        {
            Vector3 up = (Mathf.Abs(Vector3.Dot(tan, WorldUp)) > 0.95f) ? Vector3.forward : WorldUp;
            Vector3 right = Vector3.Cross(up, tan).normalized;
            float dot = Vector3.Dot(projPos - driverPos, right);
            if (InvertInsideSign) dot = -dot;

            if (dot > 0f)
            {
                transform.position = driverPos;
                attachedToPlayer = true;
                if (MatchPlayerRotationInside)
                {
                    transform.rotation = Player.rotation;
                    _rotCurrent = transform.rotation;
                }
            }
        }

        if (!attachedToPlayer)
        {
            bool snapNow = _wasAttachedLastFrame || PositionSmoothTime <= 0f;
            if (snapNow || !(Application.isPlaying && dt > 0f))
            {
                _posVel = Vector3.zero;
                transform.position = projPos;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(transform.position, projPos, ref _posVel, PositionSmoothTime, Mathf.Infinity, dt);
            }

            if (OrientToSpline)
            {
                Vector3 up = (Mathf.Abs(Vector3.Dot(tan, WorldUp)) > 0.95f) ? Vector3.forward : WorldUp;
                Quaternion targetRot = Quaternion.LookRotation(tan, up);
                if (Application.isPlaying && RotationDamping > 0f && dt > 0f)
                {
                    float t = 1f - Mathf.Exp(-RotationDamping * dt);
                    _rotCurrent = Quaternion.Slerp(_rotCurrent, targetRot, t);
                }
                else
                {
                    _rotCurrent = targetRot;
                }
                transform.rotation = _rotCurrent;
            }
        }

        _wasAttachedLastFrame = attachedToPlayer;
    }

    void Rebuild()
    {
        if (PathContainer == null || SplineIndex < 0 || SplineIndex >= PathContainer.Splines.Count)
        {
            _pts = null; _segCount = 0; return;
        }

        // Mirror closed flag if requested
        if (UseSplineClosedFlag)
            ClosedLoop = PathContainer.Splines[SplineIndex].Closed;

        int n = Mathf.Max(8, Samples);
        int pointCount = n + (ClosedLoop ? 1 : 0);
        var pts = new Vector3[pointCount];

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            if (PathContainer.Evaluate(SplineIndex, t, out float3 pos, out _, out _))
            {
                Vector3 p = (Vector3)pos;
                pts[i] = EvaluateReturnsLocal ? PathContainer.transform.TransformPoint(p) : p;
            }
            else
            {
                pts[i] = transform.position;
            }
        }
        if (ClosedLoop) pts[n] = pts[0];
        _pts = pts; _segCount = _pts.Length - 1;
    }

    public void SetRunInEditMode(bool enabled) => RunInEditMode = enabled;
    [ContextMenu("Toggle Run In Edit Mode")]
    public void ToggleRunInEditMode() => RunInEditMode = !RunInEditMode;
    static bool IsAlive(Object o) => o != null;

    void TryAutoAssignPlayerIfMissing()
    {
        if (IsAlive(Player)) return;
        if (!string.IsNullOrEmpty(PlayerTag))
        {
            var go = GameObject.FindWithTag(PlayerTag);
            if (go != null) { Player = go.transform; return; }
        }
        if (Camera.main != null) Player = Camera.main.transform;
    }

    // --- Audio: Feature control ---
    void TryStartFeatureEvents(Collider col)
    {
        foreach (var feature in featureEvents)
        {
            if (string.IsNullOrEmpty(feature.tag)) continue;
            if (col.CompareTag(feature.tag))
            {
                if (!activeFeatureInstances.TryGetValue(col, out var taggedInstances))
                {
                    taggedInstances = new Dictionary<string, EventInstance>();
                    activeFeatureInstances[col] = taggedInstances;
                }
                if (!taggedInstances.ContainsKey(feature.tag))
                {
                    var inst = RuntimeManager.CreateInstance(feature.featureEvent);
                    inst.set3DAttributes(RuntimeUtils.To3DAttributes(col.gameObject.transform));
                    //inst.setVolume(feature.maxVolume);
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
                if (inst.isValid()) inst.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            activeFeatureInstances.Remove(col);
        }
    }

    // --- Audio ---
    void CreateAudioInstance()
    {
        if (!audioCreated && !audioEvent.IsNull)
        {
            audioInst = RuntimeManager.CreateInstance(audioEvent);
            audioCreated = true;
            Update3DAttributes();
        }
    }

    void Update3DAttributes()
    {
        if (audioCreated) audioInst.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject, rb));
    }

    float MoveVolumeToward(EventInstance inst, float current, float target)
    {
        if (!IsValid(inst)) return 0f;
        float next = Mathf.MoveTowards(current, Mathf.Clamp01(target), fadeSpeed * Time.deltaTime);
        if (!Mathf.Approximately(next, current))
            SetVolume(inst, next);
        return next;
    }

    void SetVolume(EventInstance inst, float vol)
    {
        if (!IsValid(inst)) return;
        inst.setVolume(Mathf.Clamp01(vol));
    }

    void EnsureStartedIfAudible(EventInstance inst, float vol)
    {
        if (!IsValid(inst)) return;
        if (vol > 0.001f)
        {
            inst.getPlaybackState(out PLAYBACK_STATE state);
            if (state != PLAYBACK_STATE.PLAYING && state != PLAYBACK_STATE.STARTING)
                inst.start();
        }
    }

    void HandleStartStop(EventInstance inst, float currentVol, float targetVol)
    {
        if (!IsValid(inst)) return;
        inst.getPlaybackState(out PLAYBACK_STATE state);

        if (targetVol > 0.001f && (state != PLAYBACK_STATE.PLAYING && state != PLAYBACK_STATE.STARTING))
        {
            inst.start(); return;
        }
        if (targetVol <= 0.001f && currentVol <= 0.001f &&
            (state == PLAYBACK_STATE.PLAYING || state == PLAYBACK_STATE.STARTING))
        {
            inst.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        }
    }

    void StopAllInstances(bool allowFadeout)
    {
        var mode = allowFadeout ? FMOD.Studio.STOP_MODE.ALLOWFADEOUT : FMOD.Studio.STOP_MODE.IMMEDIATE;
        if (audioCreated) audioInst.stop(mode);
    }

    void ReleaseAllInstances()
    {
        if (audioCreated) { audioInst.release(); audioCreated = false; }
        foreach (var dict in activeFeatureInstances.Values)
            foreach (var inst in dict.Values)
                if (inst.isValid()) inst.release();
        activeFeatureInstances.Clear();
    }

    static bool IsValid(EventInstance inst) => inst.isValid();

    // --- Area utilities ---
    void ApplyRadiusToCollider()
    {
        if (!sphere) sphere = GetComponent<SphereCollider>();
        if (!sphere) return;
        float maxAxisScale = GetMaxLossyScale();
        float localRadius = detectionRadius / Mathf.Max(0.0001f, maxAxisScale);
        sphere.radius = localRadius;
    }

    float GetMaxLossyScale()
    {
        var ls = transform.lossyScale;
        return Mathf.Max(ls.x, Mathf.Max(ls.y, ls.z));
    }

    void UpdateInsideCacheAndMaybeLog()
    {
        var hits = Physics.OverlapSphere(transform.position, detectionRadius, ~0, QueryTriggerInteraction.Collide);
        _collidersInside.Clear();
        for (int i = 0; i < hits.Length; ++i)
        {
            var c = hits[i];
            if (c == null) continue;
            if (_featureTagSet.Contains(c.gameObject.tag))
                _collidersInside.Add(c);
        }
        _collidersInside.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (var c in _collidersInside)
        {
            if (c) sb.Append(c.gameObject.name).Append("|");
        }
        string digest = sb.ToString();

        if (digest != _lastInsideDigest)
        {
            if (logContentsToConsole)
            {
                if (_collidersInside.Count == 0)
                    Debug.Log($"[SplineGenerativeAudio] Detection radius ({detectionRadius}m) now contains: (none)", this);
                else
                {
                    Debug.Log($"[SplineGenerativeAudio] Detection radius ({detectionRadius}m) contains {_collidersInside.Count} feature object(s):", this);
                    foreach (var c in _collidersInside)
                        Debug.Log($"  - {c.gameObject.name} (tag={c.gameObject.tag})", this);
                }
            }
            _lastInsideDigest = digest;
        }
    }

    void BuildFeatureTagSet()
    {
        _featureTagSet.Clear();
        if (featureEvents == null) return;
        foreach (var f in featureEvents)
        {
            if (!string.IsNullOrEmpty(f.tag))
                _featureTagSet.Add(f.tag);
        }
    }

    void OnTriggerEnter(Collider other) { TryStartFeatureEvents(other); }
    void OnTriggerExit(Collider other) { TryStopFeatureEvents(other); }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        if (debugShowContents)
        {
            var hits = Physics.OverlapSphere(transform.position, detectionRadius, ~0, QueryTriggerInteraction.Collide);
            Gizmos.color = new Color(1f, 0.5f, 0.7f, 0.9f);
            for (int i = 0; i < hits.Length; ++i)
            {
                var c = hits[i];
                if (c == null) continue;
                if (!_featureTagSet.Contains(c.gameObject.tag)) continue;
                Vector3 p = c.bounds.center;
                Gizmos.DrawSphere(p, 0.08f);
#if UNITY_EDITOR
                Handles.color = new Color(1f, 0.9f, 0.2f, 1f);
                Handles.Label(p + Vector3.up * 0.12f, $"{c.gameObject.name} [{c.gameObject.tag}]");
#endif
            }
        }
    }
#endif
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Vector3 iconPos = transform.position + Vector3.up * 0.8f;
        if (speakerIconTexture != null)
            Gizmos.DrawGUITexture(new Rect(iconPos, new Vector2(0.5f,0.5f)), speakerIconTexture);
        else
            Gizmos.DrawIcon(iconPos, "AudioSource Gizmo", true); // fallback
    }
#endif
}