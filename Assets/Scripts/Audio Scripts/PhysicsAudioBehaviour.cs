using UnityEngine;
using FMODUnity;
using FMOD.Studio;

[RequireComponent(typeof(Rigidbody))]
public class PhysicsAudioBehaviour : MonoBehaviour
{
    [Header("FMOD Events")]
    [SerializeField] private EventReference impactEvent; // One-shot collision sound

    [Header("Settings")]
    [Tooltip("Minimum relative collision speed to trigger impact sound.")]
    public float minImpactSpeed = 0.3f; 
    [Tooltip("Maximum speed mapped to impact parameter.")]
    public float maxImpactSpeed = 5f;
    [Tooltip("Minimum time between impact sounds in seconds.")]
    public float impactCooldown = 0.05f;

    private Rigidbody rb;
    private float lastImpactTime = -1f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void OnCollisionEnter(Collision collision)
    {
        float impactSpeed = collision.relativeVelocity.magnitude;

        // Only trigger if above threshold and cooldown has passed
        if (impactSpeed >= minImpactSpeed && Time.time - lastImpactTime > impactCooldown)
        {
            lastImpactTime = Time.time;

            var impactInstance = RuntimeManager.CreateInstance(impactEvent);
            RuntimeManager.AttachInstanceToGameObject(impactInstance, transform, rb);

            // Normalize impact speed to 0-1 range for FMOD parameter
            float normImpact = Mathf.Clamp01(impactSpeed / maxImpactSpeed);
            impactInstance.setParameterByName("Impact", normImpact);

            impactInstance.start();
            impactInstance.release();
        }
    }
}