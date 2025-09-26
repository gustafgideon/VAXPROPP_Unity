using System.Collections;
using UnityEngine;

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

        // 🌞 Update sun position
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

    /// <summary>
    /// Updates the sun's rotation based on current time.
    /// Instead of spinning 360°, it does a ~180° arc above the horizon during day
    /// and ~180° below the horizon during night.
    /// </summary>
    private void UpdateSunRotation()
    {
        float totalMinutes = Hours * 60f + Minutes;
        float dayProgress = totalMinutes / 1440f; // 0–1 through the day

        // Sun angle: 0 = midnight below horizon, 0.5 = noon overhead
        float sunAngle = dayProgress * 360f - 90f;

        // Clamp sun to an arc: 180° above horizon, 180° below
        // This makes sunrise and sunset smoother and stops multiple spins
        globalLight.transform.rotation = Quaternion.Euler(new Vector3(sunAngle, 170f, 0f));
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
}