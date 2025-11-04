using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[ExecuteAlways]
public class SplineFollower : MonoBehaviour
{
    public enum Mode
    {
        Zone,        // Attach to player when "inside", else stick to nearest point on spline
        FollowSpline // Always stick to nearest point on spline
    }

    [Header("Mode")]
    [Tooltip("Zone: attach to player when inside. FollowSpline: always follow the spline.")]
    public Mode Behavior = Mode.Zone;

    [Header("Spline")]
    [Tooltip("Unity SplineContainer that holds your spline.")]
    public SplineContainer PathContainer;

    [Tooltip("Which spline in the container to use (0-based).")]
    [Min(0)] public int SplineIndex = 0;

    [Header("Follow Object")]
    [Tooltip("Transform we project onto the spline (usually the Player/Listener).")]
    public Transform Player;

    [Tooltip("Tag to use when auto-finding the Player (always used if Player is missing).")]
    public string PlayerTag = "Player";

    [Header("Zone Options (only used in Zone mode)")]
    [Tooltip("When in Zone mode and attached to player, match player's rotation.")]
    public bool MatchPlayerRotationInside = false;

    [Tooltip("Flip inside/outside test if it feels inverted (depends on spline winding).")]
    public bool InvertInsideSign = false;

    [Header("Path Settings")]
    [Tooltip("Treat spline as a closed loop (wraps end to start).")]
    public bool ClosedLoop = true;

    [Tooltip("Polyline samples used to bake the spline (higher = smoother but more CPU in projection).")]
    [Min(8)] public int Samples = 512;

    [Header("Editor")]
    [Tooltip("Run in Edit Mode (when not playing).")]
    public bool RunInEditMode = true;

    // Hardcoded behavior (change here if you want different defaults)
    const bool kOrientToSpline = true;
    const float kPositionSmoothTime = 0.08f; // seconds; set 0 to disable smoothing
    const float kRotationDamping = 12f;      // 1/seconds; set 0 to snap rotation

    // Internal
    const float Epsilon = 1e-6f;
    static readonly Vector3 WorldUp = Vector3.up;

    Vector3[] _pts;           // baked polyline points (size = Samples + 1 if closed)
    int _segCount;            // segments == points-1

    // Smoothing state
    Vector3 _posVel;          // for Vector3.SmoothDamp
    Quaternion _rotCurrent = Quaternion.identity;

    // Run-once-per-frame guard and detach transition
    int _lastProcessedFrame = -1;
    bool _wasAttachedLastFrame = false;

    void OnValidate()
    {
        if (PathContainer == null)
            PathContainer = GetComponentInParent<SplineContainer>();

        SplineIndex = Mathf.Max(0, SplineIndex);
        Samples = Mathf.Max(8, Samples);

        Rebuild();
    }

    void OnEnable()
    {
        TryAutoAssignPlayerIfMissing();
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
    }

    void Update()
    {
        // Edit Mode updates
        if (!Application.isPlaying)
        {
            if (!RunInEditMode) return;
            Step(0f, editorTick: true);
            return;
        }

        // In Play Mode, try to process here (if Player moves in Update).
        TryStepOncePerFrame();
    }

    void LateUpdate()
    {
        if (!Application.isPlaying) return;

        // In Play Mode, also try to process here (if Player moves in LateUpdate).
        TryStepOncePerFrame();
    }

    void TryStepOncePerFrame()
    {
        // Ensure we process exactly once per frame, regardless of Update/LateUpdate order.
        if (_lastProcessedFrame == Time.frameCount) return;
        Step(Time.deltaTime, editorTick: false);
        _lastProcessedFrame = Time.frameCount;
    }

    void Step(float dt, bool editorTick)
    {
        if (PathContainer == null) return;

        // If Player went missing (destroyed), try to re-acquire or early-out safely.
        if (!IsAlive(Player))
        {
            TryAutoAssignPlayerIfMissing();
            if (!IsAlive(Player)) return;
        }

        // Keep the polyline fresh in the editor (splines can change without events)
        if (editorTick)
            Rebuild();

        if (_pts == null || _segCount <= 0)
        {
            Rebuild();
            if (_pts == null || _segCount <= 0) return;
        }

        // 1) Project player to the baked polyline
        Vector3 driverPos = Player.position;
        int bestSeg = 0;
        float bestU = 0f;
        float bestD2 = float.MaxValue;

        for (int i = 0; i < _segCount; i++)
        {
            Vector3 a = _pts[i];
            Vector3 b = _pts[i + 1];
            Vector3 ab = b - a;
            float denom = Mathf.Max(Epsilon, ab.sqrMagnitude);
            float u = Mathf.Clamp01(Vector3.Dot(driverPos - a, ab) / denom);
            Vector3 q = a + u * ab;
            float d2 = (driverPos - q).sqrMagnitude;

            if (d2 < bestD2)
            {
                bestD2 = d2;
                bestSeg = i;
                bestU = u;
            }
        }

        Vector3 pa = _pts[bestSeg];
        Vector3 pb = _pts[bestSeg + 1];
        Vector3 projPos = Vector3.Lerp(pa, pb, bestU);
        Vector3 tan = pb - pa;
        if (tan.sqrMagnitude <= Epsilon) tan = transform.forward; else tan.Normalize();

        // 2) Zone inside test (robust) and attach logic
        bool attachedToPlayer = false;

        if (Behavior == Mode.Zone)
        {
            // Robust up: if tangent is near-vertical vs world up, pick a different up to avoid degeneracy.
            Vector3 up = (Mathf.Abs(Vector3.Dot(tan, WorldUp)) > 0.95f) ? Vector3.forward : WorldUp;
            Vector3 right = Vector3.Cross(up, tan).normalized;

            float dot = Vector3.Dot(projPos - driverPos, right); // positive => "inside" by default
            if (InvertInsideSign) dot = -dot;

            if (dot > 0f)
            {
                // Attach to player
                transform.position = driverPos;
                attachedToPlayer = true;

                if (MatchPlayerRotationInside)
                {
                    transform.rotation = Player.rotation;
                    _rotCurrent = transform.rotation; // keep rotation cache in sync
                }
            }
        }

        // 3) When not attached, place on spline (with a snap on the first frame after detaching)
        if (!attachedToPlayer)
        {
            bool snapNow = _wasAttachedLastFrame; // just exited zone this frame?

            if (snapNow || !(Application.isPlaying && dt > 0f && kPositionSmoothTime > 0f))
            {
                _posVel = Vector3.zero;
                transform.position = projPos;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(transform.position, projPos, ref _posVel, kPositionSmoothTime, Mathf.Infinity, dt);
            }

            // Apply orientation (with simple damping if enabled)
            if (kOrientToSpline)
            {
                Vector3 up = (Mathf.Abs(Vector3.Dot(tan, WorldUp)) > 0.95f) ? Vector3.forward : WorldUp;
                Quaternion targetRot = Quaternion.LookRotation(tan, up);

                if (Application.isPlaying && kRotationDamping > 0f && dt > 0f)
                {
                    float t = 1f - Mathf.Exp(-kRotationDamping * dt);
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
            _pts = null;
            _segCount = 0;
            return;
        }

        int n = Mathf.Max(8, Samples);
        int pointCount = n + (ClosedLoop ? 1 : 0);
        var pts = new Vector3[pointCount];

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            if (PathContainer.Evaluate(SplineIndex, t, out float3 pos, out _, out _))
                pts[i] = (Vector3)pos;
            else
                pts[i] = transform.position;
        }

        if (ClosedLoop)
            pts[n] = pts[0];

        _pts = pts;
        _segCount = _pts.Length - 1;
    }

    // Simple toggle without adding any extra Inspector UI
    public void SetRunInEditMode(bool enabled) => RunInEditMode = enabled;

    [ContextMenu("Toggle Run In Edit Mode")]
    public void ToggleRunInEditMode() => RunInEditMode = !RunInEditMode;

    static bool IsAlive(Object o) => o != null;

    void TryAutoAssignPlayerIfMissing()
    {
        if (IsAlive(Player)) return;

        // Always try to find by tag if provided
        if (!string.IsNullOrEmpty(PlayerTag))
        {
            var go = GameObject.FindWithTag(PlayerTag);
            if (go != null)
            {
                Player = go.transform;
                return;
            }
        }

        // Fallback to main camera
        if (Camera.main != null)
            Player = Camera.main.transform;
    }
}