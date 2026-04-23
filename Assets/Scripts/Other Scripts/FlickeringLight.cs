using UnityEngine;

public class FlickeringLight : MonoBehaviour
{
    [Header("Light Settings")]
    public Light streetLight;
    public float baseIntensity = 2.5f;

    [Header("Flicker Settings")]
    public float minFlickerSpeed = 0.02f;
    public float maxFlickerSpeed = 0.15f;
    [Range(0f, 1f)]
    public float offChance = 0.15f;

    [Header("Audio")]
    public FlickeringLightAudio flickeringLightAudio;
   

    [Header("Time Manager")]
    public TimeManager timeManager;

    private float _timer;
    private float _nextFlickerTime;
    private bool _isNightActive = false;

    void Start()
    {
        streetLight = streetLight != null ? streetLight : GetComponent<Light>();

        if (timeManager == null)
            timeManager = FindObjectOfType<TimeManager>();

        streetLight.enabled = false;
        ScheduleNextFlicker();
    }

    void Update()
    {
        UpdateNightState();

        if (!_isNightActive) return;

        _timer += Time.deltaTime;
        if (_timer >= _nextFlickerTime)
        {
            DoFlicker();
            ScheduleNextFlicker();
        }
    }

    private void UpdateNightState()
    {
        if (timeManager == null) return;

        bool shouldBeOn = timeManager.IsNight;
        if (shouldBeOn == _isNightActive) return;

        _isNightActive = shouldBeOn;
        streetLight.enabled = _isNightActive;
    }

    private void DoFlicker()
    {
        streetLight.intensity = Random.value < offChance
            ? 0f
            : Random.Range(baseIntensity * 0.4f, baseIntensity * 1.1f);

        if (flickeringLightAudio != null)
            flickeringLightAudio.LightFlickeringAudio(transform);
    }

    private void ScheduleNextFlicker()
    {
        _timer = 0f;
        _nextFlickerTime = Random.Range(minFlickerSpeed, maxFlickerSpeed);
    }
}