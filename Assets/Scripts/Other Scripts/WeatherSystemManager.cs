using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using System.Collections.Generic;

[System.Serializable]
public class Layers
{
    public string layerName;
    public float occlusionEQ = 1.0f;      // EQ adjustment (e.g., 0 = muffled, 1 = bright)
    public float occlusionVolume = 1.0f;  // Volume adjustment (e.g., 0 = silent, 1 = full)
}

public class WeatherSystemManager : MonoBehaviour
{
    public Transform player;
    public ParticleSystem rainParticles;
    
    [Space(10)]
    [Header("Rain Settings")]
    [Range(0f, 1f)] public float rainIntensity = 0.5f;
    public float rainRadius = 8f;        // radius around player where rain hits
    private int raysPerFrame = 1;         // max number of rays per impact burst
    private float impactRate = 0.1f;      // seconds between impact bursts
    private float impactTimer = 0f;
    public List<Layers> rainOcclusionLayers = new List<Layers>();
    public List<string> rainImpactTags; // List of tags to check for impacts (set in inspector)
    
    [Space(10)]
    [Header("Wind Settings")]
    [Range(0f, 1f)] public float windStrength = 0.3f;
    [Range(-180f, 180f)] public float windDegrees = 0f;
    private float windSourceDistance = 5f; // Distance from player to place the 3D wind source
    private float windSourceHeight = 0f; // Height offset for 3D wind source
    private float windParticleStrengthMultiplier = 12f;
    private bool applyWindToParticles = true;
    private float windParticleLerpSpeed = 4f;

    [Space(10)]
    [Header("Thunder Settings")]
    public float thunderChancePerSecond = 0.02f;
    public float thunderDistanceMin = 30f;
    public float thunderDistanceMax = 200f;
    
    [Space(10)]
    [Header("FMOD")]
    public EventReference rainLoopEvent;
    public EventReference rainImpactEvent; // One-shot for impact sounds
    public EventReference windEvent; // Your existing 2D wind event (unchanged)
    public EventReference windDirectionEvent; // NEW: 3D positioned wind event
    public EventReference thunderEvent;
    public string rainParameterName = "RainIntensity";
    public string windParameterName = "WindStrength";
    public string windDegreesParameterName = "WindDegrees";
    public string rainOcclusionEQParameterName = "RainOcclusionEQ";
    public string rainOcclusionVolumeParameterName = "RainOcclusionVolume";

    private EventInstance rainLoopInstance;
    private EventInstance windInstance; // Your existing wind event (unchanged)
    private EventInstance windDirectionInstance; // NEW: 3D wind instance
    private bool isRainValid, isWindValid, isWind3DValid;
    
    // Store current occlusion values to pass to impact events
    private float currentOcclusionEQ = 0f;
    private float currentOcclusionVolume = 0f;

    // Internal state for smoothly applying wind to particles
    private Vector3 currentAppliedWind = Vector3.zero;
    
    // NEW: Wind 3D positioning
    private Vector3 currentWind3DPosition;

    void Start()
    {
        if (rainParticles == null)
            rainParticles = GetComponentInChildren<ParticleSystem>();

        SetupAudio();
    }

    void SetupAudio()
    {
        // Rain loop (unchanged)
        rainLoopInstance = RuntimeManager.CreateInstance(rainLoopEvent);
        if (rainLoopInstance.isValid())
        {
            isRainValid = true;
            rainLoopInstance.start();
            RuntimeManager.StudioSystem.setParameterByName(rainParameterName, rainIntensity);
        }

        // Wind loop (unchanged)
        windInstance = RuntimeManager.CreateInstance(windEvent);
        if (windInstance.isValid())
        {
            isWindValid = true;
            windInstance.start();
            RuntimeManager.StudioSystem.setParameterByName(windParameterName, windStrength);
        }

        // NEW: 3D Wind event
        windDirectionInstance = RuntimeManager.CreateInstance(windDirectionEvent);
        if (windDirectionInstance.isValid())
        {
            isWind3DValid = true;
            windDirectionInstance.start();
            UpdateWind3DPosition();
            windDirectionInstance.set3DAttributes(RuntimeUtils.To3DAttributes(currentWind3DPosition));
        }
    }

    void Update()
    {
        if (player == null || rainParticles == null) return;

        UpdateRainParticles();
        UpdateRainAudio();
        UpdateWindAudio();
        UpdateWind3DAudio(); // NEW: Update 3D wind
        UpdateRainOcclusion();
        HandleRainImpacts();
        HandleThunder();

        // Apply wind to the particle system so rain looks like it's blowing
        if (applyWindToParticles)
            ApplyWindToParticles();

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

    void UpdateWindAudio() // Unchanged - works exactly as before
    {
        if (isWindValid)
        {
            RuntimeManager.StudioSystem.setParameterByName(windParameterName, windStrength);
            RuntimeManager.StudioSystem.setParameterByName(windDegreesParameterName, windDegrees);
        }
    }

    // NEW: Update 3D wind audio
    void UpdateWind3DAudio()
    {
        if (isWind3DValid)
        {
            // Update position based on wind direction
            UpdateWind3DPosition();
            windDirectionInstance.set3DAttributes(RuntimeUtils.To3DAttributes(currentWind3DPosition));
            
            // Set wind strength parameter
            windDirectionInstance.setParameterByName(windParameterName, windStrength);
        }
    }

    // NEW: Calculate 3D wind position
    void UpdateWind3DPosition()
    {
        if (player == null) return;

        // Calculate the position where wind is coming FROM
        float radians = Mathf.Deg2Rad * windDegrees;
        Vector3 windFromDirection = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        
        // Position the 3D wind source at the origin point (where wind comes from)
        currentWind3DPosition = player.position + windFromDirection * windSourceDistance + Vector3.up * windSourceHeight;
    }

    // All the rest of your methods stay exactly the same...
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
            if (rainImpactTags.Contains(col.tag))
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
                if (!rainImpactTags.Contains(hit.collider.tag)) continue; // Only use if the hit is on a valid tag

                Debug.DrawLine(randomPoint, hit.point, Color.red, 5f);

                // Determine surface type for FMOD parameter
                float surfaceParam = 0f;
                if (hit.collider.CompareTag("Foliage")) surfaceParam = 1f;
                else if (hit.collider.CompareTag("Metal Solid")) surfaceParam = 2f;
                else if (hit.collider.CompareTag("Metal Hollow")) surfaceParam = 3f;
                else if (hit.collider.CompareTag("Water")) surfaceParam = 4f;
                else if (hit.collider.CompareTag("Wood")) surfaceParam = 5f;

                // Play 3D impact sound
                if (!rainImpactEvent.IsNull)
                {
                    EventInstance instance = RuntimeManager.CreateInstance(rainImpactEvent);
                    if (instance.isValid())
                    {
                        instance.setParameterByName("RainSurfaceType", surfaceParam);
                        instance.setParameterByName(rainOcclusionEQParameterName, currentOcclusionEQ);
                        instance.setParameterByName(rainOcclusionVolumeParameterName, currentOcclusionVolume);
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
    
    void UpdateRainOcclusion()
    {
        if (player == null || !isRainValid) return;

        Vector3 rayOrigin = player.position; // Player head height
        float checkDistance = 3f; // How high to check for cover

        float occlusionEQ = 0f;     // Default: no EQ occlusion
        float occlusionVolume = 0f; // Default: no volume occlusion

        // Draw the ray for debugging (green if hit, red if not)
        RaycastHit hit;
        bool hitSomething = Physics.Raycast(rayOrigin, Vector3.up, out hit, checkDistance);

        if (hitSomething)
        {
            // Ray hit something above
            Debug.DrawLine(rayOrigin, hit.point, Color.green, 0.1f); // Short duration
            
            // Get the layer name of the hit object
            int layer = hit.collider.gameObject.layer;
            string layerName = LayerMask.LayerToName(layer);
            
            // Find matching occlusion setting by layer name
            Layers setting = rainOcclusionLayers.Find(s => s.layerName == layerName);
            if (setting != null)
            {
                occlusionEQ = setting.occlusionEQ;
                occlusionVolume = setting.occlusionVolume;
            }
        }
        else
        {
            // Ray did not hit anything
            Debug.DrawLine(rayOrigin, rayOrigin + Vector3.up * checkDistance, Color.red, 0.1f); // Short duration
        }

        // Update rain loop with both occlusion parameters
        rainLoopInstance.setParameterByName(rainOcclusionEQParameterName, occlusionEQ);
        rainLoopInstance.setParameterByName(rainOcclusionVolumeParameterName, occlusionVolume);
        
        // Store current values for impact events
        currentOcclusionEQ = occlusionEQ;
        currentOcclusionVolume = occlusionVolume;
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

    Vector3 GetBlowingDirectionHorizontal()
    {
        float radians = Mathf.Deg2Rad * windDegrees;
        Vector3 fromVec = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        Vector3 blowingDir = -fromVec;
        blowingDir.y = 0f;
        if (blowingDir.sqrMagnitude < 0.0001f) blowingDir = Vector3.right;
        blowingDir.Normalize();
        return blowingDir;
    }

    void ApplyWindToParticles()
    {
        if (rainParticles == null) return;

        Vector3 dir = GetBlowingDirectionHorizontal();
        Vector3 desiredWind = dir * (windStrength * windParticleStrengthMultiplier);
        currentAppliedWind = Vector3.Lerp(currentAppliedWind, desiredWind, Time.deltaTime * Mathf.Max(1f, windParticleLerpSpeed));

        var vel = rainParticles.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;

        vel.x = new ParticleSystem.MinMaxCurve(currentAppliedWind.x);
        vel.y = new ParticleSystem.MinMaxCurve(currentAppliedWind.y);
        vel.z = new ParticleSystem.MinMaxCurve(currentAppliedWind.z);
    }

    void OnDrawGizmosSelected()
    {
        if (player == null) return;
        
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(player.position, rainRadius);

        Gizmos.color = Color.yellow;
        Vector3 origin = player.position + Vector3.up * 2f;
        Vector3 dir = GetBlowingDirectionHorizontal();
        dir = dir.normalized * 2f;
        Gizmos.DrawLine(origin, origin + dir);
        
        Quaternion look = dir.sqrMagnitude > 0 ? Quaternion.LookRotation(dir) : Quaternion.identity;
        Vector3 right = look * Quaternion.Euler(0,150,0) * Vector3.forward * 0.4f;
        Vector3 left = look * Quaternion.Euler(0,-150,0) * Vector3.forward * 0.4f;
        Gizmos.DrawLine(origin + dir, origin + dir + right);
        Gizmos.DrawLine(origin + dir, origin + dir + left);

        // NEW: Show 3D wind source position
        Gizmos.color = Color.magenta;
        float radians = Mathf.Deg2Rad * windDegrees;
        Vector3 windFromDirection = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        Vector3 wind3DPos = player.position + windFromDirection * windSourceDistance + Vector3.up * windSourceHeight;
        Gizmos.DrawWireSphere(wind3DPos, 2f);

        #if UNITY_EDITOR
        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.Label(origin + dir + Vector3.up * 0.2f, $"Wind Direction: {windDegrees:F0}°");
        
        UnityEditor.Handles.color = Color.magenta;
        UnityEditor.Handles.Label(wind3DPos + Vector3.up * 0.5f, "Directional Sound");
        #endif
    }

    void OnDestroy()
    {
        if (rainLoopInstance.isValid())
        {
            rainLoopInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            rainLoopInstance.release();
        }
        
        if (windInstance.isValid())
        {
            windInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            windInstance.release();
        }
        
        // NEW: Clean up 3D wind
        if (windDirectionInstance.isValid())
        {
            windDirectionInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            windDirectionInstance.release();
        }
    }
}