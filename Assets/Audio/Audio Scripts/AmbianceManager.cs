using System.Collections;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public enum Location
{
    Forest,
    Desert,
    Plant
}

public class AmbianceManager : MonoBehaviour
{
    public static AmbianceManager Instance { get; private set; }

    [Header("Ambiance Emitters")]
    [SerializeField] private StudioEventEmitter forestAmbianceEmitter;
    [SerializeField] private StudioEventEmitter desertAmbianceEmitter;
    [SerializeField] private StudioEventEmitter plantAmbianceEmitter;

    [Header("Fade Settings")]
    [SerializeField] private float fadeTime = 2f; // matches your old script's fadeTime

    [Header("Weather Parameters")]
    [Tooltip("Global FMOD parameter used by WeatherSystemManager to switch rain sounds (use FMOD labels mapped to these numeric values).")]
    public string rainLocationParameterName = "RainLocation";

    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;

    private StudioEventEmitter emitter;
    private StudioEventEmitter currentlyPlaying;
    private GameObject player;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        player = GameObject.FindWithTag("Player");
        if (!player && debugLogging)
            Debug.LogWarning("AmbianceManager: No player found!");
    }

    private void Update()
    {
        if (player != null)
            transform.position = player.transform.position;
    }

    private void GetLocation(Location location)
    {
        switch (location)
        {
            case Location.Forest: emitter = forestAmbianceEmitter; break;
            case Location.Desert: emitter = desertAmbianceEmitter; break;
            case Location.Plant: emitter = plantAmbianceEmitter; break;
            default: emitter = null; break;
        }
    }

    #region Ambiance Control

    // This follows your old script behavior (fade loop that directly sets volumes each frame)
    public void ChangeAmbiance(Location newLocation)
    {
        GetLocation(newLocation);
        if (emitter == null)
        {
            if (debugLogging) Debug.LogWarning($"AmbianceManager: No emitter for {newLocation}");
            return;
        }

        // If there is a different currently playing emitter, crossfade it out while fading this in
        if (currentlyPlaying != null && currentlyPlaying != emitter)
        {
            StartCoroutine(CrossFade(currentlyPlaying, emitter));
        }
        else
        {
            // Nothing playing or same emitter: fade in this emitter
            StartCoroutine(FadeIn(emitter));
        }

        currentlyPlaying = emitter;

        // Ensure rain/global parameters match this location (if used)
        SetRainLocation(newLocation);

        if (debugLogging) Debug.Log($"AmbianceManager: Changed ambiance to {newLocation}");
    }

    // Fade out currently playing emitter to silence (useful when leaving all zones)
    public void FadeOutCurrent(float duration = -1f)
    {
        if (currentlyPlaying == null) return;
        if (duration <= 0f) duration = fadeTime;
        StartCoroutine(FadeOut(currentlyPlaying, duration));
        currentlyPlaying = null;
        if (debugLogging) Debug.Log("AmbianceManager: Fading out current ambiance to silence");
    }

    public void PlayAudio(Location location)
    {
        GetLocation(location);
        if (emitter != null && !IsEmitterActive(emitter))
        {
            emitter.Play();
            currentlyPlaying = emitter;
            if (debugLogging) Debug.Log($"PlayAudio: {location}");
        }
    }

    public void StopAudio(Location location)
    {
        GetLocation(location);
        if (emitter != null && IsEmitterActive(emitter))
        {
            emitter.Stop();
            if (currentlyPlaying == emitter) currentlyPlaying = null;
            if (debugLogging) Debug.Log($"StopAudio: {location}");
        }
    }

    #endregion

    #region Fade Logic (matches your working, old implementation)

    // Crossfade: fade out old while fading in new (uses fadeTime)
    private IEnumerator CrossFade(StudioEventEmitter oldEmitter, StudioEventEmitter newEmitter)
    {
        // Start the new emitter if not running (old script did this)
        if (!IsEmitterActive(newEmitter))
            newEmitter.Play();

        float timer = 0f;
        while (timer < fadeTime)
        {
            timer += Time.deltaTime;
            float progress = timer / fadeTime;

            // Fade out old (1 -> 0)
            oldEmitter.EventInstance.setVolume(1f - progress);

            // Fade in new (0 -> 1)
            newEmitter.EventInstance.setVolume(progress);

            yield return null;
        }

        // Ensure volumes precise at the end
        oldEmitter.EventInstance.setVolume(0f);
        newEmitter.EventInstance.setVolume(1f);

        // Stop old emitter as in your old script
        oldEmitter.Stop();
    }

    // Fade in from silence
    private IEnumerator FadeIn(StudioEventEmitter targetEmitter)
    {
        if (!IsEmitterActive(targetEmitter))
            targetEmitter.Play();

        float timer = 0f;
        while (timer < fadeTime)
        {
            timer += Time.deltaTime;
            float progress = timer / fadeTime;
            targetEmitter.EventInstance.setVolume(progress);
            yield return null;
        }
        targetEmitter.EventInstance.setVolume(1f);
    }

    // Fade out to silence
    private IEnumerator FadeOut(StudioEventEmitter emitterToFade, float duration)
    {
        if (!IsEmitterActive(emitterToFade)) yield break;

        // get starting volume if available; fallback to 1f if getVolume not available
        float startVolume = 1f;
        FMOD.RESULT volRes = emitterToFade.EventInstance.getVolume(out float currentVol);
        if (volRes == FMOD.RESULT.OK) startVolume = currentVol;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            emitterToFade.EventInstance.setVolume(Mathf.Lerp(startVolume, 0f, t / duration));
            yield return null;
        }

        emitterToFade.EventInstance.setVolume(0f);
        emitterToFade.Stop();
    }

    #endregion

    #region Helper: robust "IsActive" check

    // Use IsActive when it works (older setups). Also fallback to playback state check to be robust.
    private bool IsEmitterActive(StudioEventEmitter e)
    {
        if (e == null) return false;

        bool isActive = false;

        // Try the simple property first (this was used in your working version)
        try
        {
            isActive = e.IsActive;
        }
        catch
        {
            isActive = false;
        }

        // If the property is false, double-check playback state (makes it robust across FMOD/Unity versions)
        if (!isActive)
        {
            var result = e.EventInstance.getPlaybackState(out PLAYBACK_STATE state);
            if (result == FMOD.RESULT.OK)
            {
                isActive = (state == PLAYBACK_STATE.PLAYING || state == PLAYBACK_STATE.STARTING);
            }
        }

        return isActive;
    }

    #endregion

    #region Rain System Integration

    public void SetRainLocation(Location location)
    {
        if (string.IsNullOrEmpty(rainLocationParameterName)) return;

        float mapped = MapLocationToRainValue(location);
        RuntimeManager.StudioSystem.setParameterByName(rainLocationParameterName, mapped);
        if (debugLogging) Debug.Log($"SetRainLocation: {location} -> {mapped}");
    }

    private float MapLocationToRainValue(Location location)
    {
        switch (location)
        {
            case Location.Forest: return 0f;
            case Location.Desert: return 1f;
            case Location.Plant: return 2f;
            default: return 0f;
        }
    }

    #endregion
}
