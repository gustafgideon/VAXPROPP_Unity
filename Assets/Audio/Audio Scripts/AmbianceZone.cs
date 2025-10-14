using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[ExecuteAlways]
[RequireComponent(typeof(AudioSource))]
public class AmbianceZone : MonoBehaviour
{
    [Header("Spline")]
    public SplineContainer splineContainer;
    [Tooltip("Which spline in the container to use (0-based).")]
    public int splineIndex = 0;

    [Header("Position")]
    public PathIndexUnit positionUnits = PathIndexUnit.Distance; // Distance or Normalized
    [Tooltip("Meaning depends on Position Units: meters when Distance, 0..1 when Normalized.")]
    public float positionParam = 0f;
    [Tooltip("Speed in meters/sec (Distance) or normalized units/sec (Normalized).")]
    public float speed = 0f;
    public bool loop = true;

    [Header("Orientation")]
    public bool orientToTangent = false;
    public Vector3 fallbackUp = Vector3.up;

    [Header("Audio")]
    public bool force3DSpatialBlend = true;

    private AudioSource _audio;

    void Reset()
    {
        _audio = GetComponent<AudioSource>();
        if (splineContainer == null)
            splineContainer = GetComponentInParent<SplineContainer>();
    }

    void Awake()
    {
        if (_audio == null) _audio = GetComponent<AudioSource>();
        if (force3DSpatialBlend && _audio != null) _audio.spatialBlend = 1f;
    }

    void OnValidate()
    {
        if (_audio == null) _audio = GetComponent<AudioSource>();
        if (splineContainer == null)
            splineContainer = GetComponentInParent<SplineContainer>();
        splineIndex = Mathf.Max(0, splineIndex);
    }

    void Update()
    {
        if (splineContainer == null) return;
        if (splineIndex < 0 || splineIndex >= splineContainer.Splines.Count) return;

        // Advance along the spline
        if (!Mathf.Approximately(speed, 0f))
        {
            positionParam += speed * Time.deltaTime;
            ClampOrWrapPositionParam();
        }

        // Evaluate position/tangent and apply
        EvaluateAndApply();
    }

    void ClampOrWrapPositionParam()
    {
        var spline = splineContainer.Splines[splineIndex];

        if (positionUnits == PathIndexUnit.Normalized)
        {
            positionParam = loop ? Mathf.Repeat(positionParam, 1f) : Mathf.Clamp01(positionParam);
        }
        else if (positionUnits == PathIndexUnit.Distance)
        {
            // Get total length by converting normalized 1 -> distance
            float totalLength = SplineUtility.ConvertIndexUnit(
                spline, 1f, PathIndexUnit.Normalized, PathIndexUnit.Distance);

            if (loop)
                positionParam = totalLength > 0f ? Mathf.Repeat(positionParam, totalLength) : 0f;
            else
                positionParam = Mathf.Clamp(positionParam, 0f, totalLength);
        }
    }

    float GetNormalizedT()
    {
        var spline = splineContainer.Splines[splineIndex];

        if (positionUnits == PathIndexUnit.Normalized)
            return Mathf.Clamp01(positionParam);

        // Convert distance -> normalized
        return SplineUtility.ConvertIndexUnit(
            spline, positionParam, PathIndexUnit.Distance, PathIndexUnit.Normalized);
    }

    void EvaluateAndApply()
    {
        float t = GetNormalizedT();

        // Evaluate position, tangent, up in world space using the container
        if (splineContainer.Evaluate(splineIndex, t, out float3 pos, out float3 tangent, out float3 up))
        {
            transform.position = (Vector3)pos;

            if (orientToTangent)
            {
                Vector3 fwd = ((Vector3)tangent).normalized;
                Vector3 upVec = up.Equals(float3.zero) ? fallbackUp : (Vector3)up;
                if (fwd.sqrMagnitude > 1e-6f)
                    transform.rotation = Quaternion.LookRotation(fwd, upVec);
            }
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!Application.isPlaying)
            EvaluateAndApply();

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.075f);
    }
#endif
}