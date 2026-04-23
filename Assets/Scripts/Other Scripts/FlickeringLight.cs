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

    [Header("Bulb Settings")]
    public Renderer bulbRenderer;
    public float emissionIntensity = 2f;

    [Header("Audio")]
    public FlickeringLightAudio flickeringLightAudio;

    [Header("Time Manager")]
    public TimeManager timeManager;

    private float _timer;
    private float _nextFlickerTime;
    private bool _isNightActive = false;
    private Material _bulbMaterial;
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    void Start()
    {
        streetLight = streetLight != null ? streetLight : GetComponent<Light>();

        if (timeManager == null)
            timeManager = FindObjectOfType<TimeManager>();

        if (bulbRenderer != null)
            _bulbMaterial = bulbRenderer.material;

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

        // Turn bulb emission on/off with the light
        if (_bulbMaterial != null)
        {
            Color emissionColor = _isNightActive ? streetLight.color * emissionIntensity : Color.black;
            _bulbMaterial.SetColor(EmissionColor, emissionColor);
        }
    }

    private void DoFlicker()
    {
        streetLight.intensity = Random.value < offChance
            ? 0f
            : Random.Range(baseIntensity * 0.4f, baseIntensity * 1.1f);

        // Sync bulb emission with light intensity
        if (_bulbMaterial != null)
        {
            Color emissionColor = streetLight.color * (streetLight.intensity > 0 ? emissionIntensity : 0f);
            _bulbMaterial.SetColor(EmissionColor, emissionColor);
        }

        if (flickeringLightAudio != null)
            flickeringLightAudio.LightFlickeringAudio(transform);
    }

    private void ScheduleNextFlicker()
    {
        _timer = 0f;
        _nextFlickerTime = Random.Range(minFlickerSpeed, maxFlickerSpeed);
    }
}