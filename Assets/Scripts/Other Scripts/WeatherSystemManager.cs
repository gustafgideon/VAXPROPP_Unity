using UnityEngine;
using FMODUnity;
using FMOD.Studio;

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

        // Number of rays scales with rain intensity
        int raysThisBurst = Mathf.CeilToInt(raysPerFrame * rainIntensity);

        for (int i = 0; i < raysThisBurst; i++)
        {
            // Random point around the player within rainRadius
            Vector2 randomCircle = Random.insideUnitCircle * rainRadius;
            Vector3 origin = player.position + new Vector3(randomCircle.x, 10f, randomCircle.y);

            // Raycast down
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 50f))
            {
                Debug.DrawLine(origin, hit.point, Color.red, 5f);

                // Determine surface type for FMOD parameter
                float surfaceParam = 0f;
                if (hit.collider.CompareTag("Foliage")) surfaceParam = 1f;
                else if (hit.collider.CompareTag("Metal")) surfaceParam = 2f;
                else if (hit.collider.CompareTag("Water")) surfaceParam = 3f;

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
                Debug.DrawLine(origin, origin + Vector3.down * 50f, Color.blue, 5f);
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
