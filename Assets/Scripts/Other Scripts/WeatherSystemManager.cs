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
    //public Vector3 windDirection = Vector3.forward;

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
        
        if (player == null || rainParticles == null)
            return;

        // Position the rain particles above the player
        Vector3 rainOffset = new Vector3(0f, 10f, 0f); // 10 units above player
        rainParticles.transform.position = player.position + rainOffset;
    }

    void UpdateRainParticles()
    {
        if (rainParticles == null) return;

        var emission = rainParticles.emission;

        // Adjust particle rate: light drizzle less visible, heavy rain stronger
        float particleRate = Mathf.Lerp(50f, 800f, rainIntensity); 
        emission.rateOverTime = particleRate;

        if (rainIntensity > 0f)
        {
            if (!rainParticles.isPlaying) rainParticles.Play();
        }
        else
        {
            if (rainParticles.isPlaying) rainParticles.Stop();
        }
    }

    void UpdateRainAudio()
    {
        if (!isRainValid || player == null) return;

        
        RuntimeManager.StudioSystem.setParameterByName(rainParameterName, rainIntensity);
    }

    void UpdateWindAudio()
    {
        if (!isWindValid) return;

        // Send strength
        RuntimeManager.StudioSystem.setParameterByName(windParameterName, windStrength);

        // Send direction as degrees (-180 to 180)
       /* float angle = Mathf.Atan2(windDirection.x, windDirection.z) * Mathf.Rad2Deg;
        RuntimeManager.StudioSystem.setParameterByName("Direction", angle);*/
    }
    

    
    void HandleImpactZones()
    {
        if (rainIntensity <= 0f || impactZones.Length == 0) return;

        impactTimer += Time.deltaTime;
        float interval = impactRate / Mathf.Max(rainIntensity, 0.1f);
        if (impactTimer < interval) return;
        impactTimer = 0f;

        RainImpactZone zone = impactZones[Random.Range(0, impactZones.Length)];

        Vector3 randomOffset = new Vector3(
            Random.Range(-zone.size.x * 0.5f, zone.size.x * 0.5f),
            0f,
            Random.Range(-zone.size.z * 0.5f, zone.size.z * 0.5f)
        );

        Vector3 hitPosition = zone.center + randomOffset;

        // Optional: raycast downward
        /*if (Physics.Raycast(hitPosition + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f))
            hitPosition.y = hit.point.y;*/

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
            float distance = Random.Range(thunderDistanceMin, thunderDistanceMax);
            Vector3 direction = Random.onUnitSphere;
            direction.y = 0f;
            Vector3 thunderPos = player.position + direction.normalized * distance;

            RuntimeManager.PlayOneShot(thunderEvent, thunderPos);
        }
    }

    [System.Serializable]
    public class RainImpactZone
    {
        public string surfaceTag; // "Water", "Foliage", "Metal"
        public Vector3 center;
        public Vector3 size = new Vector3(5f, 0f, 5f);
    }
}

