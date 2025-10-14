using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

namespace Cinemachine
{
    [ExecuteAlways]
    public class AmbianceZone : MonoBehaviour
    {
        [Tooltip("Unity SplineContainer that holds your closed circle spline.")]
        public SplineContainer PathContainer;

        [Tooltip("Which spline in the container to use (0-based).")]
        public int SplineIndex = 0;

        [Tooltip("Character to track")]
        public GameObject Player;

        [Header("Baking/Accuracy")]
        [Range(32, 2048)] public int BakeSamples = 256; // higher = smoother/less jitter

        [Header("Inside Hysteresis (prevents flicker)")]
        [Tooltip("Attach to player when player is within (meanRadius - EnterMargin).")]
        public float EnterMargin = 0.10f;
        [Tooltip("Detach back to spline when player is beyond (meanRadius + ExitMargin).")]
        public float ExitMargin = 0.15f;

        [Header("Orientation")]
        [Tooltip("Align with spline tangent when outside the circle.")]
        public bool OrientToSplineOutside = true;
        [Tooltip("Match player rotation when inside.")]
        public bool MatchPlayerRotationInside = false;
        [Tooltip("Fixed up used to build the frame (use Vector3.up for XZ circles).")]
        public Vector3 FixedUp = Vector3.up;

        [Header("Smoothing")]
        [Tooltip("Position smooth time (0 = snap).")]
        public float PositionSmoothTime = 0.06f;
        [Tooltip("Rotation speed in deg/sec (0 = snap).")]
        public float RotationSpeed = 360f;

        // Baked data
        Vector3[] _pts;     // world-space polyline points
        float[] _ts;        // corresponding normalized t values
        Vector3 _centroid;  // world-space centroid (XZ)
        float _meanRadius;  // average radius from centroid (XZ)
        int _count;

        // State
        bool _attachedToPlayer = false;
        Vector3 _vel;

        void OnValidate()
        {
            if (PathContainer == null)
                PathContainer = GetComponentInParent<SplineContainer>();
            SplineIndex = Mathf.Max(0, SplineIndex);
        }

        void OnEnable() => Rebuild();
        void OnDisable() { _pts = null; _ts = null; _count = 0; }

        void Update()
        {
            if (PathContainer == null || Player == null) return;
            if (SplineIndex < 0 || SplineIndex >= PathContainer.Splines.Count) return;

            // Rebuild if transform moved (editor) or samples changed
            if (_pts == null || _count != Mathf.Clamp(BakeSamples, 32, 2048))
                Rebuild();

            // 1) Closest point on the baked polyline (continuous via segment projection)
            ClosestOnPolyline(Player.transform.position, out Vector3 closestPos, out Vector3 closestTangent, out float tNorm);

            // 2) Outside pose from spline/polyline
            Vector3 targetPosOutside = closestPos;
            Quaternion targetRotOutside = OrientToSplineOutside
                ? Quaternion.LookRotation(closestTangent, FixedUp)
                : transform.rotation;

            // 3) Inside test (robust circle check with hysteresis)
            Vector2 pXZ = new Vector2(Player.transform.position.x, Player.transform.position.z);
            Vector2 cXZ = new Vector2(_centroid.x, _centroid.z);
            float dist = (pXZ - cXZ).magnitude;

            if (!_attachedToPlayer && dist <= Mathf.Max(0f, _meanRadius - EnterMargin))
                _attachedToPlayer = true;
            else if (_attachedToPlayer && dist >= _meanRadius + ExitMargin)
                _attachedToPlayer = false;

            // 4) Apply
            Vector3 targetPos = _attachedToPlayer ? Player.transform.position : targetPosOutside;
            Quaternion targetRot = _attachedToPlayer
                ? (MatchPlayerRotationInside ? Player.transform.rotation : transform.rotation)
                : targetRotOutside;

            if (PositionSmoothTime > 0f)
                transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _vel, PositionSmoothTime);
            else
                transform.position = targetPos;

            if (RotationSpeed > 0f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, RotationSpeed * Time.deltaTime);
            else
                transform.rotation = targetRot;
        }

        void Rebuild()
        {
            int n = Mathf.Clamp(BakeSamples, 32, 2048);
            _pts = new Vector3[n + 1]; // +1 to duplicate first at end for closed loop
            _ts = new float[n + 1];
            _count = n;

            Vector3 centroidSum = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n; // [0,1)
                if (PathContainer.Evaluate(SplineIndex, t, out float3 pos, out _, out _))
                {
                    Vector3 p = (Vector3)pos;
                    _pts[i] = p;
                    _ts[i] = t;
                    centroidSum += new Vector3(p.x, 0f, p.z);
                }
                else
                {
                    _pts[i] = transform.position;
                    _ts[i] = t;
                }
            }
            // Close the loop
            _pts[n] = _pts[0];
            _ts[n] = 1f;

            // Centroid (XZ) and mean radius
            _centroid = new Vector3(centroidSum.x / n, _pts[0].y, centroidSum.z / n);
            float rSum = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = new Vector2(_pts[i].x, _pts[i].z);
                Vector2 c = new Vector2(_centroid.x, _centroid.z);
                rSum += (a - c).magnitude;
            }
            _meanRadius = rSum / n;
        }

        void ClosestOnPolyline(Vector3 point, out Vector3 pos, out Vector3 tangent, out float tNorm)
        {
            pos = _pts[0];
            tangent = Vector3.forward;
            tNorm = 0f;

            float bestDist = float.MaxValue;
            for (int i = 0; i < _count; i++)
            {
                Vector3 a = _pts[i];
                Vector3 b = _pts[i + 1];
                Vector3 ab = b - a;
                float abLen2 = Mathf.Max(1e-9f, ab.sqrMagnitude);

                float u = Mathf.Clamp01(Vector3.Dot(point - a, ab) / abLen2);
                Vector3 q = a + u * ab;
                float d2 = (point - q).sqrMagnitude;

                if (d2 < bestDist)
                {
                    bestDist = d2;
                    pos = q;
                    tangent = ab.sqrMagnitude > 1e-9f ? ab.normalized : tangent;
                    // interpolate normalized t across the segment
                    tNorm = Mathf.Lerp(_ts[i], _ts[i + 1], u);
                }
            }
        }
    }
}