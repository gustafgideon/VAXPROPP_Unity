using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class WeatherSystemManager : MonoBehaviour
{
    [Header("Rain Settings")]
    public ParticleSystem rainParticles;
    [Range(0f, 1f)] public float rainIntensity = 0.5f;
    public Transform player;

    [Header("FMOD Audio")]
    public EventReference rainLoopEvent;
    public EventReference windEvent;
    public EventReference thunderEvent;
    public string rainParameterName = "RainIntensity";
    public string windParameterName = "WindStrength";

    private EventInstance rainLoopInstance;
    private EventInstance windInstance;
    private bool isRainValid, isWindValid;

    [Header("Impact Zones")]
    public RainImpactZone[] impactZones;
    public float impactRate = 0.05f; // frequency of surface hits
    private float impactTimer = 0f;

    [Header("Wind Settings")]
    [Range(0f, 1f)] public float windStrength = 0.3f;
    public Vector3 windDirection = Vector3.forward;

    [Header("Thunder Settings")]
    public float thunderChancePerSecond = 0.02f; // chance per second
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
        // Rain
        rainLoopInstance = RuntimeManager.CreateInstance(rainLoopEvent);
        if (rainLoopInstance.isValid())
        {
            isRainValid = true;
            rainLoopInstance.start();
            rainLoopInstance.setVolume(0f);
            RuntimeManager.StudioSystem.setParameterByName(rainParameterName, rainIntensity);
        }

        // Wind
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
        UpdateRainParticles();
        UpdateRainAudio();
        UpdateWindAudio();
        HandleImpactZones();
        HandleThunder();
    }

    void UpdateRainParticles()
    {
        if (rainParticles == null) return;

        // Update emission based on intensity
        var emission = rainParticles.emission;
        if (rainIntensity > 0f)
        {
            emission.rateOverTime = Mathf.Lerp(100f, 800f, rainIntensity);
            if (!rainParticles.isPlaying) rainParticles.Play();
        }
        else
        {
            emission.rateOverTime = 0f;
            if (rainParticles.isPlaying) rainParticles.Stop();
        }

        // Center particles on player
        if (player != null)
            rainParticles.transform.position = player.position;
    }

    void UpdateRainAudio()
    {
        if (!isRainValid) return;
        rainLoopInstance.setVolume(rainIntensity);
        RuntimeManager.StudioSystem.setParameterByName(rainParameterName, rainIntensity);
    }

    void UpdateWindAudio()
    {
        if (!isWindValid) return;
        windInstance.setVolume(windStrength);
        RuntimeManager.StudioSystem.setParameterByName(windParameterName, windStrength);
    }

    void HandleImpactZones()
    {
        if (rainIntensity <= 0f || impactZones.Length == 0) return;

        impactTimer += Time.deltaTime;
        float interval = impactRate / Mathf.Max(rainIntensity, 0.1f);
        if (impactTimer < interval) return;
        impactTimer = 0f;

        // Random impact zone
        RainImpactZone zone = impactZones[Random.Range(0, impactZones.Length)];

        Vector3 randomOffset = new Vector3(
            Random.Range(-zone.size.x * 0.5f, zone.size.x * 0.5f),
            0f,
            Random.Range(-zone.size.z * 0.5f, zone.size.z * 0.5f)
        );

        Vector3 hitPosition = zone.center + randomOffset;

        // Optional: raycast downward to surface
        if (Physics.Raycast(hitPosition + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f))
            hitPosition.y = hit.point.y;

        // Play surface-specific FMOD hit
        switch (zone.surfaceTag)
        {
            case "Water":
                RuntimeManager.PlayOneShot("event:/Rain/Hit_Water", hitPosition);
                break;
            case "Foliage":
                RuntimeManager.PlayOneShot("event:/Rain/Hit_Foliage", hitPosition);
                break;
            case "Metal":
                RuntimeManager.PlayOneShot("event:/Rain/Hit_Metal", hitPosition);
                break;
        }
    }

    void HandleThunder()
    {
        if (Random.value < thunderChancePerSecond * Time.deltaTime)
        {
            // Random distance for thunder
            float distance = Random.Range(thunderDistanceMin, thunderDistanceMax);
            Vector3 direction = Random.onUnitSphere;
            direction.y = 0f; // horizontal plane
            Vector3 thunderPos = player.position + direction.normalized * distance;

            RuntimeManager.PlayOneShot(thunderEvent, thunderPos);
        }
    }

    [System.Serializable]
    public class RainImpactZone
    {
        public string surfaceTag; // "Water", "Foliage", "Metal"
        public Vector3 center;
        public Vector3 size = new Vector3(5f, 0f, 5f); // horizontal spread
    }
}
