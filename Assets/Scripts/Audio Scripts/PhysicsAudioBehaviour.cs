using UnityEngine;
using FMODUnity;
using FMOD.Studio;

[RequireComponent(typeof(Rigidbody))]
public class PhysicsAudioBehaviour : MonoBehaviour
{
    [Header("FMOD Events")]
    [SerializeField] private EventReference impactEvent; // One-shot collision sound
    [SerializeField] private EventReference rollEvent;   // Continuous rolling sound

    [Header("Settings")]
    public float minImpactSpeed = 1f;
    public float minRollSpeed = 0.1f;
    public float maxRollSpeed = 10f;
    public float rollStopDelay = 0.2f; // Delay before stopping the roll sound

    private Rigidbody rb;
    private EventInstance rollInstance;
    private bool isRolling = false;
    private float rollStopTimer = 0f;

    private Vector3 smoothedVelocity;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rollInstance = RuntimeManager.CreateInstance(rollEvent);
        RuntimeManager.AttachInstanceToGameObject(rollInstance, transform, rb);
    }

    void OnCollisionEnter(Collision collision)
    {
        float impactSpeed = collision.relativeVelocity.magnitude;

        if (impactSpeed >= minImpactSpeed)
        {
            var impactInstance = RuntimeManager.CreateInstance(impactEvent);
            RuntimeManager.AttachInstanceToGameObject(impactInstance, transform, rb);

            // Map impact speed to parameter (normalized if needed)
            impactInstance.setParameterByName("Impact", impactSpeed);
            impactInstance.start();
            impactInstance.release();
        }
    }

    void Update()
    {
        // Smooth velocity to avoid jitter
        smoothedVelocity = Vector3.Lerp(smoothedVelocity, rb.linearVelocity, Time.deltaTime * 10f);
        float speed = smoothedVelocity.magnitude;

        if (speed > minRollSpeed)
        {
            float normSpeed = Mathf.Clamp01(speed / maxRollSpeed);
            rollInstance.setParameterByName("Roll", normSpeed);

            if (!isRolling)
            {
                rollInstance.start();
                isRolling = true;
            }

            // Reset stop timer if still moving
            rollStopTimer = 0f;
        }
        else if (isRolling)
        {
            // Increment stop timer
            rollStopTimer += Time.deltaTime;
            if (rollStopTimer >= rollStopDelay)
            {
                rollInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                isRolling = false;
            }
        }
    }

    void OnDestroy()
    {
        rollInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        rollInstance.release();
    }
}