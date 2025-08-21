using UnityEngine;
using FMODUnity;
using FMOD.Studio;

[RequireComponent(typeof(Rigidbody))]
public class PhysicsAudioBehaviour : MonoBehaviour
{
    [Header("FMOD Event")]
    [SerializeField] private EventReference impactEvent;

    [Header("Impact Settings")]
    public float minImpactSpeed = 0.1f;    // Minimum collision speed to trigger sound
    public float maxImpactSpeed = 10f;     // Speed corresponding to max parameter
    public float minMotionThreshold = 0.05f; // Ignore impacts when object is nearly stationary
    public float impactCooldown = 0.08f;
    public AnimationCurve impactCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.3f, 0f),
        new Keyframe(0.5f, 0.3f),
        new Keyframe(1f, 1f)
    );

    private Rigidbody rb;
    private float lastImpactTime = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();
    }

    void OnCollisionEnter(Collision collision)
    {
        TriggerImpact(collision);
    }

    private void TriggerImpact(Collision collision)
    {
        float currentTime = Time.time;

        if (currentTime - lastImpactTime < impactCooldown)
            return;

        float speed = collision.relativeVelocity.magnitude;

        // Ignore if object is barely moving
        if (rb.linearVelocity.magnitude < minMotionThreshold)
            return;

        if (speed >= minImpactSpeed)
        {
            float normSpeed = Mathf.Clamp01(speed / maxImpactSpeed);
            float curveSpeed = impactCurve.Evaluate(normSpeed);

            if (curveSpeed <= 0f)
                return;

            var impactInstance = RuntimeManager.CreateInstance(impactEvent);
            RuntimeManager.AttachInstanceToGameObject(impactInstance, transform, rb);
            impactInstance.setParameterByName("Impact", curveSpeed);
            impactInstance.start();
            impactInstance.release();

            lastImpactTime = currentTime;
        }
    }
}