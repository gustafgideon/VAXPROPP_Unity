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

    [Header("Overcast Skyboxes")]
    [SerializeField] private Texture2D overcastNight;
    [SerializeField] private Texture2D overcastDawn;
    [SerializeField] private Texture2D overcastDay;
    [SerializeField] private Texture2D overcastDusk;

    [Header("Light Gradients")]
    [SerializeField] private Gradient gradientNightToDawn;
    [SerializeField] private Gradient gradientDawnToDay;
    [SerializeField] private Gradient gradientDayToDusk;
    [SerializeField] private Gradient gradientDuskToNight;

    [Header("Overcast Light Gradients")]
    [SerializeField] private Gradient overcastGradientNight;
    [SerializeField] private Gradient overcastGradientDawn;
    [SerializeField] private Gradient overcastGradientDay;
    [SerializeField] private Gradient overcastGradientDusk;

    [Header("Sun Light")]
    [SerializeField] private Light globalLight;

    [Header("Time Settings")]
    [Tooltip("How many real-time minutes one full 24h in-game day should take.")]
    [SerializeField] private float fullDayLengthMinutes = 4f;

    [Header("Skybox & Light Transition Settings")]
    [SerializeField] private float transitionDuration = 10f;

    [Header("FMOD Settings")]
    [SerializeField] private string timeOfDayParameterName = "TimeOfDay";

    [Header("Weather System")]
    [SerializeField] private WeatherSystemManager weatherSystem;

    private int minutes;
    private int hours = 5; // start at dawn
    private float tempSecond;

    private enum DayState { Night, Dawn, Day, Dusk }
    private DayState currentState;

    private Coroutine lightCoroutine;

    private void Start()
    {
        currentState = GetCurrentDayState(hours);
        SetState(currentState);

        UpdateSkyboxTextures();
        UpdateLightImmediate();
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
        UpdateSkyboxBlend();
        UpdateLightBlend();
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
        if (hour >= 5 && hour < 7) return DayState.Dawn;
        else if (hour >= 7 && hour < 18) return DayState.Day;
        else if (hour >= 18 && hour < 21) return DayState.Dusk;
        else return DayState.Night;
    }

    private void UpdateState()
    {
        DayState newState = GetCurrentDayState(hours);
        if (newState != currentState)
        {
            SetState(newState);

            StartLerpLight(GetNormalGradientForState(newState), transitionDuration);
            UpdateSkyboxTextures(); // <- important for shader
            SetTimeOfDayParameter(newState);
        }
    }

    private void SetState(DayState newState)
    {
        currentState = newState;
        Debug.Log($"☀️ Time of day changed: {currentState} at {hours:00}:{minutes:00}");
    }

    #region Skybox Handling

    private void UpdateSkyboxTextures()
    {
        if (RenderSettings.skybox == null) return;

        Material skyMat = RenderSettings.skybox;
        skyMat.SetTexture("_Texture1", GetBaseSkyForState(currentState));
        skyMat.SetTexture("_Texture2", GetOvercastSkyForState(currentState));
    }

    private void UpdateSkyboxBlend()
    {
        if (RenderSettings.skybox == null) return;

        float blend = weatherSystem != null ? weatherSystem.rainIntensity : 0f;
        RenderSettings.skybox.SetFloat("_Blend", blend);
    }

    private Texture2D GetBaseSkyForState(DayState state)
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

    private Texture2D GetOvercastSkyForState(DayState state)
    {
        return state switch
        {
            DayState.Dawn => overcastDawn,
            DayState.Day => overcastDay,
            DayState.Dusk => overcastDusk,
            DayState.Night => overcastNight,
            _ => overcastNight
        };
    }

    #endregion

    #region Light Handling

    private void UpdateLightImmediate()
    {
        UpdateLightBlend();
    }

    private void UpdateLightBlend()
    {
        if (globalLight == null) return;

        float rain = weatherSystem != null ? weatherSystem.rainIntensity : 0f;
        Color normalColor = GetNormalGradientForState(currentState).Evaluate(1f);
        Color overcastColor = GetOvercastGradientForState(currentState).Evaluate(1f);

        globalLight.color = Color.Lerp(normalColor, overcastColor, rain);
        RenderSettings.fogColor = globalLight.color;
    }

    private Gradient GetNormalGradientForState(DayState state)
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

    private Gradient GetOvercastGradientForState(DayState state)
    {
        return state switch
        {
            DayState.Dawn => overcastGradientDawn,
            DayState.Day => overcastGradientDay,
            DayState.Dusk => overcastGradientDusk,
            DayState.Night => overcastGradientNight,
            _ => overcastGradientNight
        };
    }

    private void StartLerpLight(Gradient targetGradient, float duration)
    {
        if (lightCoroutine != null) StopCoroutine(lightCoroutine);
        lightCoroutine = StartCoroutine(LerpLight(targetGradient, duration));
    }

    private IEnumerator LerpLight(Gradient targetGradient, float duration)
    {
        float t = 0f;
        Color startColor = globalLight.color;

        while (t < duration)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / duration);

            Color normal = targetGradient.Evaluate(1f);
            Color overcast = GetOvercastGradientForState(currentState).Evaluate(1f);
            float rain = weatherSystem != null ? weatherSystem.rainIntensity : 0f;

            globalLight.color = Color.Lerp(normal, overcast, rain);
            RenderSettings.fogColor = globalLight.color;

            yield return null;
        }

        Color finalNormal = targetGradient.Evaluate(1f);
        Color finalOvercast = GetOvercastGradientForState(currentState).Evaluate(1f);
        float finalRain = weatherSystem != null ? weatherSystem.rainIntensity : 0f;

        globalLight.color = Color.Lerp(finalNormal, finalOvercast, finalRain);
        RenderSettings.fogColor = globalLight.color;

        lightCoroutine = null;
    }

    #endregion

    private void UpdateSunRotation()
    {
        float totalMinutes = hours * 60f + minutes;
        float dayProgress = totalMinutes / 1440f;
        float sunAngle = dayProgress * 360f;
        globalLight.transform.rotation = Quaternion.Euler(30f, sunAngle - 90f, 0f);
    }

    #region FMOD

    private void SetTimeOfDayParameter(DayState state)
    {
        float value = state switch
        {
            DayState.Dawn => 0f,
            DayState.Day => 1f,
            DayState.Dusk => 2f,
            DayState.Night => 3f,
            _ => 0f
        };

        RuntimeManager.StudioSystem.setParameterByName(timeOfDayParameterName, value);
    }

    #endregion
}
