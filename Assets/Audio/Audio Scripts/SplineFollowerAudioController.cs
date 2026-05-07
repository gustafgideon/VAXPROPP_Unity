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
    public enum Mode { Zone, FollowSpline }

    private Texture2D speakerIconTexture;
    public Transform Player;
    private string PlayerTag = "Player";

    [Header("Spline Settings")]
    public Mode mode = Mode.Zone;
    public SplineContainer PathContainer;

    [Tooltip("Index of the spline in the container to follow.")]
    [Min(0)]
    private int SplineIndex = 0;

    [Tooltip("When in Zone mode, match the player's rotation while inside.")]
    private bool MatchPlayerRotationInside = false;

    [Min(0.1f)]
    public float DetectionRadius = 15f;

    [Tooltip("If true, use only XZ (horizontal) to compute nearest point so jumping doesn't affect attachment.")]
    private bool ProjectDriverHorizontally = true;

    [Tooltip("Orient the follower along the spline tangent.")]
    private bool OrientToSpline = true;

    [Tooltip("Smooth time for position; set to 0 for exact (snappy) attachment to spline.")]
    [Min(0f)]
    private float PositionSmoothTime = 0.08f;

    [Tooltip("Rotation damping when orienting to spline; 0 for instant.")]
    [Min(0f)]
    private float RotationDamping = 12f;

    [Header("FMOD")]
    public EventReference mainAudioEvent;
    private float baseAudioVolume = 1f;
    public List<FeatureAudioEvent> featureAudioEvents = new List<FeatureAudioEvent>();

    [System.Serializable]
    public class FeatureAudioEvent
    {
        public string tag;
        public EventReference featureAudioEvent;
        [Range(0f, 1f)]
        private float maxVolume = 1f;
    }

    [Header("Debug")]
    private bool debugShowContents = false;
    private bool logContentsToConsole = false;
    private bool RunInEditMode = true;
    private bool drawGizmos = false;
    private bool InvertInsideSign = false;

    private float fadeSpeed = 3f;

    static readonly Vector3 WorldUp = Vector3.up;

    Vector3 _posVel;
    Quaternion _rotCurrent = Quaternion.identity;
    int _lastProcessedFrame = -1;
    bool _wasAttachedLastFrame = false;

    Rigidbody rb;
    SphereCollider sphere;
    EventInstance audioInst;
    float audioVol;
    bool audioCreated;

    Dictionary<Collider, Dictionary<string, EventInstance>> activeFeatureInstances = new Dictionary<Collider, Dictionary<string, EventInstance>>();
    List<Collider> _collidersInside = new List<Collider>();
    string _lastInsideDigest = "";
    HashSet<string> _featureTagSet = new HashSet<string>();

    void OnValidate()
    {
        if (PathContainer == null)
            PathContainer = GetComponentInParent<SplineContainer>();

        SplineIndex = Mathf.Max(0, SplineIndex);

        BuildFeatureTagSet();

        if (!sphere) sphere = GetComponent<SphereCollider>();
        if (sphere)
        {
            sphere.isTrigger = true;
            ApplyRadiusToCollider();
        }
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

        var hits = Physics.OverlapSphere(transform.position, DetectionRadius, ~0, QueryTriggerInteraction.Collide);
        foreach (var h in hits) TryStartFeatureEvents(h);

        if (debugShowContents || logContentsToConsole)
            UpdateInsideCacheAndMaybeLog();

        _rotCurrent = transform.rotation;
        _lastProcessedFrame = -1;
        _wasAttachedLastFrame = false;
    }

    void OnDisable()
    {
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
        if (!Application.isPlaying)
        {
            if (!RunInEditMode) return;
            Step(0f);
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

    void TryStepOncePerFrame()
    {
        if (_lastProcessedFrame == Time.frameCount) return;
        Step(Time.deltaTime);
        _lastProcessedFrame = Time.frameCount;
    }

    void Step(float dt)
    {
        if (PathContainer == null) return;

        if (!IsAlive(Player))
        {
            TryAutoAssignPlayerIfMissing();
            if (!IsAlive(Player)) return;
        }

        if (SplineIndex < 0 || SplineIndex >= PathContainer.Splines.Count)
            return;

        var spline = PathContainer.Splines[SplineIndex];
        Vector3 driverPos = Player.position;

        Vector3 localPlayerPos = PathContainer.transform.InverseTransformPoint(driverPos);
        float3 queryPointLocal = localPlayerPos;

        if (ProjectDriverHorizontally)
            queryPointLocal.y = 0f;

        SplineUtility.GetNearestPoint(spline, queryPointLocal, out float3 nearestPointLocal, out float normalizedT);

        Vector3 projPos = PathContainer.transform.TransformPoint((Vector3)nearestPointLocal);

        Vector3 tan = PathContainer.transform.TransformDirection((Vector3)spline.EvaluateTangent(normalizedT)).normalized;
        if (tan.sqrMagnitude < 0.0001f)
            tan = transform.forward;

        bool attachedToPlayer = false;

        if (mode == Mode.Zone && spline.Closed)
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
                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    projPos,
                    ref _posVel,
                    PositionSmoothTime,
                    Mathf.Infinity,
                    dt
                );
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

    void TryStartFeatureEvents(Collider col)
    {
        foreach (var feature in featureAudioEvents)
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
                    var inst = RuntimeManager.CreateInstance(feature.featureAudioEvent);
                    inst.set3DAttributes(RuntimeUtils.To3DAttributes(col.gameObject.transform));
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

    void CreateAudioInstance()
    {
        if (!audioCreated && !mainAudioEvent.IsNull)
        {
            audioInst = RuntimeManager.CreateInstance(mainAudioEvent);
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
            inst.start();
            return;
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

    void ApplyRadiusToCollider()
    {
        if (!sphere) sphere = GetComponent<SphereCollider>();
        if (!sphere) return;
        float maxAxisScale = GetMaxLossyScale();
        float localRadius = DetectionRadius / Mathf.Max(0.0001f, maxAxisScale);
        sphere.radius = localRadius;
    }

    float GetMaxLossyScale()
    {
        var ls = transform.lossyScale;
        return Mathf.Max(ls.x, Mathf.Max(ls.y, ls.z));
    }

    void UpdateInsideCacheAndMaybeLog()
    {
        var hits = Physics.OverlapSphere(transform.position, DetectionRadius, ~0, QueryTriggerInteraction.Collide);
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
                    Debug.Log($"[SplineGenerativeAudio] Detection radius ({DetectionRadius}m) now contains: (none)", this);
                else
                {
                    Debug.Log($"[SplineGenerativeAudio] Detection radius ({DetectionRadius}m) contains {_collidersInside.Count} feature object(s):", this);
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
        if (featureAudioEvents == null) return;
        foreach (var f in featureAudioEvents)
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
        Gizmos.DrawWireSphere(transform.position, DetectionRadius);
        if (debugShowContents)
        {
            var hits = Physics.OverlapSphere(transform.position, DetectionRadius, ~0, QueryTriggerInteraction.Collide);
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
            Gizmos.DrawGUITexture(new Rect(iconPos, new Vector2(0.5f, 0.5f)), speakerIconTexture);
        else
            Gizmos.DrawIcon(iconPos, "AudioSource Gizmo", true);
    }
#endif
}