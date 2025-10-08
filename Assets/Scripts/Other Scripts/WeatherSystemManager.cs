using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using System.Collections.Generic;

public class WeatherSystemManager : MonoBehaviour
{
    [Header("Rain Settings")]
    public ParticleSystem rainParticles;
    [Range(0f, 1f)] public float rainIntensity = 0.5f;
    public Transform player;
    public float rainRadius = 8f;        // radius around player where rain hits
    public int raysPerFrame = 5;         // max number of rays per impact burst
    public float impactRate = 0.1f;      // seconds between impact bursts
    private float impactTimer = 0f;
    
    [Header("Rain Impact Target Settings")]
    public List<string> impactTags; // List of tags to check for impacts (set in inspector)

    [Header("FMOD Audio")]
    public EventReference rainLoopEvent;
    public EventReference windEvent;
    public EventReference thunderEvent;
    public EventReference rainImpactEvent; // One-shot for impact sounds
    public string rainParameterName = "RainIntensity";
    public string windParameterName = "WindStrength";

    private EventInstance rainLoopInstance;
    private EventInstance windInstance;
    private bool isRainValid, isWindValid;

    [Header("Wind Settings")]
    [Range(0f, 1f)] public float windStrength = 0.3f;

    [Header("Thunder Settings")]
    public float thunderChancePerSecond = 0.02f;
    public float thunderDistanceMin = 30f;
    public float thunderDistanceMax = 200f;

    void Start()
    {
        if (rainParticles == null)
            rainParticles = GetComponentInChildren<ParticleSystem>();

        SetupAudio();
    }

    void SetupAudio()
    {
        // Rain loop
        rainLoopInstance = RuntimeManager.CreateInstance(rainLoopEvent);
        if (rainLoopInstance.isValid())
        {
            isRainValid = true;
            rainLoopInstance.start();
            RuntimeManager.StudioSystem.setParameterByName(rainParameterName, rainIntensity);
        }

        // Wind loop
        windInstance = RuntimeManager.CreateInstance(windEvent);
        if (windInstance.isValid())
        {
            isWindValid = true;
            windInstance.start();
            RuntimeManager.StudioSystem.setParameterByName(windParameterName, windStrength);
        }
    }

    void Update()
    {
        if (player == null || rainParticles == null) return;

        UpdateRainParticles();
        UpdateRainAudio();
        UpdateWindAudio();
        HandleRainImpacts();
        HandleThunder();

        // Keep rain particles above the player
        rainParticles.transform.position = player.position + Vector3.up * 10f;
    }

    void UpdateRainParticles()
    {
        var emission = rainParticles.emission;
        emission.rateOverTime = Mathf.Lerp(50f, 800f, rainIntensity);

        if (rainIntensity > 0f && !rainParticles.isPlaying) rainParticles.Play();
        else if (rainIntensity == 0f && rainParticles.isPlaying) rainParticles.Stop();
    }

    void UpdateRainAudio()
    {
        if (isRainValid)
            RuntimeManager.StudioSystem.setParameterByName(rainParameterName, rainIntensity);
    }

    void UpdateWindAudio()
    {
        if (isWindValid)
            RuntimeManager.StudioSystem.setParameterByName(windParameterName, windStrength);
    }

    void HandleRainImpacts()
    {
        if (rainIntensity <= 0f || player == null) return;

        // Limit bursts based on impactRate
        impactTimer += Time.deltaTime;
        if (impactTimer < impactRate) return;
        impactTimer = 0f;

        // Find all colliders in radius with allowed tags
        Collider[] colliders = Physics.OverlapSphere(player.position, rainRadius);
        List<Collider> targetColliders = new List<Collider>();

        foreach (var col in colliders)
        {
            if (impactTags.Contains(col.tag))
                targetColliders.Add(col);
        }

        int raysThisBurst = Mathf.CeilToInt(raysPerFrame * rainIntensity);

        for (int i = 0; i < raysThisBurst; i++)
        {
            if (targetColliders.Count == 0) break;

            // Pick a random collider
            Collider target = targetColliders[Random.Range(0, targetColliders.Count)];

            // Pick a random point on its bounds (or collider surface)
            Bounds bounds = target.bounds;
            Vector3 randomPoint = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                bounds.max.y + 5f, // start above
                Random.Range(bounds.min.z, bounds.max.z)
            );

            // Raycast down
            if (Physics.Raycast(randomPoint, Vector3.down, out RaycastHit hit, 50f))
            {
                if (!impactTags.Contains(hit.collider.tag)) continue; // Only use if the hit is on a valid tag

                Debug.DrawLine(randomPoint, hit.point, Color.red, 5f);

                // Determine surface type for FMOD parameter
                float surfaceParam = 0f;
                if (hit.collider.CompareTag("Foliage")) surfaceParam = 1f;
                else if (hit.collider.CompareTag("Metal")) surfaceParam = 2f;
                else if (hit.collider.CompareTag("Water")) surfaceParam = 3f;
                else if (hit.collider.CompareTag("Wood")) surfaceParam = 4f;

                // Play 3D impact sound
                if (!rainImpactEvent.IsNull)
                {
                    EventInstance instance = RuntimeManager.CreateInstance(rainImpactEvent);
                    if (instance.isValid())
                    {
                        instance.setParameterByName("RainSurfaceType", surfaceParam);
                        instance.set3DAttributes(RuntimeUtils.To3DAttributes(hit.point));
                        instance.start();
                        instance.release();
                    }
                }
            }
            else
            {
                Debug.DrawLine(randomPoint, randomPoint + Vector3.down * 50f, Color.blue, 5f);
            }
        }
    }

    void HandleThunder()
    {
        if (Random.value < thunderChancePerSecond * Time.deltaTime)
        {
            float distance = Random.Range(thunderDistanceMin, thunderDistanceMax);
            Vector3 direction = Random.onUnitSphere;
            direction.y = 0f;
            Vector3 thunderPos = player.position + direction.normalized * distance;
            RuntimeManager.PlayOneShot(thunderEvent, thunderPos);
        }
    }

    // Draw rain radius in Scene view
    void OnDrawGizmosSelected()
    {
        if (player == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(player.position, rainRadius);
    }
}
