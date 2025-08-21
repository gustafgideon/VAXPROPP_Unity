using UnityEngine;
using FMODUnity;
using FMOD.Studio;

[RequireComponent(typeof(Rigidbody))]
public class PhysicsImpactAudio : MonoBehaviour
{
    [Header("FMOD Events")]
    [SerializeField] private EventReference impactEvent; // One-shot collision sound

    [Header("Settings")]
    public float minImpactSpeed = 0.1f;      // Minimum speed to trigger sound
    public float maxImpactSpeed = 10f;       // Speed for max impact intensity
    public float impactCooldown = 0.1f;      // Minimum time between impact sounds
    public float smoothing = 0.8f;           // Low-pass smoothing factor (0-1)

    private Rigidbody rb;
    private float lastImpactTime;
    private float smoothedVelocity;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        smoothedVelocity = 0f;
    }

    void Update()
    {
        // Smooth the velocity to avoid tiny ticks
        float currentSpeed = rb.linearVelocity.magnitude;
        smoothedVelocity = Mathf.Lerp(smoothedVelocity, currentSpeed, 1 - smoothing);
    }

    void OnCollisionEnter(Collision collision)
    {
        TryPlayImpact(collision.relativeVelocity.magnitude);
    }

    void OnCollisionStay(Collision collision)
    {
        // Also check for ongoing collisions with sudden velocity spikes
        TryPlayImpact(collision.relativeVelocity.magnitude);
    }

    private void TryPlayImpact(float collisionSpeed)
    {
        // Time cooldown to avoid spamming
        if (Time.time - lastImpactTime < impactCooldown) return;

        // Use smoothed velocity to avoid tiny ticks
        float impactStrength = Mathf.Max(collisionSpeed, smoothedVelocity);

        if (impactStrength >= minImpactSpeed)
        {
            lastImpactTime = Time.time;

            var impactInstance = RuntimeManager.CreateInstance(impactEvent);
            RuntimeManager.AttachInstanceToGameObject(impactInstance, transform, rb);

            // Map speed to FMOD parameter
            float normalized = Mathf.Clamp01((impactStrength - minImpactSpeed) / (maxImpactSpeed - minImpactSpeed));
            impactInstance.setParameterByName("Impact", normalized);

            impactInstance.start();
            impactInstance.release();
        }
    }
}