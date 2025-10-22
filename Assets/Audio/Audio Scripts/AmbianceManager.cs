using System.Collections;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using UnityEngine.Serialization;

public enum Location
{
    Forest,
    Desert,
    Plant
}

public class AmbianceManager : MonoBehaviour
{
    [System.Serializable]
    private class AmbianceEmitters
    {
        public StudioEventEmitter Forest;
        public StudioEventEmitter Desert;
        public StudioEventEmitter Plant;
    }

    public static AmbianceManager Instance { get; private set; }
    
    [FormerlySerializedAs("volumeCrossFadeTime")]
    [Space(20)]
    [FormerlySerializedAs("fadeTime")] [SerializeField] private float crossFadeTime = 2f;
    
    [Header("Ambiance Emitters")]
    [SerializeField] private AmbianceEmitters ambianceEmitters = new AmbianceEmitters();

    // Hidden legacy fields kept for safe migration of existing references
    [HideInInspector, FormerlySerializedAs("forestAmbianceEmitter")]
    [SerializeField] private StudioEventEmitter legacy_forestAmbianceEmitter;
    [HideInInspector, FormerlySerializedAs("desertAmbianceEmitter")]
    [SerializeField] private StudioEventEmitter legacy_desertAmbianceEmitter;
    [HideInInspector, FormerlySerializedAs("plantAmbianceEmitter")]
    [SerializeField] private StudioEventEmitter legacy_plantAmbianceEmitter;
    
    [Header("FMOD")]
    [Tooltip("Global FMOD parameter used by WeatherSystemManager to switch rain sounds (use FMOD labels mapped to these numeric values).")]
    public string rainLocationParameterName = "RainLocation";

    [Tooltip("Global FMOD parameter used by WeatherSystemManager to switch wind sounds (use FMOD labels mapped to these numeric values).")]
    public string windLocationParameterName = "WindLocation";
    
    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;

    private StudioEventEmitter emitter;
    private StudioEventEmitter currentlyPlaying;
    private GameObject player;

    // Track active crossfade to avoid conflicting stops/fades
    private Coroutine crossfadeRoutine;
    private StudioEventEmitter crossfadeOldEmitter;

    public bool IsCrossfading => crossfadeRoutine != null;

    private void OnValidate()
    {
        // Migrate old flat fields to the new foldout group automatically
        if (ambianceEmitters == null)
            ambianceEmitters = new AmbianceEmitters();

        if (ambianceEmitters.Forest == null && legacy_forestAmbianceEmitter != null)
            ambianceEmitters.Forest = legacy_forestAmbianceEmitter;

        if (ambianceEmitters.Desert == null && legacy_desertAmbianceEmitter != null)
            ambianceEmitters.Desert = legacy_desertAmbianceEmitter;

        if (ambianceEmitters.Plant == null && legacy_plantAmbianceEmitter != null)
            ambianceEmitters.Plant = legacy_plantAmbianceEmitter;
    }

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
            case Location.Forest: emitter = ambianceEmitters?.Forest; break;
            case Location.Desert: emitter = ambianceEmitters?.Desert; break;
            case Location.Plant:  emitter = ambianceEmitters?.Plant;  break;
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

        // If the same emitter is already active, do nothing to avoid restarting/jumping
        if (currentlyPlaying == emitter && IsEmitterActive(emitter))
        {
            // Still keep weather/global params in sync (no audio restart)
            SetRainLocation(newLocation);
            SetWindLocation(newLocation);
            if (debugLogging) Debug.Log($"AmbianceManager: {newLocation} already playing; skipping restart.");
            return;
        }

        // If there is a different currently playing emitter, crossfade it out while fading this in
        if (currentlyPlaying != null && currentlyPlaying != emitter)
        {
            // Cancel any in-flight crossfade first (very rare edge case of rapid changes)
            if (crossfadeRoutine != null) StopCoroutine(crossfadeRoutine);
            crossfadeRoutine = StartCoroutine(CrossFade(currentlyPlaying, emitter));
        }
        else
        {
            // Nothing playing or same emitter not active: fade in this emitter
            StartCoroutine(FadeIn(emitter));
        }

        currentlyPlaying = emitter;

        // Ensure weather/global parameters match this location (if used)
        SetRainLocation(newLocation);
        SetWindLocation(newLocation);

        if (debugLogging) Debug.Log($"AmbianceManager: Changed ambiance to {newLocation}");
    }

    // Fade out currently playing emitter to silence (useful when leaving all zones)
    public void FadeOutCurrent(float duration = -1f)
    {
        if (currentlyPlaying == null) return;
        if (duration <= 0f) duration = crossFadeTime;
        StartCoroutine(FadeOut(currentlyPlaying, duration));
        currentlyPlaying = null;
        if (debugLogging) Debug.Log("AmbianceManager: Fading out current ambiance to silence");
    }

    #endregion

    #region Fade Logic (matches your working, old implementation)

    // Crossfade: fade out old while fading in new (uses fadeTime)
    private IEnumerator CrossFade(StudioEventEmitter oldEmitter, StudioEventEmitter newEmitter)
    {
        crossfadeOldEmitter = oldEmitter;

        // Start the new emitter if not running (old script did this)
        if (!IsEmitterActive(newEmitter))
            newEmitter.Play();

        // Ensure starting volumes
        newEmitter.EventInstance.setVolume(0f);
        oldEmitter.EventInstance.getVolume(out float oldStartVol);
        if (oldStartVol <= 0f) oldStartVol = 1f; // fallback

        float timer = 0f;
        while (timer < crossFadeTime)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / crossFadeTime);

            // Fade out old (oldStartVol -> 0)
            oldEmitter.EventInstance.setVolume(Mathf.Lerp(oldStartVol, 0f, progress));

            // Fade in new (0 -> 1)
            newEmitter.EventInstance.setVolume(progress);

            yield return null;
        }

        // Ensure volumes precise at the end
        oldEmitter.EventInstance.setVolume(0f);
        newEmitter.EventInstance.setVolume(1f);

        // Stop old emitter as in your old script
        oldEmitter.Stop();

        // Clear crossfade state
        crossfadeOldEmitter = null;
        crossfadeRoutine = null;
    }

    // Fade in from silence
    private IEnumerator FadeIn(StudioEventEmitter targetEmitter)
    {
        if (!IsEmitterActive(targetEmitter))
            targetEmitter.Play();

        // start from 0
        targetEmitter.EventInstance.setVolume(0f);

        float timer = 0f;
        while (timer < crossFadeTime)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / crossFadeTime);
            targetEmitter.EventInstance.setVolume(progress);
            yield return null;
        }
        targetEmitter.EventInstance.setVolume(1f);
    }

    // Fade out to silence
    private IEnumerator FadeOut(StudioEventEmitter emitterToFade, float duration)
    {
        if (!IsEmitterActive(emitterToFade)) yield break;

        // If this emitter is already being crossfaded out, don't double-fade
        if (IsCrossfading && emitterToFade == crossfadeOldEmitter)
            yield break;

        // get starting volume if available; fallback to 1f if getVolume not available
        float startVolume = 1f;
        FMOD.RESULT volRes = emitterToFade.EventInstance.getVolume(out float currentVol);
        if (volRes == FMOD.RESULT.OK && currentVol > 0f) startVolume = currentVol;

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

    #region Wind System Integration

    public void SetWindLocation(Location location)
    {
        if (string.IsNullOrEmpty(windLocationParameterName)) return;

        float mapped = MapLocationToWindValue(location);
        RuntimeManager.StudioSystem.setParameterByName(windLocationParameterName, mapped);
        if (debugLogging) Debug.Log($"SetWindLocation: {location} -> {mapped}");
    }

    private float MapLocationToWindValue(Location location)
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