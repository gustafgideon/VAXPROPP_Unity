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

    [Header("Sun / Main Directional Light")]
    [SerializeField] private Light globalLight;

    [Header("Time Settings")]
    [Tooltip("Real-time minutes for a full 24h in-game day.")]
    [SerializeField] private float fullDayLengthMinutes = 4f;

    [Header("Phase Transition Blend (In-Game Minutes)")]
    [Tooltip("Blend duration for light COLOR after a phase change.")]
    [SerializeField] private float stateColorBlendDurationMinutes = 30f;
    [Tooltip("Blend duration for light INTENSITY after a phase change.")]
    [SerializeField] private float stateIntensityBlendDurationMinutes = 20f;

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
    [SerializeField] private Color rainColdTint = new Color(0.65f, 0.72f, 0.85f, 1f); // bluish-gray
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
    [Tooltip("Global dim multiplier for the sun under rain (applied in addition to rainLightDarkeningCurve).")]
    [SerializeField] private AnimationCurve rainGlobalDimCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(0.15f, 0.92f),
        new Keyframe(0.5f, 0.75f),
        new Keyframe(1f, 0.6f)
    );

    [Tooltip("Ambient dimming when raining. If ambient mode is Skybox, this scales ambientIntensity; otherwise, scales ambientLight color.")]
    [SerializeField] private AnimationCurve ambientDimByRain = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(1f, 0.55f)
    );

    [Tooltip("Skybox exposure multiplier under rain (darker sky).")]
    [SerializeField] private AnimationCurve skyboxExposureByRain = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(1f, 0.7f)
    );

    [Tooltip("Fog density multiplier under rain (thicker fog). Requires fog enabled.")]
    [SerializeField] private AnimationCurve fogDensityByRain = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(1f, 1.6f)
    );

    [Header("Shadow Fade During Rain (No Hard Disable)")]
    [Tooltip("Rain intensity where shadow fading begins (toward min).")]
    [SerializeField] private float shadowFadeStart = 0.12f;
    [Tooltip("Rain intensity where shadow fade target reaches its minimum.")]
    [SerializeField] private float shadowFadeEnd = 0.28f;
    [Tooltip("Speed (units per second) for currentShadowFactor to chase target.")]
    [SerializeField] private float shadowFadeLerpSpeed = 2.5f;
    [Tooltip("Minimum relative shadow factor at maximum rain (keep some soft shadowing for contrast).")]
    [Range(0f, 1f)]
    [SerializeField] private float minShadowFactorAtMaxRain = 0.25f;

    [Header("Base Light Intensity Over 24h")]
    [SerializeField] private AnimationCurve lightIntensityOverDay = new AnimationCurve(
        new Keyframe(0f,    0.05f),
        new Keyframe(0.21f, 0.5f),
        new Keyframe(0.29f, 0.85f),
        new Keyframe(0.5f,  1.0f),
        new Keyframe(0.71f, 0.85f),
        new Keyframe(0.79f, 0.55f),
        new Keyframe(1f,    0.05f)
    );

    [Header("FMOD Settings")]
    [SerializeField] private string timeOfDayParameterName = "TimeOfDay";

    [Header("Weather System (Optional)")]
    [SerializeField] private WeatherSystemManager weatherSystem;

    [Header("Debug Options")]
    [SerializeField] private bool debugDayStateChanges = true;
    [SerializeField] private bool debugShadowTransitions = false;
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

    // Shadows (no hard disable)
    private float originalShadowStrength = 1f;
    private float currentShadowFactor = 1f; // 1 = full, minShadowFactorAtMaxRain at max rain

    // Skybox exposure and ambient originals
    private float originalAmbientIntensity = 1f;
    private Color originalAmbientLight;
    private bool hasSkyboxExposureProps = false;
    private float baseFogDensity = 0.01f; // will be sampled at Start

    private void Start()
    {
        if (globalLight == null)
        {
            Debug.LogWarning("TimeManager: No globalLight assigned.");
            enabled = false; return;
        }

        originalShadowStrength = globalLight.shadowStrength;

        // Cache ambient baselines
        originalAmbientIntensity = RenderSettings.ambientIntensity;
        originalAmbientLight = RenderSettings.ambientLight;

        // Cache fog baseline if enabled
        if (RenderSettings.fog) baseFogDensity = RenderSettings.fogDensity;

        currentState = GetCurrentDayState(hours);
        previousState = currentState;
        stateStartTotalMinutes = GetTotalMinutes();
        previousPhaseColor = globalLight.color;
        previousPhaseIntensity = globalLight.intensity;

        if (debugDayStateChanges)
            Debug.Log($"[TimeManager] Initial state: {currentState} at {hours:00}:{minutes:00}");

        UpdateSkyboxTextures();
        DetectSkyboxExposureProps();
        UpdateTimeOfDayLerp();
        UpdateLightingContinuous();
        UpdateSunRotation();

        previousPhaseColor = globalLight.color;
        previousPhaseIntensity = globalLight.intensity;
    }

    private void Update()
    {
        AdvanceTime();
        UpdateSunRotation();
        UpdateState();

        UpdateTimeOfDayLerp();
        UpdateSkyboxBlend();
        UpdateLightingContinuous();
        UpdateSkyboxExposure(); // envelope affects skybox exposure too
        UpdateAmbientAndFogEnvelope(); // envelope affects ambient + fog
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
        // Exposure now controlled in UpdateSkyboxExposure()
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
        float expo = Mathf.Clamp(skyboxExposureByRain.Evaluate(rain), 0.05f, 2f);
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

    #region Lighting, Envelope, Shadows

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

        // Raw target intensity (pre-phase-blend)
        float dayProgress = GetDayProgress();
        float baseIntensity = lightIntensityOverDay.Evaluate(dayProgress);
        float rainMultiplier = rainLightDarkeningCurve.Evaluate(rain);
        float targetPhaseIntensity = baseIntensity * rainMultiplier;

        // Phase blending (in-game minutes)
        float elapsedInState = GetTotalMinutes() - stateStartTotalMinutes;
        float colorBlendAlpha = stateColorBlendDurationMinutes <= 0f ? 1f : Mathf.Clamp01(elapsedInState / stateColorBlendDurationMinutes);
        float intensityBlendAlpha = stateIntensityBlendDurationMinutes <= 0f ? 1f : Mathf.Clamp01(elapsedInState / stateIntensityBlendDurationMinutes);

        Color blendedColor = Color.Lerp(previousPhaseColor, targetPhaseColor, colorBlendAlpha);
        float blendedIntensity = Mathf.Lerp(previousPhaseIntensity, targetPhaseIntensity, intensityBlendAlpha);

        // Shadow fade (no hard disable; keep minimum shadowing)
        UpdateShadowFadeNoDisable(rain);

        // Apply global rain envelope dimming to the sun
        float globalDim = Mathf.Clamp01(rainGlobalDimCurve.Evaluate(rain));
        float finalSunIntensity = blendedIntensity * globalDim;

        // Apply to light
        globalLight.intensity = finalSunIntensity;
        globalLight.color = blendedColor;

        // Fog color follows light color (density handled in envelope)
        RenderSettings.fogColor = blendedColor;

        if (debugPhaseBlend)
        {
            if (elapsedInState < Mathf.Max(stateColorBlendDurationMinutes, stateIntensityBlendDurationMinutes))
            {
                Debug.Log($"[TimeManager] PhaseBlend state={currentState} elapsed={elapsedInState:0.0}m colorA={colorBlendAlpha:0.00} intensA={intensityBlendAlpha:0.00} rain={rain:0.00}");
            }
        }
    }

    private void UpdateShadowFadeNoDisable(float rain)
    {
        // Compute target shadow factor from 1 -> minShadowFactorAtMaxRain
        float target;
        if (rain <= shadowFadeStart) target = 1f;
        else if (rain >= shadowFadeEnd) target = minShadowFactorAtMaxRain;
        else
        {
            float t = Mathf.InverseLerp(shadowFadeStart, shadowFadeEnd, rain); // 0..1
            target = Mathf.Lerp(1f, minShadowFactorAtMaxRain, t);
        }

        float prev = currentShadowFactor;
        currentShadowFactor = Mathf.MoveTowards(currentShadowFactor, target, shadowFadeLerpSpeed * Time.deltaTime);
        globalLight.shadowStrength = originalShadowStrength * currentShadowFactor;

        if (debugShadowTransitions && Mathf.Abs(prev - currentShadowFactor) > 0.001f)
        {
            Debug.Log($"[TimeManager] ShadowFade (no disable) rain={rain:0.00} target={target:0.00} current={currentShadowFactor:0.00}");
        }
    }

    private void UpdateAmbientAndFogEnvelope()
    {
        float rain = weatherSystem ? weatherSystem.rainIntensity : 0f;

        // Ambient
        float ambientDim = Mathf.Clamp01(ambientDimByRain.Evaluate(rain));

        if (RenderSettings.ambientMode == AmbientMode.Skybox)
        {
            RenderSettings.ambientIntensity = originalAmbientIntensity * ambientDim;
        }
        else
        {
            // For Flat/Trilight, gently cool and dim ambientLight
            Color targetAmbient = Color.Lerp(originalAmbientLight, rainColdTint, Mathf.Clamp01(rain * 0.6f));
            targetAmbient *= ambientDim;
            RenderSettings.ambientLight = targetAmbient;
        }

        // Fog density (if enabled)
        if (RenderSettings.fog)
        {
            float fogMul = Mathf.Max(0f, fogDensityByRain.Evaluate(rain));
            RenderSettings.fogDensity = baseFogDensity * fogMul;
        }

        if (debugEnvelope)
        {
            Debug.Log($"[TimeManager] Envelope rain={rain:0.00} ambDim={ambientDim:0.00} fogDens={RenderSettings.fogDensity:0.0000}");
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

    private void UpdateSunRotation()
    {
        float dayProgress = GetDayProgress();
        float sunAngle = dayProgress * 360f;
        globalLight.transform.rotation = Quaternion.Euler(30f, sunAngle - 90f, 0f);
    }

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
        shadowFadeEnd = Mathf.Max(shadowFadeEnd, shadowFadeStart + 0.001f);
        minShadowFactorAtMaxRain = Mathf.Clamp01(minShadowFactorAtMaxRain);
        stateColorBlendDurationMinutes = Mathf.Max(0f, stateColorBlendDurationMinutes);
        stateIntensityBlendDurationMinutes = Mathf.Max(0f, stateIntensityBlendDurationMinutes);
    }
#endif
}