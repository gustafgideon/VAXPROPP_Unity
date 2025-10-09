using UnityEngine;
using UnityEngine.Rendering; // For RenderSettings.ambientMode
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

    [Header("Sun / Main Directional Light")]
    [SerializeField] private Light globalLight;

    [Header("Sun Path")]
    [Tooltip("Maximum altitude of the sun above horizon (degrees). Night drives it below 0.")]
    [Range(0f, 90f)]
    [SerializeField] private float maxSunAltitude = 55f;

    [Header("Time Settings")]
    [Tooltip("Real-time minutes for a full 24h in-game day.")]
    [SerializeField] private float fullDayLengthMinutes = 4f;

    [Header("Light Color Gradients (Per Segment)")]
    [SerializeField] private Gradient gradientNightToDawn;
    [SerializeField] private Gradient gradientDawnToDay;
    [SerializeField] private Gradient gradientDayToDusk;
    [SerializeField] private Gradient gradientDuskToNight;

    [Header("Overcast Color Gradients (Per Segment)")]
    [SerializeField] private Gradient overcastGradientNight;
    [SerializeField] private Gradient overcastGradientDawn;
    [SerializeField] private Gradient overcastGradientDay;
    [SerializeField] private Gradient overcastGradientDusk;

    [Header("Phase Transition Blend (In-Game Minutes)")]
    [Tooltip("Blend duration for light COLOR after any phase change.")]
    [SerializeField] private float stateColorBlendDurationMinutes = 30f;
    [Tooltip("Blend duration for light INTENSITY after any phase change.")]
    [SerializeField] private float stateIntensityBlendDurationMinutes = 20f;
    [Tooltip("Optional faster blend duration specifically when entering Night (color).")]
    [SerializeField] private float nightColorBlendDurationMinutes = 8f;
    [Tooltip("Optional faster blend duration specifically when entering Night (intensity).")]
    [SerializeField] private float nightIntensityBlendDurationMinutes = 10f;

    [Header("Rain / Overcast Blend Settings")]
    [Tooltip("Front-loaded curve so overcast appears earlier with light rain.")]
    [SerializeField] private AnimationCurve overcastBlendCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.1f, 0.55f),
        new Keyframe(0.3f, 0.85f),
        new Keyframe(1f, 1f)
    );

    [Tooltip("Multiplies base light intensity by this curve vs rainIntensity.")]
    [SerializeField] private AnimationCurve rainLightDarkeningCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(0.15f, 0.8f),
        new Keyframe(0.5f, 0.55f),
        new Keyframe(1f, 0.4f)
    );

    [Header("Rain Cold Tint")]
    [SerializeField] private bool forceColdTintInRain = true;
    [SerializeField] private Color rainColdTint = new Color(0.65f, 0.72f, 0.85f, 1f);
    [SerializeField] private AnimationCurve rainColdTintStrengthByRain = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.2f, 0.5f),
        new Keyframe(1f, 1f)
    );
    [SerializeField] private AnimationCurve rainDesaturationByRain = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(1f, 0.6f)
    );

    [Header("Rain Envelope (Global Darkening)")]
    [SerializeField] private AnimationCurve rainGlobalDimCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(0.15f, 0.92f),
        new Keyframe(0.5f, 0.75f),
        new Keyframe(1f, 0.6f)
    );
    [SerializeField] private AnimationCurve ambientDimByRain = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(1f, 0.55f)
    );
    [SerializeField] private AnimationCurve skyboxExposureByRain = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(1f, 0.7f)
    );
    [SerializeField] private AnimationCurve fogDensityByRain = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(1f, 1.6f)
    );

    [Header("Night Envelope (Global Darkening and Cooling)")]
    [Tooltip("Cold tint target color at night.")]
    [SerializeField] private Color nightColdTint = new Color(0.55f, 0.62f, 0.78f, 1f);
    [Tooltip("How strongly to bias toward the night tint as night deepens (x=nightFactor, y=strength).")]
    [SerializeField] private AnimationCurve nightColdTintStrengthByNight = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.5f, 0.5f),
        new Keyframe(1f, 1f)
    );
    [Tooltip("Desaturation by night depth (x=nightFactor, y=desat).")]
    [SerializeField] private AnimationCurve nightDesaturationByNight = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(1f, 0.4f)
    );
    [Tooltip("Direct sun dimming by night depth (multiplier).")]
    [SerializeField] private AnimationCurve nightGlobalDimByNight = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(1f, 0.35f)
    );
    [Tooltip("Ambient dimming by night depth.")]
    [SerializeField] private AnimationCurve nightAmbientDimByNight = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(1f, 0.45f)
    );
    [Tooltip("Skybox exposure reduction by night depth.")]
    [SerializeField] private AnimationCurve nightSkyboxExposureByNight = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(1f, 0.55f)
    );
    [Tooltip("Fog density increase by night depth.")]
    [SerializeField] private AnimationCurve nightFogDensityByNight = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(1f, 1.3f)
    );

    [Header("Shadowless Control (No Shadows in Rain/Night)")]
    [SerializeField] private bool disableShadowsAtNight = true;
    [Tooltip("Rain intensity at or above which we remove shadows completely.")]
    [SerializeField] private float rainNoShadowThreshold = 0.01f;
    [Tooltip("Sun-height threshold for shadow removal by night depth (0..1 of day). Higher removes earlier in dusk.")]
    [Range(0f, 1f)]
    [SerializeField] private float nightShadowlessSunHeightThreshold = 0.12f;
    [Tooltip("Speed (units per second) to fade to/from shadowless mode.")]
    [SerializeField] private float shadowToggleLerpSpeed = 2.5f;
    [Tooltip("Extra dim when shadows are fully off, to avoid the scene feeling brighter.")]
    [Range(0.5f, 1f)]
    [SerializeField] private float noShadowDimWhenOff = 0.85f;

    [Header("Base Light Intensity Over 24h")]
    [Tooltip("Base intensity over 24h BEFORE rain/night envelopes. Will be auto-multiplied by sun height so night goes near zero.")]
    [SerializeField] private AnimationCurve lightIntensityOverDay = new AnimationCurve(
        new Keyframe(0f,    0.02f),  // Midnight
        new Keyframe(0.21f, 0.4f),   // ~05:00
        new Keyframe(0.29f, 0.85f),  // ~07:00
        new Keyframe(0.5f,  1.0f),   // Midday
        new Keyframe(0.71f, 0.85f),  // ~17:00
        new Keyframe(0.79f, 0.4f),   // ~19:00
        new Keyframe(1f,    0.02f)   // Midnight
    );

    [Header("FMOD Settings")]
    [SerializeField] private string timeOfDayParameterName = "TimeOfDay";

    [Header("Weather System (Optional)")]
    [SerializeField] private WeatherSystemManager weatherSystem;

    [Header("Debug Options")]
    [SerializeField] private bool debugDayStateChanges = true;
    [SerializeField] private bool debugShadowlessTransitions = false;
    [SerializeField] private bool debugPhaseBlend = false;
    [SerializeField] private bool debugEnvelope = false;

    private int minutes;
    private int hours = 5; // start at dawn
    private float tempSecond;

    private enum DayState { Night, Dawn, Day, Dusk }
    private DayState currentState;

    // Phase blending
    private DayState previousState;
    private float stateStartTotalMinutes;
    private Color previousPhaseColor;
    private float previousPhaseIntensity;

    // Shadowless (full removal) controller
    private LightShadows originalShadowType;
    private float originalShadowStrength = 1f;
    // 0 = normal shadows, 1 = shadowless (Light.shadows=None)
    private float currentShadowless = 0f;
    private bool physicallyShadowless = false; // true when Light.shadows=None

    // Skybox/ambient/fog baselines
    private float originalAmbientIntensity = 1f;
    private Color originalAmbientLight;
    private bool hasSkyboxExposureProps = false;
    private float baseFogDensity = 0.01f;

    // Cached sun height (0..1, 0 night, 1 high noon)
    private float sunHeight01 = 0f;

    private void Start()
    {
        if (globalLight == null)
        {
            Debug.LogWarning("TimeManager: No globalLight assigned.");
            enabled = false; return;
        }

        originalShadowType = globalLight.shadows;
        originalShadowStrength = globalLight.shadowStrength;

        // Cache ambient baselines
        originalAmbientIntensity = RenderSettings.ambientIntensity;
        originalAmbientLight = RenderSettings.ambientLight;

        if (RenderSettings.fog) baseFogDensity = RenderSettings.fogDensity;

        currentState = GetCurrentDayState(hours);
        previousState = currentState;
        stateStartTotalMinutes = GetTotalMinutes();

        UpdateSkyboxTextures();
        DetectSkyboxExposureProps();
        UpdateTimeOfDayLerp();
        UpdateSunRotation();          // sets sunHeight01
        UpdateLightingContinuous();   // initializes color/intensity

        // Capture initial phase snapshots
        previousPhaseColor = globalLight.color;
        previousPhaseIntensity = globalLight.intensity;

        if (debugDayStateChanges)
            Debug.Log($"[TimeManager] Initial state: {currentState} at {hours:00}:{minutes:00}");
    }

    private void Update()
    {
        AdvanceTime();
        UpdateSunRotation();      // updates sunHeight01
        UpdateState();

        UpdateTimeOfDayLerp();
        UpdateSkyboxBlend();
        UpdateLightingContinuous();
        UpdateSkyboxExposure();
        UpdateAmbientAndFogEnvelope();
    }

    private void AdvanceTime()
    {
        tempSecond += Time.deltaTime;
        float secondsPerGameMinute = (fullDayLengthMinutes * 60f) / 1440f;
        if (tempSecond >= secondsPerGameMinute)
        {
            tempSecond = 0f;
            minutes++;
            if (minutes >= 60)
            {
                minutes = 0;
                hours = (hours + 1) % 24;
            }
        }
    }

    private float GetTotalMinutes() => hours * 60f + minutes;

    private DayState GetCurrentDayState(int hour)
    {
        if (hour >= 5 && hour < 7) return DayState.Dawn;
        if (hour >= 7 && hour < 18) return DayState.Day;
        if (hour >= 18 && hour < 21) return DayState.Dusk;
        return DayState.Night;
    }

    private void UpdateState()
    {
        DayState newState = GetCurrentDayState(hours);
        if (newState != currentState)
        {
            previousState = currentState;
            currentState = newState;

            // Snapshot old phase values before recalculating
            previousPhaseColor = globalLight.color;
            previousPhaseIntensity = globalLight.intensity;
            stateStartTotalMinutes = GetTotalMinutes();

            if (debugDayStateChanges)
            {
                float rain = weatherSystem ? weatherSystem.rainIntensity : 0f;
                Debug.Log($"[TimeManager] Day state {previousState} -> {currentState} at {hours:00}:{minutes:00} (rain={rain:0.00})");
            }

            UpdateSkyboxTextures();
            SetTimeOfDayParameter(newState);
        }
    }

    #region Skybox

    private void UpdateSkyboxTextures()
    {
        if (RenderSettings.skybox == null) return;
        var mat = RenderSettings.skybox;
        mat.SetTexture("_Texture1", GetBaseSkyForState(currentState));
        mat.SetTexture("_Texture2", GetNextBaseSkyForLerp(currentState));
        mat.SetTexture("_OvercastTexture", GetOvercastSkyForState(currentState));
    }

    private void DetectSkyboxExposureProps()
    {
        if (RenderSettings.skybox == null) return;
        var mat = RenderSettings.skybox;
        hasSkyboxExposureProps = mat.HasProperty("_Exposure1") && mat.HasProperty("_Exposure2");
    }

    private void UpdateSkyboxExposure()
    {
        if (RenderSettings.skybox == null || !hasSkyboxExposureProps) return;
        float rain = weatherSystem ? weatherSystem.rainIntensity : 0f;

        // Combine rain and night exposure multipliers
        float expoRain = Mathf.Clamp(skyboxExposureByRain.Evaluate(rain), 0.05f, 2f);
        float nightFactor = 1f - sunHeight01; // 0 day, 1 deep night
        float expoNight = Mathf.Clamp(nightSkyboxExposureByNight.Evaluate(nightFactor), 0.05f, 2f);
        float expo = Mathf.Clamp(expoRain * expoNight, 0.01f, 2f);

        RenderSettings.skybox.SetFloat("_Exposure1", expo);
        RenderSettings.skybox.SetFloat("_Exposure2", expo);
    }

    private void UpdateTimeOfDayLerp()
    {
        if (RenderSettings.skybox == null) return;
        float hourFraction = hours + minutes / 60f;
        float lerp = 0f;

        switch (currentState)
        {
            case DayState.Dawn: lerp = Mathf.InverseLerp(5f, 7f, hourFraction); break;
            case DayState.Day:  lerp = Mathf.InverseLerp(7f, 18f, hourFraction); break;
            case DayState.Dusk: lerp = Mathf.InverseLerp(18f, 21f, hourFraction); break;
            case DayState.Night:
                float wrapped = hours >= 21 ? hourFraction : hourFraction + 24f;
                lerp = Mathf.InverseLerp(21f, 29f, wrapped);
                break;
        }

        RenderSettings.skybox.SetFloat("_TimeOfDayLerp", lerp);
    }

    private void UpdateSkyboxBlend()
    {
        if (RenderSettings.skybox == null) return;
        float rain = weatherSystem ? weatherSystem.rainIntensity : 0f;
        RenderSettings.skybox.SetFloat("_Blend", overcastBlendCurve.Evaluate(rain));
    }

    private Texture2D GetBaseSkyForState(DayState s) => s switch
    {
        DayState.Dawn => skyboxDawn,
        DayState.Day  => skyboxDay,
        DayState.Dusk => skyboxDusk,
        DayState.Night => skyboxNight,
        _ => skyboxNight
    };

    private Texture2D GetNextBaseSkyForLerp(DayState s) => s switch
    {
        DayState.Night => skyboxDawn,
        DayState.Dawn  => skyboxDay,
        DayState.Day   => skyboxDusk,
        DayState.Dusk  => skyboxNight,
        _ => skyboxNight
    };

    private Texture2D GetOvercastSkyForState(DayState s) => s switch
    {
        DayState.Dawn => overcastDawn,
        DayState.Day  => overcastDay,
        DayState.Dusk => overcastDusk,
        DayState.Night => overcastNight,
        _ => overcastNight
    };

    #endregion

    #region Lighting, Envelope, Shadowless

    private void UpdateLightingContinuous()
    {
        if (!globalLight) return;

        float rain = weatherSystem ? weatherSystem.rainIntensity : 0f;

        // Raw target color from gradients (pre-phase-blend)
        float segmentProgress = GetSegmentProgress();
        Color normalColor = GetNormalGradientForState(currentState).Evaluate(segmentProgress);
        Color overcastColor = GetOvercastGradientForState(currentState).Evaluate(segmentProgress);
        float overcastBlend = overcastBlendCurve.Evaluate(rain);
        Color targetPhaseColor = Color.Lerp(normalColor, overcastColor, overcastBlend);

        // Force cold, desaturated tint in rain
        if (forceColdTintInRain && rain > 0f)
        {
            float coldStrength = Mathf.Clamp01(rainColdTintStrengthByRain.Evaluate(rain));
            targetPhaseColor = Color.Lerp(targetPhaseColor, rainColdTint, coldStrength);

            float desat = Mathf.Clamp01(rainDesaturationByRain.Evaluate(rain));
            targetPhaseColor = Desaturate(targetPhaseColor, desat);
        }

        // Night cold tint + desat by night depth (even if not raining)
        float nightFactor = 1f - sunHeight01; // 0 day .. 1 deep night
        if (nightFactor > 0f)
        {
            float nightTint = Mathf.Clamp01(nightColdTintStrengthByNight.Evaluate(nightFactor));
            targetPhaseColor = Color.Lerp(targetPhaseColor, nightColdTint, nightTint);

            float nightDesat = Mathf.Clamp01(nightDesaturationByNight.Evaluate(nightFactor));
            targetPhaseColor = Desaturate(targetPhaseColor, nightDesat);
        }

        // Raw target intensity (pre-phase-blend)
        float dayProgress = GetDayProgress();
        float baseIntensity = lightIntensityOverDay.Evaluate(dayProgress);

        // Multiply by sun height so night intensity collapses naturally
        float targetFromSun = baseIntensity * sunHeight01;

        // Apply rain darkening envelope on direct light
        float rainMultiplier = rainLightDarkeningCurve.Evaluate(rain);
        float targetPhaseIntensity = targetFromSun * rainMultiplier;

        // Phase blending (in-game minutes), with faster Night option
        float elapsedInState = GetTotalMinutes() - stateStartTotalMinutes;
        float colorDuration = (currentState == DayState.Night && nightColorBlendDurationMinutes > 0f)
            ? nightColorBlendDurationMinutes
            : stateColorBlendDurationMinutes;
        float intensityDuration = (currentState == DayState.Night && nightIntensityBlendDurationMinutes > 0f)
            ? nightIntensityBlendDurationMinutes
            : stateIntensityBlendDurationMinutes;

        float colorBlendAlpha = colorDuration <= 0f ? 1f : Mathf.Clamp01(elapsedInState / colorDuration);
        float intensityBlendAlpha = intensityDuration <= 0f ? 1f : Mathf.Clamp01(elapsedInState / intensityDuration);

        Color blendedColor = Color.Lerp(previousPhaseColor, targetPhaseColor, colorBlendAlpha);
        float blendedIntensity = Mathf.Lerp(previousPhaseIntensity, targetPhaseIntensity, intensityBlendAlpha);

        // Shadowless control (no shadows in heavy rain or deep night)
        UpdateShadowless(rain, nightFactor);

        // Apply global envelopes: rain and night
        float dimRain = Mathf.Clamp01(rainGlobalDimCurve.Evaluate(rain));
        float dimNight = Mathf.Clamp01(nightGlobalDimByNight.Evaluate(nightFactor));
        float shadowlessDim = Mathf.Lerp(1f, noShadowDimWhenOff, currentShadowless);

        float finalSunIntensity = blendedIntensity * dimRain * dimNight * shadowlessDim;

        // Apply to light
        globalLight.intensity = finalSunIntensity;
        globalLight.color = blendedColor;

        // Fog color follows light color (density handled in envelope)
        RenderSettings.fogColor = blendedColor;

        if (debugPhaseBlend)
        {
            if (elapsedInState < Mathf.Max(colorDuration, intensityDuration))
            {
                Debug.Log($"[TimeManager] PhaseBlend state={currentState} elapsed={elapsedInState:0.0}m colorA={colorBlendAlpha:0.00} intensA={intensityBlendAlpha:0.00} rain={rain:0.00} nightFactor={nightFactor:0.00}");
            }
        }
    }

    private void UpdateShadowless(float rain, float nightFactor)
    {
        // Sun height threshold for night-based shadowless
        bool nightShadowless = disableShadowsAtNight && (sunHeight01 <= (1f - Mathf.Clamp01(1f - nightShadowlessSunHeightThreshold)));
        // Or simply: remove shadows when the sun is low enough (dusk/night)
        nightShadowless = disableShadowsAtNight && (sunHeight01 <= (1f - nightShadowlessSunHeightThreshold));

        bool wantShadowlessFromRain = rain >= rainNoShadowThreshold;
        bool targetShadowless = nightShadowless || wantShadowlessFromRain;

        float target = targetShadowless ? 1f : 0f;
        float prev = currentShadowless;

        currentShadowless = Mathf.MoveTowards(currentShadowless, target, shadowToggleLerpSpeed * Time.deltaTime);

        // Fade shadowStrength while not physically shadowless
        if (!physicallyShadowless)
        {
            if (globalLight.shadows == LightShadows.None && currentShadowless < 0.999f)
                globalLight.shadows = originalShadowType;

            globalLight.shadowStrength = originalShadowStrength * (1f - currentShadowless);

            if (currentShadowless >= 0.999f)
            {
                globalLight.shadows = LightShadows.None;
                physicallyShadowless = true;
                if (debugShadowlessTransitions)
                    Debug.Log($"[TimeManager] Shadows DISABLED (rain={rain:0.00}, nightFactor={nightFactor:0.00})");
            }
        }
        else
        {
            if (currentShadowless <= 0.001f)
            {
                globalLight.shadows = originalShadowType;
                physicallyShadowless = false;
                globalLight.shadowStrength = originalShadowStrength;
                if (debugShadowlessTransitions)
                    Debug.Log($"[TimeManager] Shadows ENABLED (rain={rain:0.00}, nightFactor={nightFactor:0.00})");
            }
            else
            {
                globalLight.shadowStrength = 0f;
            }
        }

        if (debugShadowlessTransitions && Mathf.Abs(prev - currentShadowless) > 0.001f)
        {
            Debug.Log($"[TimeManager] Shadowless fade: target={target:0.00} current={currentShadowless:0.00} rain={rain:0.00} nightFactor={nightFactor:0.00} sunHeight01={sunHeight01:0.00}");
        }
    }

    private void UpdateAmbientAndFogEnvelope()
    {
        float rain = weatherSystem ? weatherSystem.rainIntensity : 0f;
        float nightFactor = 1f - sunHeight01;

        // Ambient dim combines rain and night
        float ambientDim = Mathf.Clamp01(ambientDimByRain.Evaluate(rain)) *
                           Mathf.Clamp01(nightAmbientDimByNight.Evaluate(nightFactor));

        if (RenderSettings.ambientMode == AmbientMode.Skybox)
        {
            RenderSettings.ambientIntensity = originalAmbientIntensity * ambientDim;
        }
        else
        {
            // For Flat/Trilight, cool and dim ambientLight
            Color targetAmbient = originalAmbientLight;
            // Cool toward night tint by night depth; also slight cool in rain
            targetAmbient = Color.Lerp(targetAmbient, nightColdTint, Mathf.Clamp01(nightFactor));
            targetAmbient = Color.Lerp(targetAmbient, rainColdTint, Mathf.Clamp01(rain * 0.4f));
            targetAmbient *= ambientDim;
            RenderSettings.ambientLight = targetAmbient;
        }

        // Fog density combines rain and night
        if (RenderSettings.fog)
        {
            float fogMul = Mathf.Max(0f, fogDensityByRain.Evaluate(rain)) *
                           Mathf.Max(0f, nightFogDensityByNight.Evaluate(nightFactor));
            RenderSettings.fogDensity = baseFogDensity * fogMul;
        }

        if (debugEnvelope)
        {
            Debug.Log($"[TimeManager] Envelope rain={rain:0.00} nightFactor={nightFactor:0.00} ambDim={ambientDim:0.00} fogDens={RenderSettings.fogDensity:0.0000}");
        }
    }

    private float GetSegmentProgress()
    {
        float hf = hours + minutes / 60f;
        return currentState switch
        {
            DayState.Dawn => Mathf.InverseLerp(5f, 7f, hf),
            DayState.Day  => Mathf.InverseLerp(7f, 18f, hf),
            DayState.Dusk => Mathf.InverseLerp(18f, 21f, hf),
            DayState.Night => Mathf.InverseLerp(21f, 29f, (hours >= 21 ? hf : hf + 24f)),
            _ => 0f
        };
    }

    private float GetDayProgress()
    {
        float totalMinutes = GetTotalMinutes();
        return totalMinutes / 1440f;
    }

    private void UpdateSunRotation()
    {
        // dayProgress 0..1
        float dayProgress = GetDayProgress();

        // Sun height: 0 at night, 1 at high noon
        // sin(2πt) produces -1..1 over the day; remap to 0..1
        float s = Mathf.Sin(dayProgress * Mathf.PI * 2f);
        sunHeight01 = Mathf.Clamp01(s * 0.5f + 0.5f);

        // Altitude angle goes from -max -> +max; negative at night
        float altitudeAngle = (s) * maxSunAltitude;

        // Azimuth around the horizon (same yaw as before)
        float sunAngle = dayProgress * 360f;
        globalLight.transform.rotation = Quaternion.Euler(altitudeAngle, sunAngle - 90f, 0f);
    }

    private Gradient GetNormalGradientForState(DayState s) => s switch
    {
        DayState.Dawn => gradientNightToDawn,
        DayState.Day  => gradientDawnToDay,
        DayState.Dusk => gradientDayToDusk,
        DayState.Night => gradientDuskToNight,
        _ => gradientDuskToNight
    };

    private Gradient GetOvercastGradientForState(DayState s) => s switch
    {
        DayState.Dawn => overcastGradientDawn,
        DayState.Day  => overcastGradientDay,
        DayState.Dusk => overcastGradientDusk,
        DayState.Night => overcastGradientNight,
        _ => overcastGradientNight
    };

    private static Color Desaturate(Color c, float amount)
    {
        amount = Mathf.Clamp01(amount);
        Color.RGBToHSV(c, out float h, out float s, out float v);
        s = Mathf.Lerp(s, 0f, amount);
        return Color.HSVToRGB(h, s, v);
    }

    #endregion

    #region FMOD
    private void SetTimeOfDayParameter(DayState state)
    {
        float value = state switch
        {
            DayState.Dawn => 0f,
            DayState.Day  => 1f,
            DayState.Dusk => 2f,
            DayState.Night => 3f,
            _ => 0f
        };
        RuntimeManager.StudioSystem.setParameterByName(timeOfDayParameterName, value);
    }
    #endregion

#if UNITY_EDITOR
    private void OnValidate()
    {
        stateColorBlendDurationMinutes = Mathf.Max(0f, stateColorBlendDurationMinutes);
        stateIntensityBlendDurationMinutes = Mathf.Max(0f, stateIntensityBlendDurationMinutes);
        nightColorBlendDurationMinutes = Mathf.Max(0f, nightColorBlendDurationMinutes);
        nightIntensityBlendDurationMinutes = Mathf.Max(0f, nightIntensityBlendDurationMinutes);
        shadowToggleLerpSpeed = Mathf.Max(0.01f, shadowToggleLerpSpeed);
        noShadowDimWhenOff = Mathf.Clamp(noShadowDimWhenOff, 0.5f, 1f);
        rainNoShadowThreshold = Mathf.Max(0f, rainNoShadowThreshold);
        maxSunAltitude = Mathf.Clamp(maxSunAltitude, 0f, 90f);
    }
#endif
}