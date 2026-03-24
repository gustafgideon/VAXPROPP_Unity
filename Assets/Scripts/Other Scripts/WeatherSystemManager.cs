using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Serialization;

[System.Serializable]
public class Layers
{
    public string layerName;
    public float occlusionEQ = 1.0f;      // EQ adjustment (e.g., 0 = muffled, 1 = bright)
    public float occlusionVolume = 1.0f;  // Volume adjustment (e.g., 0 = silent, 1 = full)
}

public enum ThunderLevel
{
    None = 0,
    Distant = 1,
    Mid = 2,
    Close = 3
}

public class WeatherSystemManager : MonoBehaviour
{
    // Fired when a lightning visual should occur (used by TimeManager).
    // Intensity 0..1 (TimeManager uses this to scale lightning brightness).
    public event System.Action<float> OnThunderTriggered;

    public Transform playerTransform;
    public GameObject playerObject;
    public ParticleSystem rainParticles;

    [Space(10)]
    [Header("Rain Settings")]
    [Range(0f, 1f)] public float rainIntensity = 0.5f;
    public float rainRadius = 8f;
    private int raysPerFrame = 1;
    private float impactRate = 0.1f;
    private float impactTimer = 0f;
    public List<Layers> rainOcclusionLayers = new List<Layers>();
    public List<string> rainImpactTags;

    [Space(10)]
    [Header("Wind Settings")]
    [Range(0f, 1f)] public float windStrength = 0.3f;
    [Range(-180f, 180f)] public float windDegrees = 0f;
    private float windSourceDistance = 5f;
    private float windSourceHeight = 0f;
    private float windParticleStrengthMultiplier = 12f;
    private bool applyWindToParticles = true;
    private float windParticleLerpSpeed = 4f;

    [Space(10)]
    [Header("Thunder Settings")]
    [Tooltip("0=None, 1=Distant, 2=Mid, 3=Close")]
    public ThunderLevel thunderLevel = ThunderLevel.None;

    [Space(10)]
    [Header("FMOD")]
    public EventReference rainLoopEvent;
    public EventReference rainImpactEvent;
    public EventReference windEvent;
    public EventReference windDirectionEvent;
    public EventReference thunderEvent;

    public string rainParameterName = "RainIntensity";
    public string windParameterName = "WindStrength";
    public string windDegreesParameterName = "WindDegrees";
    public string rainOcclusionEQParameterName = "RainOcclusionEQ";
    public string rainOcclusionVolumeParameterName = "RainOcclusionVolume";

    // Only set when GenerateThunder() is called (never on inspector change).
    public string thunderLevelParameterName = "ThunderLevel";

    private EventInstance rainLoopInstance;
    private EventInstance windInstance;
    private EventInstance windDirectionInstance;
    private bool isRainValid, isWindValid, isWind3DValid;

    private float currentOcclusionEQ = 0f;
    private float currentOcclusionVolume = 0f;

    private Vector3 currentAppliedWind = Vector3.zero;
    private Vector3 currentWind3DPosition;
    
    private bool debug = false;

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

        // 3D Wind event
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
        if (playerTransform == null || rainParticles == null) return;

        UpdateRainParticles();
        UpdateRainAudio();
        UpdateWindAudio();
        UpdateWind3DAudio();
        UpdateRainOcclusion();
        HandleRainImpacts();

        if (applyWindToParticles)
            ApplyWindToParticles();

        // Keep rain particles above the player
        rainParticles.transform.position = playerTransform.position + Vector3.up * 5f;
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
        {
            RuntimeManager.StudioSystem.setParameterByName(windParameterName, windStrength);
            RuntimeManager.StudioSystem.setParameterByName(windDegreesParameterName, windDegrees);
        }
    }

    void UpdateWind3DAudio()
    {
        if (isWind3DValid)
        {
            UpdateWind3DPosition();
            windDirectionInstance.set3DAttributes(RuntimeUtils.To3DAttributes(currentWind3DPosition));
        }
    }

    void UpdateWind3DPosition()
    {
        if (playerTransform == null) return;

        float radians = Mathf.Deg2Rad * windDegrees;
        Vector3 windFromDirection = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        currentWind3DPosition = playerTransform.position + windFromDirection * windSourceDistance + Vector3.up * windSourceHeight;
    }

    // Ensures the XZ of a point is within rainRadius around the player.
    Vector3 ClampToRainRadius(Vector3 point)
    {
        if (playerTransform == null) return point;

        Vector3 center = playerTransform.position;
        Vector3 offsetXZ = new Vector3(point.x - center.x, 0f, point.z - center.z);
        float maxR = rainRadius;
        float maxR2 = maxR * maxR;

        if (offsetXZ.sqrMagnitude > maxR2)
        {
            offsetXZ = offsetXZ.normalized * maxR;
            point.x = center.x + offsetXZ.x;
            point.z = center.z + offsetXZ.z;
        }

        return point;
    }

    void HandleRainImpacts()
    {
        if (rainIntensity <= 0f || playerTransform == null) return;

        impactTimer += Time.deltaTime;
        if (impactTimer < impactRate) return;
        impactTimer = 0f;

        Collider[] colliders = Physics.OverlapSphere(playerTransform.position, rainRadius);
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

            Collider target = targetColliders[Random.Range(0, targetColliders.Count)];

            Bounds bounds = target.bounds;
            Vector3 randomPoint = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                bounds.max.y + 5f,
                Random.Range(bounds.min.z, bounds.max.z)
            );

            // NEW: Clamp the random origin to always be within the rain radius.
            randomPoint = ClampToRainRadius(randomPoint);

            if (Physics.Raycast(randomPoint, Vector3.down, out RaycastHit hit, 50f))
            {
                if (!rainImpactTags.Contains(hit.collider.tag)) continue;

                Debug.DrawLine(randomPoint, hit.point, Color.red, 5f);

                float surfaceParam = 0f;
                if (hit.collider.CompareTag("Foliage")) surfaceParam = 1f;
                else if (hit.collider.CompareTag("Metal Solid")) surfaceParam = 2f;
                else if (hit.collider.CompareTag("Metal Hollow")) surfaceParam = 3f;
                else if (hit.collider.CompareTag("Water")) surfaceParam = 4f;
                else if (hit.collider.CompareTag("Wood")) surfaceParam = 5f;

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
        if (playerTransform == null || !isRainValid) return;

        Vector3 rayOrigin = playerTransform.position;
        float checkDistance = 10f;

        float occlusionEQ = 0f;
        float occlusionVolume = 0f;

        bool hitSomething = Physics.Raycast(rayOrigin, Vector3.up, out RaycastHit hit, checkDistance);

        if (hitSomething)
        {
            Debug.DrawLine(rayOrigin, hit.point, Color.green, 0.1f);
            int layer = hit.collider.gameObject.layer;
            string layerName = LayerMask.LayerToName(layer);

            Layers setting = rainOcclusionLayers.Find(s => s.layerName == layerName);
            if (setting != null)
            {
                occlusionEQ = setting.occlusionEQ;
                occlusionVolume = setting.occlusionVolume;
            }
        }
        else
        {
            Debug.DrawLine(rayOrigin, rayOrigin + Vector3.up * checkDistance, Color.red, 0.1f);
        }

        rainLoopInstance.setParameterByName(rainOcclusionEQParameterName, occlusionEQ);
        rainLoopInstance.setParameterByName(rainOcclusionVolumeParameterName, occlusionVolume);

        currentOcclusionEQ = occlusionEQ;
        currentOcclusionVolume = occlusionVolume;
    }

    // Manual thunder trigger. Call from UI Button or click the Inspector context menu in Play Mode.
    [ContextMenu("Generate Thunder")]
    public void GenerateThunder()
    {
        if (thunderLevel == ThunderLevel.None)
        {
            if (debug) Debug.LogWarning("[WeatherSystemManager] ThunderLevel is None. Set a level (Distant/Mid/Close) to generate thunder.");
            return;
        }

        Vector3 pos = playerTransform ? playerTransform.position : Vector3.zero;

        // Trigger lightning visuals for TimeManager
        float intensity =
            (thunderLevel == ThunderLevel.Distant) ? 0.2f :
            (thunderLevel == ThunderLevel.Mid)     ? 0.66f :
                                                     1.0f; // Close
        OnThunderTriggered?.Invoke(intensity);

        // Set FMOD parameter at trigger time (not on inspector change)
        if (!string.IsNullOrEmpty(thunderLevelParameterName))
        {
            RuntimeManager.StudioSystem.setParameterByName(thunderLevelParameterName, (float)thunderLevel);
        }

        // Play thunder event now; any delay is authored in FMOD
        if (!thunderEvent.IsNull)
        {
            RuntimeManager.PlayOneShotAttached(thunderEvent, playerObject);
        }

        if (debug) Debug.Log($"[WeatherSystemManager] Manual thunder generated (level {(int)thunderLevel}).");
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
        if (playerTransform == null) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(playerTransform.position, rainRadius);

        Gizmos.color = Color.yellow;
        Vector3 origin = playerTransform.position + Vector3.up * 2f;
        Vector3 dir = GetBlowingDirectionHorizontal();
        dir = dir.normalized * 2f;
        Gizmos.DrawLine(origin, origin + dir);

        Quaternion look = dir.sqrMagnitude > 0 ? Quaternion.LookRotation(dir) : Quaternion.identity;
        Vector3 right = look * Quaternion.Euler(0, 150, 0) * Vector3.forward * 0.4f;
        Vector3 left = look * Quaternion.Euler(0, -150, 0) * Vector3.forward * 0.4f;
        Gizmos.DrawLine(origin + dir, origin + dir + right);
        Gizmos.DrawLine(origin + dir, origin + dir + left);

        // Show 3D wind source position
        /*Gizmos.color = Color.magenta;
        float radians = Mathf.Deg2Rad * windDegrees;
        Vector3 windFromDirection = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        Vector3 wind3DPos = playerTransform.position + windFromDirection * windSourceDistance + Vector3.up * windSourceHeight;
        Gizmos.DrawWireSphere(wind3DPos, 2f);*/

#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.Label(origin + dir + Vector3.up * 0.2f, $"Wind Direction: {windDegrees:F0}°");

        //UnityEditor.Handles.color = Color.magenta;
        //UnityEditor.Handles.Label(wind3DPos + Vector3.up * 0.5f, "Directional Sound");
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

        if (windDirectionInstance.isValid())
        {
            windDirectionInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            windDirectionInstance.release();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // No automatic FMOD parameter syncing here to avoid unintended triggers.
    }
#endif
}