using System.Collections;
using UnityEngine;
using FMODUnity;

public class TimeManager : MonoBehaviour
{
    [Header("Skyboxes")]
    [SerializeField] private Texture2D skyboxNight;
    [SerializeField] private Texture2D skyboxDawn;
    [SerializeField] private Texture2D skyboxDay;
    [SerializeField] private Texture2D skyboxDusk;

    [Header("Light Gradients")]
    [SerializeField] private Gradient gradientNightToDawn;
    [SerializeField] private Gradient gradientDawnToDay;
    [SerializeField] private Gradient gradientDayToDusk;
    [SerializeField] private Gradient gradientDuskToNight;

    [Header("Sun Light")]
    [SerializeField] private Light globalLight;

    [Header("Time Settings")]
    [Tooltip("How many real-time minutes one full 24h in-game day should take.")]
    [SerializeField] private float fullDayLengthMinutes = 4f;

    [Header("Skybox & Light Transition Settings")]
    [SerializeField] private float transitionDuration = 10f;

    [Header("FMOD Settings")]
    [SerializeField] private string timeOfDayParameterName = "TimeOfDay";

    private int minutes;
    private int hours = 5; // start at dawn
    private float tempSecond;

    private enum DayState { Night, Dawn, Day, Dusk }
    private DayState currentState;

    private Coroutine skyboxCoroutine;
    private Coroutine lightCoroutine;

    private void Start()
    {
        // Determine the correct initial state
        currentState = GetCurrentDayState(hours);
        SetState(currentState);

        // Set FMOD and visuals immediately
        SetTimeOfDayParameter(currentState);
        RenderSettings.skybox.SetTexture("_Texture1", GetSkyboxForState(currentState));
        RenderSettings.skybox.SetFloat("_Blend", 0f);
        globalLight.color = GetGradientColorForState(currentState, 1f);
        RenderSettings.fogColor = globalLight.color;

        UpdateSunRotation();
    }

    private void Update()
    {
        tempSecond += Time.deltaTime;
        float secondsPerGameMinute = (fullDayLengthMinutes * 60f) / 1440f;

        if (tempSecond >= secondsPerGameMinute)
        {
            tempSecond = 0f;
            AddMinute();
        }

        UpdateSunRotation();
        UpdateState();
    }

    private void AddMinute()
    {
        minutes++;
        if (minutes >= 60)
        {
            minutes = 0;
            hours = (hours + 1) % 24;
        }
    }

    private DayState GetCurrentDayState(int hour)
    {
        if (hour >= 5 && hour < 7)        return DayState.Dawn;
        else if (hour >= 7 && hour < 18)  return DayState.Day;
        else if (hour >= 18 && hour < 21) return DayState.Dusk;
        else                               return DayState.Night;
    }

    private void UpdateState()
    {
        DayState newState = GetCurrentDayState(hours);

        if (newState != currentState)
        {
            SetState(newState);

            // Start smooth skybox & light transitions
            StartLerpSkybox(GetSkyboxForState(newState), transitionDuration);
            StartLerpLight(GetGradientForState(newState), transitionDuration);

            // Update FMOD parameter
            SetTimeOfDayParameter(newState);
        }
    }

    private void SetState(DayState newState)
    {
        currentState = newState;
        Debug.Log($"☀️ Time of day changed: {currentState} at {hours:00}:{minutes:00}");
    }

    private Texture2D GetSkyboxForState(DayState state)
    {
        return state switch
        {
            DayState.Dawn => skyboxDawn,
            DayState.Day => skyboxDay,
            DayState.Dusk => skyboxDusk,
            DayState.Night => skyboxNight,
            _ => skyboxNight
        };
    }

    private Gradient GetGradientForState(DayState state)
    {
        return state switch
        {
            DayState.Dawn => gradientNightToDawn,
            DayState.Day => gradientDawnToDay,
            DayState.Dusk => gradientDayToDusk,
            DayState.Night => gradientDuskToNight,
            _ => gradientDuskToNight
        };
    }

    private Color GetGradientColorForState(DayState state, float t)
    {
        return GetGradientForState(state).Evaluate(t);
    }

    private void UpdateSunRotation()
    {
        float totalMinutes = hours * 60f + minutes;
        float dayProgress = totalMinutes / 1440f;
        float sunAngle = dayProgress * 360f;

        globalLight.transform.rotation = Quaternion.Euler(30f, sunAngle - 90f, 0f);
    }

    #region Skybox & Light Lerp
    private void StartLerpSkybox(Texture2D target, float duration)
    {
        if (skyboxCoroutine != null) StopCoroutine(skyboxCoroutine);
        skyboxCoroutine = StartCoroutine(LerpSkybox(target, duration));
    }

    private IEnumerator LerpSkybox(Texture2D target, float duration)
    {
        Material skyMat = RenderSettings.skybox;
        Texture2D from = skyMat.GetTexture("_Texture1") as Texture2D;

        skyMat.SetTexture("_Texture1", from);
        skyMat.SetTexture("_Texture2", target);
        skyMat.SetFloat("_Blend", 0f);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            skyMat.SetFloat("_Blend", Mathf.Clamp01(t / duration));
            yield return null;
        }

        // finalize
        skyMat.SetTexture("_Texture1", target);
        skyMat.SetFloat("_Blend", 0f);
        skyboxCoroutine = null;
    }

    private void StartLerpLight(Gradient gradient, float duration)
    {
        if (lightCoroutine != null) StopCoroutine(lightCoroutine);
        lightCoroutine = StartCoroutine(LerpLight(gradient, duration));
    }

    private IEnumerator LerpLight(Gradient gradient, float duration)
    {
        float t = 0f;
        Color startColor = globalLight.color;

        while (t < duration)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / duration);
            globalLight.color = Color.Lerp(startColor, gradient.Evaluate(1f), lerp);
            RenderSettings.fogColor = globalLight.color;
            yield return null;
        }

        globalLight.color = gradient.Evaluate(1f);
        RenderSettings.fogColor = globalLight.color;
        lightCoroutine = null;
    }
    #endregion

    #region FMOD
    private void SetTimeOfDayParameter(DayState state)
    {
        float value = state switch
        {
            DayState.Dawn  => 0f,
            DayState.Day   => 1f,
            DayState.Dusk  => 2f,
            DayState.Night => 3f,
            _ => 0f
        };

        RuntimeManager.StudioSystem.setParameterByName(timeOfDayParameterName, value);
        Debug.Log($"🎧 FMOD parameter '{timeOfDayParameterName}' set to {state} ({value})");
    }
    #endregion
}
