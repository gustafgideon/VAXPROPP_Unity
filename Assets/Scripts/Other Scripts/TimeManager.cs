using System.Collections;
using UnityEngine;
using FMODUnity;

public class TimeManager : MonoBehaviour
{
    [Header("Skyboxes")]
    [SerializeField] private Texture2D skyboxNight;
    [SerializeField] private Texture2D skyboxSunrise;
    [SerializeField] private Texture2D skyboxDay;
    [SerializeField] private Texture2D skyboxSunset;

    [Header("Light Gradients")]
    [SerializeField] private Gradient graddientNightToSunrise;
    [SerializeField] private Gradient graddientSunriseToDay;
    [SerializeField] private Gradient graddientDayToSunset;
    [SerializeField] private Gradient graddientSunsetToNight;

    [Header("Sun Light")]
    [SerializeField] private Light globalLight;

    [Header("Time Settings")]
    [Tooltip("How many real-time minutes one full 24h in-game day should take.")]
    [SerializeField] public float fullDayLengthMinutes = 4f;

    [Header("FMOD Settings")]
    [SerializeField] private string timeOfDayParameterName = "TimeOfDay";
    [SerializeField] private float transitionDurationInSeconds = 5f; // smooth transition time
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private int minutes;
    public int Minutes
    {
        get => minutes;
        set { minutes = value; OnMinutesChange(value); }
    }

    private int hours = 5; // start at 05:00 for sunrise soon
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

    private enum DayState { Night, Sunrise, Day, Sunset }
    private DayState currentState = DayState.Night;

    public enum TimeOfDay { Day, Night }
    private TimeOfDay currentTimeOfDay = TimeOfDay.Night;
    private TimeOfDay targetTimeOfDay = TimeOfDay.Night;

    // FMOD transition values
    private bool isTransitioning = false;
    private float transitionStartTime;
    private float transitionStartValue;
    private float transitionTargetValue;
    private float currentParameterValue = 1f; // start night = 1

    private void Start()
    {
        // Set initial FMOD parameter based on starting time
        SetGlobalParameter(currentParameterValue);
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

        UpdateTransition(); // smoothly update FMOD parameter each frame
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
        // Sunrise start (05:00)
        if (value == 5)
        {
            SetState(DayState.Sunrise);
            StartCoroutine(LerpSkybox(skyboxNight, skyboxSunrise, 10f));
            StartCoroutine(LerpLight(graddientNightToSunrise, 10f));
        }
        // Day start (06:00)
        else if (value == 6)
        {
            SetState(DayState.Day);
            StartCoroutine(LerpSkybox(skyboxSunrise, skyboxDay, 10f));
            StartCoroutine(LerpLight(graddientSunriseToDay, 10f));
            StartTimeOfDayTransition(TimeOfDay.Day); // 🎧 Trigger FMOD transition to day
        }
        // Sunset start (18:00)
        else if (value == 18)
        {
            SetState(DayState.Sunset);
            StartCoroutine(LerpSkybox(skyboxDay, skyboxSunset, 10f));
            StartCoroutine(LerpLight(graddientDayToSunset, 10f));
        }
        // Night start (19:00)
        else if (value == 19)
        {
            SetState(DayState.Night);
            StartCoroutine(LerpSkybox(skyboxSunset, skyboxNight, 10f));
            StartCoroutine(LerpLight(graddientSunsetToNight, 10f));
            StartTimeOfDayTransition(TimeOfDay.Night); // 🎧 Trigger FMOD transition to night
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

        // Rotate around Y for east-west movement (shadows sweep horizontally)
        // Optional: tilt a little on X to simulate Earth's tilt (~20-30°)
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

    // 🎧 FMOD – Trigger transition
    private void StartTimeOfDayTransition(TimeOfDay newTimeOfDay)
    {
        if (isTransitioning || newTimeOfDay == targetTimeOfDay) return;

        targetTimeOfDay = newTimeOfDay;
        isTransitioning = true;
        transitionStartTime = Time.time;
        transitionStartValue = currentParameterValue;
        transitionTargetValue = newTimeOfDay == TimeOfDay.Day ? 0f : 1f;

        Debug.Log($"🎧 Starting TimeOfDay transition: {currentTimeOfDay} → {newTimeOfDay}");
    }

    // 🎧 FMOD – Update transition each frame
    private void UpdateTransition()
    {
        if (!isTransitioning) return;

        float elapsed = Time.time - transitionStartTime;
        float progress = Mathf.Clamp01(elapsed / transitionDurationInSeconds);
        float curveValue = transitionCurve.Evaluate(progress);
        currentParameterValue = Mathf.Lerp(transitionStartValue, transitionTargetValue, curveValue);

        SetGlobalParameter(currentParameterValue);

        if (progress >= 1f)
        {
            isTransitioning = false;
            currentTimeOfDay = targetTimeOfDay;
            Debug.Log($"🎧 TimeOfDay transition complete: {currentTimeOfDay}");
        }
    }

    private void SetGlobalParameter(float value)
    {
        RuntimeManager.StudioSystem.setParameterByName(timeOfDayParameterName, value);
        Debug.Log($"🎧 FMOD parameter '{timeOfDayParameterName}' set to {value:F2}");
    }
}
