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
    [SerializeField] private float fadeTime = 2f;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;

    private StudioEventEmitter emitter;
    private StudioEventEmitter currentlyPlaying;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }
        DontDestroyOnLoad(this);
    }

    private void Update()
    {
        // Follow the player if there is one
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            transform.position = player.transform.position;
        }
    }

    private void GetLocation(Location location)
    {
        switch (location)
        {
            case Location.Forest:
                emitter = forestAmbianceEmitter;
                break;
            case Location.Factory:
                emitter = factoryAmbianceEmitter;
                break;
        }
    }

    #region Ambiance Control

    public void ChangeAmbiance(Location newLocation)
    {
        GetLocation(newLocation);

        if (currentlyPlaying != null && currentlyPlaying != emitter)
        {
            StartCoroutine(CrossFade(currentlyPlaying, emitter));
        }
        else
        {
            StartCoroutine(FadeIn(emitter));
        }

        currentlyPlaying = emitter;
    }

    public void PlayAudio(Location location)
    {
        GetLocation(location);
        if (!emitter.IsActive)
        {
            emitter.Play();
            currentlyPlaying = emitter;
            if (debugLogging) Debug.Log($"🎵 Started playing {location} ambiance");
        }
    }

    public void StopAudio(Location location)
    {
        GetLocation(location);
        if (emitter.IsActive)
        {
            emitter.Stop();
            if (currentlyPlaying == emitter)
                currentlyPlaying = null;
            if (debugLogging) Debug.Log($"🛑 Stopped {location} ambiance");
        }
    }

    public void SetParameter(Location location, string parameterName, float parameterValue)
    {
        GetLocation(location);
        if (emitter.IsActive)
            emitter.SetParameter(parameterName, parameterValue);
    }

    #endregion

    #region Fade Logic

    private IEnumerator CrossFade(StudioEventEmitter oldEmitter, StudioEventEmitter newEmitter)
    {
        if (!newEmitter.IsActive)
            newEmitter.Play();

        float timer = 0f;
        while (timer < fadeTime)
        {
            timer += Time.deltaTime;
            float progress = timer / fadeTime;

            oldEmitter.EventInstance.setVolume(1f - progress);
            newEmitter.EventInstance.setVolume(progress);

            yield return null;
        }

        oldEmitter.EventInstance.setVolume(0f);
        newEmitter.EventInstance.setVolume(1f);
        oldEmitter.Stop();
    }

    private IEnumerator FadeIn(StudioEventEmitter targetEmitter)
    {
        if (!targetEmitter.IsActive)
            targetEmitter.Play();

        float timer = 0f;
        while (timer < fadeTime)
        {
            timer += Time.deltaTime;
            targetEmitter.EventInstance.setVolume(timer / fadeTime);
            yield return null;
        }

        targetEmitter.EventInstance.setVolume(1f);
    }

    #endregion

    #region Test Methods

    [ContextMenu("Test Forest Ambiance")]
    public void TestForest() => ChangeAmbiance(Location.Forest);

    [ContextMenu("Test Factory Ambiance")]
    public void TestFactory() => ChangeAmbiance(Location.Factory);

    #endregion
}
