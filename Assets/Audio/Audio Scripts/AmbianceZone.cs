using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[ExecuteAlways]
public class AmbianceZone : MonoBehaviour
{
    [Tooltip("Unity SplineContainer that holds your closed circle spline.")]
    public SplineContainer PathContainer;

    [Tooltip("Which spline in the container to use (0-based).")]
    public int SplineIndex = 0;

    [Tooltip("Character to track")]
    public GameObject Player;

    // Internal defaults (not shown in Inspector)
    const int Samples = 256;                      // polyline samples for closest-point
    static readonly Vector3 FixedUp = Vector3.up; // up axis for orientation

    // Baked polyline of the spline
    Vector3[] _pts;
    int _count;

    void OnValidate()
    {
        if (PathContainer == null)
            PathContainer = GetComponentInParent<SplineContainer>();
        SplineIndex = Mathf.Max(0, SplineIndex);
    }

    void OnEnable() => Rebuild();
    void OnDisable() { _pts = null; _count = 0; }

    void Update()
    {
        if (PathContainer == null || Player == null) return;
        if (SplineIndex < 0 || SplineIndex >= PathContainer.Splines.Count) return;

        if (_pts == null) Rebuild();

        // 1) Closest point on polyline (continuous via segment projection)
        ClosestOnPolyline(Player.transform.position, out Vector3 closestPos, out Vector3 tangent);

        // 2) Set spline pose first (matches original flow)
        Quaternion rot = tangent.sqrMagnitude > 0f
            ? Quaternion.LookRotation(tangent.normalized, FixedUp)
            : transform.rotation;

        transform.position = closestPos;
        transform.rotation = rot;

        // 3) Inside test via dot, attach to player when "inside"
        Vector3 Sub = transform.position - Player.transform.position; // splineClosest - player
        Vector3 SplineRight = transform.right;                        // frame right at closest point
        float dot = Vector3.Dot(Sub, SplineRight);

        if (dot > 0f)
        {
            transform.position = Player.transform.position;
            transform.rotation = Player.transform.rotation;
        }
    }

    void Rebuild()
    {
        int n = Samples;
        _pts = new Vector3[n + 1]; // closed loop (duplicate first at end)
        _count = n;

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n; // [0,1)
            if (PathContainer.Evaluate(SplineIndex, t, out float3 pos, out _, out _))
                _pts[i] = (Vector3)pos;
            else
                _pts[i] = transform.position;
        }
        _pts[n] = _pts[0];
    }

    // Continuous closest point by projecting onto each segment
    void ClosestOnPolyline(Vector3 point, out Vector3 pos, out Vector3 tangent)
    {
        pos = _pts[0];
        tangent = Vector3.forward;
        float bestD2 = float.MaxValue;

        for (int i = 0; i < _count; i++)
        {
            Vector3 a = _pts[i];
            Vector3 b = _pts[i + 1];
            Vector3 ab = b - a;
            float abLen2 = Mathf.Max(1e-9f, ab.sqrMagnitude);

            float u = Mathf.Clamp01(Vector3.Dot(point - a, ab) / abLen2);
            Vector3 q = a + u * ab;
            float d2 = (point - q).sqrMagnitude;

            if (d2 < bestD2)
            {
                bestD2 = d2;
                pos = q;
                tangent = abLen2 > 1e-9f ? ab.normalized : tangent;
            }
        }
    }
}