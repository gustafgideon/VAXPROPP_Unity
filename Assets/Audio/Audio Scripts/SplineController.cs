using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[ExecuteAlways]
public class SplineController : MonoBehaviour
{
    public enum Mode
    {
        Zone,        // Attach to player when "inside" (dot>0), else stick to spline
        FollowSpline // Always stick to closest point on spline
    }

    [Header("Mode")]
    [Tooltip("Zone: attach to player when inside. FollowSpline: always follow the spline.")]
    public Mode Behavior = Mode.Zone;

    [Header("Spline")]
    [Tooltip("Unity SplineContainer that holds your spline.")]
    public SplineContainer PathContainer;

    [Tooltip("Which spline in the container to use (0-based).")]
    public int SplineIndex = 0;

    [Header("Driver")]
    [Tooltip("Transform we project onto the spline (usually the Player/Listener).")]
    public Transform Player;

    [Tooltip("If true, auto-find Player by tag when missing or destroyed.")]
    public bool AutoFindPlayerByTag = true;

    [Tooltip("Tag to use when auto-finding the Player.")]
    public string PlayerTag = "Player";

    [Header("Orientation")]
    [Tooltip("Align follower with spline tangent.")]
    public bool OrientToSpline = true;

    [Tooltip("Use world up for a stable frame (prevents rolling/flip).")]
    public bool UseWorldUp = true;

    [Header("Zone Options (only used in Zone mode)")]
    [Tooltip("When in Zone mode and attached to player, match player's rotation.")]
    public bool MatchPlayerRotationInside = false;

    [Tooltip("Flip inside/outside test if it feels inverted (depends on spline winding).")]
    public bool InvertInsideSign = false;

    [Header("Path Settings")]
    [Tooltip("Treat spline as a closed loop (wraps arc-length).")]
    public bool ClosedLoop = true;

    [Tooltip("Polyline samples used to bake the spline (higher = smoother but more memory).")]
    [Min(8)] public int Samples = 512;

    [Header("Projection Search")]
    [Tooltip("Only search near the last best segment for performance/stability.")]
    public bool UseLocalSearch = true;

    [Tooltip("Segments to search on each side of the last best segment.")]
    [Range(2, 512)] public int LocalSearchWindow = 16;

    [Tooltip("Extra closeness required to switch segments (meters). Helps avoid flapping.")]
    [Min(0f)] public float DistanceSwitchBias = 0.01f;

    public enum ProjectionUpdateMode
    {
        EveryFrame,        // Highest accuracy
        DistanceOrAngle,   // Re-project only when player moved/turned enough
        FixedInterval      // Re-project at fixed Hz (interpolate between)
    }

    [Header("Update Budget")]
    public ProjectionUpdateMode ProjectionMode = ProjectionUpdateMode.DistanceOrAngle;

    [Tooltip("Meters of player movement required to re-project (DistanceOrAngle mode).")]
    [Min(0f)] public float MoveThreshold = 0.03f;

    [Tooltip("Degrees of player rotation required to re-project (DistanceOrAngle mode).")]
    [Min(0f)] public float AngleThresholdDeg = 1.0f;

    [Tooltip("Seconds between projections (FixedInterval mode). 0.033 = 30 Hz")]
    [Min(0.001f)] public float ProjectionInterval = 0.033f;

    [Tooltip("Run step in LateUpdate in Play Mode to follow after the player moved.")]
    public bool RunInLateUpdate = true;

    [Header("Editor")]
    [Tooltip("Run in Edit Mode (when not playing). Disable to avoid edit-time null issues.")]
    public bool RunInEditMode = false;

    [Header("Smoothing")]
    [Tooltip("Smooth time (seconds) for along-path movement.")]
    [Min(0f)] public float PositionSmoothTime = 0.08f;

    [Tooltip("Max speed (meters/second) along the path for smoothing.")]
    [Min(0.001f)] public float MaxSpeedAlongPath = 100f;

    [Tooltip("Rotation damping factor (1/seconds). 0 = no damping, higher = snappier.")]
    [Min(0f)] public float RotationDamping = 12f;

    // Internal
    const float Epsilon = 1e-6f;
    static readonly Vector3 WorldUp = Vector3.up;

    Vector3[] _pts;           // baked polyline points (size = Samples + 1 if closed)
    float[] _segLen;          // per-segment lengths (size = segmentCount)
    float[] _cumLen;          // cumulative length at segment start (size = segmentCount + 1)
    int _segCount;            // segments == points-1
    float _totalLen;

    // Along-path state (arc length, meters)
    float _filteredS;
    float _filteredSVel; // for SmoothDamp
    int _lastBestSeg = -1;

    // Cached rotation
    Quaternion _rotCurrent = Quaternion.identity;

    // Projection throttling
    Vector3 _lastPlayerPos;
    Quaternion _lastPlayerRot = Quaternion.identity;
    float _nextProjectionTime;
    bool _haveTargetS;
    float _targetS; // last computed targetS from projection

    void OnValidate()
    {
        if (PathContainer == null)
            PathContainer = GetComponentInParent<SplineContainer>();

        SplineIndex = Mathf.Max(0, SplineIndex);
        Samples = Mathf.Max(8, Samples);
        LocalSearchWindow = Mathf.Clamp(LocalSearchWindow, 2, 1024);
        MaxSpeedAlongPath = Mathf.Max(0.001f, MaxSpeedAlongPath);
        ProjectionInterval = Mathf.Max(0.001f, ProjectionInterval);
    }

    void Awake()
    {
        TryAutoAssignPlayerIfMissing();
    }

    void OnEnable()
    {
        Rebuild();
        TryAutoAssignPlayerIfMissing();
        InitializeOnStart();
    }

    void OnDisable()
    {
        _pts = null;
        _segLen = null;
        _cumLen = null;
        _segCount = 0;
        _lastBestSeg = -1;
        _haveTargetS = false;
    }

    void Update()
    {
        if (!Application.isPlaying && !RunInEditMode) return;

        // In play mode, optionally run in LateUpdate to ensure we follow AFTER the player moved.
        if (Application.isPlaying && RunInLateUpdate) return;

        // If Player went missing (destroyed), try to re-acquire or early-out safely.
        if (!IsAlive(Player))
        {
            TryAutoAssignPlayerIfMissing();
            if (!IsAlive(Player)) return;
        }

        Step(Application.isPlaying ? Time.deltaTime : 0f);
    }

    void LateUpdate()
    {
        if (!Application.isPlaying) return;
        if (!RunInLateUpdate) return;

        // If Player went missing (destroyed), try to re-acquire or early-out safely.
        if (!IsAlive(Player))
        {
            TryAutoAssignPlayerIfMissing();
            if (!IsAlive(Player)) return;
        }

        Step(Time.deltaTime);
    }

    void Step(float dt)
    {
        if (PathContainer == null || !IsAlive(Player)) return;
        if (SplineIndex < 0 || SplineIndex >= PathContainer.Splines.Count) return;
        if (_pts == null || _segCount == 0) { Rebuild(); if (_pts == null || _segCount == 0) return; }

        // 1) Decide whether to re-project this frame
        bool shouldProject = ShouldReproject();
        if (!IsAlive(Player)) return; // guard again in case Player was destroyed mid-frame

        if (shouldProject || !_haveTargetS)
        {
            var proj = ProjectPointToPolyline(Player.position);
            _targetS = _cumLen[proj.segment] + proj.u * _segLen[proj.segment];
            _haveTargetS = true;

            // Closed-loop wrap to keep continuity vs current filtered S
            if (ClosedLoop && _totalLen > Epsilon)
            {
                float delta = _targetS - _filteredS;
                if (delta > 0.5f * _totalLen) _targetS -= _totalLen;
                else if (delta < -0.5f * _totalLen) _targetS += _totalLen;
            }

            // Bookkeeping for next re-projection check
            if (IsAlive(Player))
            {
                _lastPlayerPos = Player.position;
                _lastPlayerRot = Player.rotation;
            }
            _nextProjectionTime = Time.time + ProjectionInterval;
        }

        // 2) Smooth along-path s using SmoothDamp
        float filteredS = Mathf.SmoothDamp(_filteredS, _targetS, ref _filteredSVel, PositionSmoothTime, MaxSpeedAlongPath, dt);

        // 3) Wrap filteredS and evaluate position/tangent at s
        if (ClosedLoop && _totalLen > Epsilon)
            filteredS = Mod(filteredS, _totalLen);
        else
            filteredS = Mathf.Clamp(filteredS, 0f, Mathf.Max(0f, _totalLen - Epsilon));

        EvaluateAtArcLength(filteredS, out Vector3 pos, out Vector3 tan);

        // 4) Apply transform with optional rotation damping
        transform.position = pos;

        if (OrientToSpline)
        {
            Vector3 fwd = tan.sqrMagnitude > Epsilon ? tan.normalized : transform.forward;
            Vector3 up = UseWorldUp ? WorldUp : WorldUp;

            Quaternion targetRot = Quaternion.LookRotation(fwd, up);

            if (RotationDamping > 0f && Application.isPlaying && dt > 0f)
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

        // 5) Zone behavior: optionally attach to the player if inside the spline zone.
        //    This mirrors the original AmbianceZone dot-product logic:
        //    compute Sub = splineClosest - player, use transform.right as spline frame's right,
        //    and attach when dot(Sub, right) > 0 (optionally inverted).
        if (Behavior == Mode.Zone && IsAlive(Player))
        {
            Vector3 Sub = transform.position - Player.position; // splineClosest - player
            Vector3 SplineRight = transform.right;              // frame right at closest point
            float dot = Vector3.Dot(Sub, SplineRight);
            if (InvertInsideSign) dot = -dot;

            if (dot > 0f)
            {
                // Attach to player
                transform.position = Player.position;
                if (MatchPlayerRotationInside && IsAlive(Player))
                {
                    transform.rotation = Player.rotation;
                    _rotCurrent = transform.rotation; // keep rotation cache in sync
                }
            }
        }

        _filteredS = filteredS;
    }

    bool ShouldReproject()
    {
        // Guard against destroyed Player
        if (!IsAlive(Player)) return false;

        switch (ProjectionMode)
        {
            case ProjectionUpdateMode.EveryFrame:
                return true;

            case ProjectionUpdateMode.FixedInterval:
                return Time.time >= _nextProjectionTime;

            case ProjectionUpdateMode.DistanceOrAngle:
            default:
            {
                Vector3 pos = Player.position;
                Quaternion rot = Player.rotation;

                if (_lastPlayerRot == Quaternion.identity && _lastPlayerPos == default)
                    return true; // first time

                // Moved enough?
                if ((pos - _lastPlayerPos).sqrMagnitude >= MoveThreshold * MoveThreshold)
                    return true;

                // Turned enough?
                float ang = Quaternion.Angle(rot, _lastPlayerRot);
                if (ang >= AngleThresholdDeg)
                    return true;

                return false;
            }
        }
    }

    void Rebuild()
    {
        if (PathContainer == null || SplineIndex < 0 || SplineIndex >= PathContainer.Splines.Count)
        {
            _pts = null; _segLen = null; _cumLen = null; _segCount = 0; _totalLen = 0;
            return;
        }

        int n = Mathf.Max(8, Samples);
        var pts = new Vector3[n + (ClosedLoop ? 1 : 0)];

        // Sample [0..1)
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
        _segLen = new float[_segCount];
        _cumLen = new float[_segCount + 1];

        float accum = 0f;
        _cumLen[0] = 0f;
        for (int i = 0; i < _segCount; i++)
        {
            float len = (_pts[i + 1] - _pts[i]).magnitude;
            _segLen[i] = Mathf.Max(Epsilon, len);
            accum += _segLen[i];
            _cumLen[i + 1] = accum;
        }
        _totalLen = accum;

        // Reset rot cache to current
        _rotCurrent = transform.rotation;
    }

    void InitializeOnStart()
    {
        if (!IsAlive(Player) || _segCount == 0) return;

        var proj = ProjectPointToPolyline(Player.position, fullScan: true);
        _lastBestSeg = proj.segment;
        _filteredS = Mathf.Clamp(_cumLen[proj.segment] + proj.u * _segLen[proj.segment], 0f, Mathf.Max(0f, _totalLen - Epsilon));
        _filteredSVel = 0f;

        _targetS = _filteredS;
        _haveTargetS = true;

        // Initialize rotation quickly
        EvaluateAtArcLength(_filteredS, out _, out Vector3 tan);
        Vector3 fwd = tan.sqrMagnitude > Epsilon ? tan.normalized : transform.forward;
        Vector3 up = UseWorldUp ? WorldUp : WorldUp;
        _rotCurrent = Quaternion.LookRotation(fwd, up);

        if (IsAlive(Player))
        {
            _lastPlayerPos = Player.position;
            _lastPlayerRot = Player.rotation;
        }
        _nextProjectionTime = Time.time + ProjectionInterval;
    }

    struct Projection
    {
        public int segment;   // segment index
        public float u;       // [0..1] along segment
        public Vector3 pos;   // projected position
        public float d2;      // squared distance
    }

    Projection ProjectPointToPolyline(Vector3 point, bool fullScan = false)
    {
        Projection best = new Projection
        {
            segment = Mathf.Clamp(_lastBestSeg, 0, Mathf.Max(0, _segCount - 1)),
            u = 0f,
            pos = (_segCount > 0) ? _pts[0] : transform.position,
            d2 = float.MaxValue
        };

        if (_segCount == 0) return best;

        int start, end;

        if (UseLocalSearch && !fullScan && _lastBestSeg >= 0)
        {
            int w = Mathf.Clamp(LocalSearchWindow, 2, _segCount);
            start = Mathf.Max(0, _lastBestSeg - w);
            end = Mathf.Min(_segCount - 1, _lastBestSeg + w);
        }
        else
        {
            start = 0;
            end = _segCount - 1;
        }

        for (int i = start; i <= end; i++)
        {
            Vector3 a = _pts[i];
            Vector3 b = _pts[i + 1];
            Vector3 ab = b - a;
            float denom = Mathf.Max(Epsilon, ab.sqrMagnitude);
            float u = Mathf.Clamp01(Vector3.Dot(point - a, ab) / denom);
            Vector3 q = a + u * ab;
            float d2 = (point - q).sqrMagnitude;

            // Bias to stay on current segment unless meaningfully better
            if (d2 + DistanceSwitchBias < best.d2)
            {
                best.segment = i;
                best.u = u;
                best.pos = q;
                best.d2 = d2;
            }
        }

        // If local window was used and result sits on the window edge and is still far,
        // fall back to a full scan once in a while (rare).
        if (UseLocalSearch && !fullScan)
        {
            if ((best.segment == start || best.segment == end) && best.d2 > 0.5f) // heuristic
            {
                return ProjectPointToPolyline(point, fullScan: true);
            }
        }

        _lastBestSeg = best.segment;
        return best;
    }

    void EvaluateAtArcLength(float s, out Vector3 pos, out Vector3 tan)
    {
        pos = transform.position;
        tan = transform.forward;

        if (_segCount == 0)
            return;

        s = Mathf.Clamp(s, 0f, Mathf.Max(0f, _totalLen - Epsilon));

        // Binary search on cumulative lengths
        int lo = 0, hi = _segCount;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (_cumLen[mid + 1] < s) lo = mid + 1;
            else hi = mid;
        }
        int iSeg = Mathf.Clamp(lo, 0, _segCount - 1);
        float s0 = _cumLen[iSeg];
        float segLen = _segLen[iSeg];
        float u = segLen > Epsilon ? Mathf.Clamp01((s - s0) / segLen) : 0f;

        Vector3 a = _pts[iSeg];
        Vector3 b = _pts[iSeg + 1];
        pos = Vector3.Lerp(a, b, u);
        Vector3 ab = b - a;
        tan = ab.sqrMagnitude > Epsilon ? ab.normalized : tan;
    }

    static float Mod(float x, float m)
    {
        if (m <= 0f) return x;
        float r = x % m;
        if (r < 0f) r += m;
        return r;
    }

    static bool IsAlive(Object o) => o != null;

    void TryAutoAssignPlayerIfMissing()
    {
        if (IsAlive(Player)) return;
        if (!AutoFindPlayerByTag || string.IsNullOrEmpty(PlayerTag)) return;

        var go = GameObject.FindWithTag(PlayerTag);
        if (go != null)
        {
            Player = go.transform;
        }
        else
        {
            // Optional: try main camera if tagged player not found
            if (Camera.main != null)
                Player = Camera.main.transform;
        }
    }
}