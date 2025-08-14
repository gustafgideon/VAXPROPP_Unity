using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using System.Collections;

public class RainController : MonoBehaviour
{
    [Header("Rain Intensity (0 = Off, 1 = Heavy Rain)")]
    [Range(0f, 1f)]
    public float rainIntensity = 0.5f;

    [Header("Particle System")]
    public ParticleSystem rainParticles;

    [Header("FMOD Rain Audio")]
    public EventReference rainEvent;
    public string globalParameterName = "RainIntensity";
    [Range(0f, 1f)]
    public float masterVolume = 1f;

    [Header("Intensity Settings")]
    public float minRainRate = 100f;
    public float maxRainRate = 800f;

    [Header("Particle Size Settings")]
    [Range(0.1f, 2f)]
    public float particleSize = 1f;
    [Range(0f, 0.5f)]
    public float sizeVariation = 0.2f;

    [Header("Player Detection")]
    public Transform player;
    public float checkInterval = 0.5f;
    public bool enableDebugVisuals = true;

    [Header("Rain Zone Adjustment")]
    public bool useManualRainZone = true;
    public Vector3 manualRainZoneCenter;
    public Vector3 manualRainZoneSize = new Vector3(10, 10, 10);

    [Header("Audio Transition Settings")]
    public float fadeInTime = 1.0f;
    public float fadeOutTime = 2.0f;

    private float lastIntensity = -1f;
    private float lastParticleSize = -1f;
    private float lastSizeVariation = -1f;
    private float lastMasterVolume = -1f;
    private float nextCheckTime = 0f;
    private Coroutine fadeCoroutine = null;
    private bool playerInZone = false;

    private EventInstance rainEventInstance;
    private bool isEventValid = false;

    void Start()
    {
        if (rainParticles == null)
            rainParticles = GetComponentInChildren<ParticleSystem>();

        if (rainParticles == null)
        {
            Debug.LogError("No ParticleSystem found!");
            return;
        }

        if (useManualRainZone && manualRainZoneCenter == Vector3.zero)
            manualRainZoneCenter = transform.position;

        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (player == null)
                Debug.LogWarning("Player reference not found! Please assign it manually in the inspector.");
        }

        SetupRainAudio();
        rainParticles.transform.localScale = Vector3.one;

        UpdateParticleSize();
        UpdateRain();
        playerInZone = false;
        CheckPlayerPosition();
    }

    void SetupRainAudio()
    {
        if (rainEvent.IsNull)
        {
            Debug.LogWarning("Rain FMOD event is not assigned!");
            return;
        }

        rainEventInstance = RuntimeManager.CreateInstance(rainEvent);

        if (rainEventInstance.isValid())
        {
            isEventValid = true;
            rainEventInstance.start();
            rainEventInstance.setVolume(0f); // start muted
            RuntimeManager.StudioSystem.setParameterByName(globalParameterName, rainIntensity);
        }
        else
        {
            Debug.LogError("Failed to create FMOD event instance.");
        }
    }

    void Update()
    {
        if (Time.time >= nextCheckTime)
        {
            CheckPlayerPosition();
            nextCheckTime = Time.time + checkInterval;
        }

        if (rainIntensity != lastIntensity)
        {
            UpdateRain();
            if (isEventValid)
                RuntimeManager.StudioSystem.setParameterByName(globalParameterName, rainIntensity);
            lastIntensity = rainIntensity;
        }

        if (masterVolume != lastMasterVolume)
        {
            UpdateRainVolume();
            lastMasterVolume = masterVolume;
        }

        if (particleSize != lastParticleSize || sizeVariation != lastSizeVariation)
        {
            UpdateParticleSize();
            lastParticleSize = particleSize;
            lastSizeVariation = sizeVariation;
        }
    }

    void CheckPlayerPosition()
    {
        if (player == null) return;

        Vector3 zoneCenter = useManualRainZone ? manualRainZoneCenter : transform.position;
        Vector3 zoneSize = useManualRainZone ? manualRainZoneSize : rainParticles.shape.scale;
        Vector3 halfSize = zoneSize * 0.5f;

        Vector3 pos = player.position;
        bool isInside = pos.x >= (zoneCenter.x - halfSize.x) && pos.x <= (zoneCenter.x + halfSize.x) &&
                        pos.y >= (zoneCenter.y - halfSize.y) && pos.y <= (zoneCenter.y + halfSize.y) &&
                        pos.z >= (zoneCenter.z - halfSize.z) && pos.z <= (zoneCenter.z + halfSize.z);

        if (isInside != playerInZone)
        {
            playerInZone = isInside;
            if (playerInZone)
                FadeAudioVolume(masterVolume, fadeInTime);
            else
                FadeAudioVolume(0f, fadeOutTime);
        }
    }

    void FadeAudioVolume(float targetVolume, float fadeTime)
    {
        if (!isEventValid) return;
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeAudioVolumeCoroutine(targetVolume, fadeTime));
    }

    IEnumerator FadeAudioVolumeCoroutine(float targetVolume, float fadeTime)
    {
        rainEventInstance.getVolume(out float startVolume);
        float startTime = Time.time;

        while (Time.time < startTime + fadeTime)
        {
            float t = (Time.time - startTime) / fadeTime;
            float smoothT = t * t * (3f - 2f * t);
            rainEventInstance.setVolume(Mathf.Lerp(startVolume, targetVolume, smoothT));
            yield return null;
        }

        rainEventInstance.setVolume(targetVolume);
        fadeCoroutine = null;
    }

    void UpdateRain()
    {
        var emission = rainParticles.emission;
        if (rainIntensity > 0f)
        {
            emission.rateOverTime = Mathf.Lerp(minRainRate, maxRainRate, rainIntensity);
            if (!rainParticles.isPlaying) rainParticles.Play();
        }
        else
        {
            emission.rateOverTime = 0f;
            if (rainParticles.isPlaying) rainParticles.Stop();
        }
    }

    void UpdateRainVolume()
    {
        if (!isEventValid) return;
        rainEventInstance.setVolume(masterVolume);
    }

    void UpdateParticleSize()
    {
        var main = rainParticles.main;
        if (sizeVariation > 0f)
            main.startSize = new ParticleSystem.MinMaxCurve(
                Mathf.Max(particleSize - sizeVariation, 0.1f),
                particleSize + sizeVariation
            );
        else
            main.startSize = particleSize;
    }

    public void SetRainIntensity(float intensity)
    {
        rainIntensity = Mathf.Clamp01(intensity);
        UpdateRain();
        if (isEventValid)
            RuntimeManager.StudioSystem.setParameterByName(globalParameterName, rainIntensity);
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        UpdateRainVolume();
    }

    void ReleaseRainAudio()
    {
        if (isEventValid)
        {
            rainEventInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            rainEventInstance.release();
            isEventValid = false;
        }
    }

    void OnDestroy()
    {
        ReleaseRainAudio();
    }

    void OnDrawGizmos()
    {
        if (!enableDebugVisuals) return;

        Gizmos.color = Color.yellow;
        if (useManualRainZone)
            Gizmos.DrawWireCube(manualRainZoneCenter, manualRainZoneSize);
        else if (rainParticles != null)
            Gizmos.DrawWireCube(transform.position, rainParticles.shape.scale);

        if (player != null)
        {
            Gizmos.color = playerInZone ? Color.green : Color.red;
            Vector3 center = useManualRainZone ? manualRainZoneCenter : transform.position;
            Gizmos.DrawLine(center, player.position);
            Gizmos.DrawSphere(player.position, 0.5f);
        }
    }
}