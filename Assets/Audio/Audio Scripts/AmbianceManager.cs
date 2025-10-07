using System.Collections;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public enum Location
{
    Forest,
    Factory
}

public class AmbianceManager : MonoBehaviour
{
    public static AmbianceManager Instance { get; private set; }

    [Header("Ambiance Emitters")]
    [SerializeField] private StudioEventEmitter forestAmbianceEmitter;
    [SerializeField] private StudioEventEmitter factoryAmbianceEmitter;

    [Header("Fade Settings")]
    [SerializeField] private float defaultFadeTime = 2f;

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
            case Location.Factory: emitter = factoryAmbianceEmitter; break;
        }
    }

    #region Ambiance Control

    public void ChangeAmbiance(Location newLocation)
    {
        GetLocation(newLocation);
        if (emitter == null)
        {
            if (debugLogging) Debug.LogWarning($"AmbianceManager: No emitter for {newLocation}");
            return;
        }

        if (!emitter.IsActive)
        {
            emitter.Play();
            emitter.EventInstance.setVolume(0f);
        }

        if (currentlyPlaying != null && currentlyPlaying != emitter)
            StartCoroutine(CrossFade(currentlyPlaying, emitter, defaultFadeTime));
        else if (currentlyPlaying != emitter)
            StartCoroutine(FadeIn(emitter, defaultFadeTime));

        currentlyPlaying = emitter;

        if (debugLogging) Debug.Log($"AmbianceManager: Changed ambiance to {newLocation}");
    }

    public void PlayAudio(Location location)
    {
        GetLocation(location);
        if (emitter != null && !emitter.IsActive)
        {
            emitter.Play();
            currentlyPlaying = emitter;
            if (debugLogging) Debug.Log($"PlayAudio: {location}");
        }
    }

    public void StopAudio(Location location)
    {
        GetLocation(location);
        if (emitter != null && emitter.IsActive)
        {
            emitter.Stop();
            if (currentlyPlaying == emitter) currentlyPlaying = null;
            if (debugLogging) Debug.Log($"StopAudio: {location}");
        }
    }

    // Event-local parameter
    public void SetParameter(Location location, string parameterName, float parameterValue)
    {
        if (string.IsNullOrEmpty(parameterName)) return;
        GetLocation(location);
        if (emitter == null) return;

        if (!emitter.IsActive)
            emitter.Play();

        emitter.SetParameter(parameterName, parameterValue);

        if (debugLogging) Debug.Log($"Event Parameter {parameterName}->{parameterValue} on {location}");
    }

    // GLOBAL parameter
    public void SetGlobalParameter(string parameterName, float value, bool log = true)
    {
        if (string.IsNullOrEmpty(parameterName)) return;
        RuntimeManager.StudioSystem.setParameterByName(parameterName, value);
        if (debugLogging && log) Debug.Log($"Global Parameter {parameterName} -> {value}");
    }

    public void SetGlobalParameterFade(string parameterName, float from, float to, float time)
    {
        StartCoroutine(FadeGlobalParameter(parameterName, from, to, time));
    }

    private IEnumerator FadeGlobalParameter(string parameterName, float from, float to, float time)
    {
        if (time <= 0f)
        {
            SetGlobalParameter(parameterName, to);
            yield break;
        }

        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            float v = Mathf.Lerp(from, to, t / time);
            RuntimeManager.StudioSystem.setParameterByName(parameterName, v);
            yield return null;
        }
        RuntimeManager.StudioSystem.setParameterByName(parameterName, to);
        if (debugLogging) Debug.Log($"Global Parameter (faded) {parameterName} -> {to}");
    }

    public void DebugReadGlobalParameter(string parameterName)
    {
        if (!debugLogging) return;
        if (RuntimeManager.StudioSystem.getParameterDescriptionByName(parameterName, out var desc) == FMOD.RESULT.OK)
        {
            RuntimeManager.StudioSystem.getParameterByID(desc.id, out float value, out float finalValue);
            Debug.Log($"[ParamDebug] {parameterName} Raw:{value} Final:{finalValue}");
        }
    }

    #endregion

    #region Fade Logic

    private IEnumerator CrossFade(StudioEventEmitter oldEmitter, StudioEventEmitter newEmitter, float duration)
    {
        if (!newEmitter.IsActive) newEmitter.Play();
        newEmitter.EventInstance.setVolume(0f);
        oldEmitter.EventInstance.setVolume(1f);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = t / duration;
            oldEmitter.EventInstance.setVolume(1f - p);
            newEmitter.EventInstance.setVolume(p);
            yield return null;
        }

        oldEmitter.EventInstance.setVolume(0f);
        newEmitter.EventInstance.setVolume(1f);
        oldEmitter.Stop();
    }

    private IEnumerator FadeIn(StudioEventEmitter targetEmitter, float duration)
    {
        if (!targetEmitter.IsActive)
        {
            targetEmitter.Play();
            targetEmitter.EventInstance.setVolume(0f);
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            targetEmitter.EventInstance.setVolume(t / duration);
            yield return null;
        }
        targetEmitter.EventInstance.setVolume(1f);
    }

    #endregion
}