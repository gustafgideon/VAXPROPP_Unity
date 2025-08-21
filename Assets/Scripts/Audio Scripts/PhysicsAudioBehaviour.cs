using UnityEngine;
using FMODUnity;
using FMOD.Studio;

[RequireComponent(typeof(Rigidbody))]
public class PhysicsImpactAudio : MonoBehaviour
{
    [Header("FMOD Events")]
    [SerializeField] private EventReference impactEvent;

    [Header("Settings")]
    public float minImpactSpeed = 0.1f;      // Minimum speed to trigger sound for light objects
    public float maxImpactSpeed = 10f;       // Speed for maximum impact intensity
    public float impactCooldown = 0.05f;     // Minimum time between impacts
    public float smoothing = 0.8f;           // Low-pass smoothing factor (0-1)
    public float massMultiplier = 0.5f;      // Multiplier to scale heavy objects

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
        // Smooth velocity to avoid tiny ticks from rolling
        float currentSpeed = rb.linearVelocity.magnitude;
        smoothedVelocity = Mathf.Lerp(smoothedVelocity, currentSpeed, 1 - smoothing);
    }

    void OnCollisionEnter(Collision collision)
    {
        TryPlayImpact(collision.relativeVelocity.magnitude);
    }

    void OnCollisionStay(Collision collision)
    {
        // Detect sudden spikes even during rolling
        TryPlayImpact(collision.relativeVelocity.magnitude);
    }

    private void TryPlayImpact(float collisionSpeed)
    {
        // Avoid spamming impacts
        if (Time.time - lastImpactTime < impactCooldown) return;

        // Scale impact by mass (heavier objects feel stronger)
        float impactStrength = Mathf.Max(collisionSpeed, smoothedVelocity) * (1f + rb.mass * massMultiplier);

        if (impactStrength >= minImpactSpeed)
        {
            lastImpactTime = Time.time;

            var impactInstance = RuntimeManager.CreateInstance(impactEvent);
            RuntimeManager.AttachInstanceToGameObject(impactInstance, transform, rb);

            // Map speed to FMOD parameter (0–1)
            float normalized = Mathf.Clamp01((impactStrength - minImpactSpeed) / (maxImpactSpeed - minImpactSpeed));
            impactInstance.setParameterByName("Impact", normalized);

            impactInstance.start();
            impactInstance.release();
        }
    }
}