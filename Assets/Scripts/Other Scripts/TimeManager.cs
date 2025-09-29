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

    [Header("FMOD Settings")]
    [SerializeField] private string timeOfDayParameterName = "TimeOfDay";

    private int minutes;
    public int Minutes
    {
        get => minutes;
        set { minutes = value; OnMinutesChange(value); }
    }

    private int hours = 5; // start at 05:00 for dawn
    public int Hours
    {
        get => hours;
        set { hours = value; OnHoursChange(value); }
    }

    private int days;
    public int Days
    {
        get => days;
        set => days = value;
    }

    private float tempSecond;

    private enum DayState { Night, Dawn, Day, Dusk }
    private DayState currentState = DayState.Night;

    private void Start()
    {
        // Set initial FMOD parameter based on starting time
        SetTimeOfDayParameter(DayState.Night);
    }

    private void Update()
    {
        tempSecond += Time.deltaTime;

        // How many seconds one in-game minute should take based on total cycle length
        float secondsPerGameMinute = (fullDayLengthMinutes * 60f) / 1440f;

        if (tempSecond >= secondsPerGameMinute)
        {
            Minutes += 1;
            tempSecond = 0;
        }
    }

    private void OnMinutesChange(int value)
    {
        if (value >= 60)
        {
            Hours++;
            minutes = 0;
        }
        if (Hours >= 24)
        {
            Hours = 0;
            Days++;
            Debug.Log($"🌍 New Day: {Days}");
        }

        UpdateSunRotation();
    }

    private void OnHoursChange(int value)
    {
        // Dawn start (05:00)
        if (value == 5)
        {
            SetState(DayState.Dawn);
            StartCoroutine(LerpSkybox(skyboxNight, skyboxDawn, 10f));
            StartCoroutine(LerpLight(gradientNightToDawn, 10f));
            SetTimeOfDayParameter(DayState.Dawn);
        }
        // Day start (06:00)
        else if (value == 6)
        {
            SetState(DayState.Day);
            StartCoroutine(LerpSkybox(skyboxDawn, skyboxDay, 10f));
            StartCoroutine(LerpLight(gradientDawnToDay, 10f));
            SetTimeOfDayParameter(DayState.Day);
        }
        // Dusk start (18:00)
        else if (value == 18)
        {
            SetState(DayState.Dusk);
            StartCoroutine(LerpSkybox(skyboxDay, skyboxDusk, 10f));
            StartCoroutine(LerpLight(gradientDayToDusk, 10f));
            SetTimeOfDayParameter(DayState.Dusk);
        }
        // Night start (19:00)
        else if (value == 19)
        {
            SetState(DayState.Night);
            StartCoroutine(LerpSkybox(skyboxDusk, skyboxNight, 10f));
            StartCoroutine(LerpLight(gradientDuskToNight, 10f));
            SetTimeOfDayParameter(DayState.Night);
        }
    }

    private void SetState(DayState newState)
    {
        if (currentState != newState)
        {
            currentState = newState;
            Debug.Log($"☀️ Time of day changed: {currentState} at {Hours:00}:{Minutes:00}");
        }
    }

    private void UpdateSunRotation()
    {
        float totalMinutes = Hours * 60f + Minutes;
        float dayProgress = totalMinutes / 1440f; // 0–1 through the day

        // 0° = midnight, 180° = noon
        float sunAngle = dayProgress * 360f;

        globalLight.transform.rotation = Quaternion.Euler(30f, sunAngle - 90f, 0f);
    }

    private IEnumerator LerpSkybox(Texture2D a, Texture2D b, float time)
    {
        RenderSettings.skybox.SetTexture("_Texture1", a);
        RenderSettings.skybox.SetTexture("_Texture2", b);
        RenderSettings.skybox.SetFloat("_Blend", 0);
        for (float i = 0; i < time; i += Time.deltaTime)
        {
            RenderSettings.skybox.SetFloat("_Blend", i / time);
            yield return null;
        }
        RenderSettings.skybox.SetTexture("_Texture1", b);
    }

    private IEnumerator LerpLight(Gradient lightGradient, float time)
    {
        for (float i = 0; i < time; i += Time.deltaTime)
        {
            globalLight.color = lightGradient.Evaluate(i / time);
            RenderSettings.fogColor = globalLight.color;
            yield return null;
        }
    }

    // 🎧 FMOD – Set discrete labeled parameter as float
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
}