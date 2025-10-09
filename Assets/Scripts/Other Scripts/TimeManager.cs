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
    [Tooltip("Duration (in GAME minutes) to blend light COLOR after a phase change.")]
    [SerializeField] private float stateColorBlendDurationMinutes = 30f;
    [Tooltip("Duration (in GAME minutes) to blend light INTENSITY after a phase change.")]
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
    [Tooltip("Force a cold tint when raining, regardless of phase gradients.")]
    [SerializeField] private bool forceColdTintInRain = true;
    [Tooltip("Cold tint target color used during rain.")]
    [SerializeField] private Color rainColdTint = new Color(0.65f, 0.72f, 0.85f, 1f); // bluish-gray
    [Tooltip("How strongly to bias the light color toward the cold tint as rain increases.")]
    [SerializeField] private AnimationCurve rainColdTintStrengthByRain = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.2f, 0.5f),
        new Keyframe(1f, 1f)
    );
    [Tooltip("Desaturation amount as rain increases (0=no extra desat, 1=grayscale).")]
    [SerializeField] private AnimationCurve rainDesaturationByRain = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(1f, 0.6f)
    );

    [Header("Shadow Fade During Rain (Bidirectional)")]
    [Tooltip("Rain intensity where shadow fading begins (toward 0).")]
    [SerializeField] private float shadowFadeStart = 0.12f;
    [Tooltip("Rain intensity where shadow fade target reaches 0.")]
    [SerializeField] private float shadowFadeEnd = 0.28f;
    [Tooltip("Speed (units per second) for currentShadowFactor to chase target.")]
    [SerializeField] private float shadowFadeLerpSpeed = 2.5f;
    [Tooltip("Below this shadowStrength (relative) we may fully disable shadows for performance.")]
    [SerializeField] private float minShadowStrengthBeforeDisable = 0.05f;
    [Tooltip("Rain must fall this far below shadowFadeStart before we re-enable physically disabled shadows.")]
    [SerializeField] private float shadowDisableHysteresis = 0.03f;
    [Tooltip("Completely disable Light.shadows after fade reaches near zero? If false, we just keep a very low strength.")]
    [SerializeField] private bool disableShadowsCompletelyAtEnd = true;
    [Tooltip("Intensity multiplier when shadows are fully gone (compensates perceived brightening).")]
    [Range(0.5f, 1f)]
    [SerializeField] private float shadowRemovalDimFactor = 0.9f;

    [Header("Base Light Intensity Over 24h")]
    [SerializeField] private AnimationCurve lightIntensityOverDay = new AnimationCurve(
        new Keyframe(0f,    0.05f),
        new Keyframe(0.21f, 0.5f),   // ~05:00
        new Keyframe(0.29f, 0.85f),  // ~07:00
        new Keyframe(0.5f,  1.0f),   // Midday
        new Keyframe(0.71f, 0.85f),  // ~17:00
        new Keyframe(0.79f, 0.55f),  // ~19:00
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

    private int minutes;
    private int hours = 5; // start at dawn
    private float tempSecond;

    private enum DayState { Night, Dawn, Day, Dusk }
    private DayState currentState;

    // For transition blending
    private DayState previousState;
    private float stateStartTotalMinutes;            // In-game minute stamp when current state started
    private Color previousPhaseColor;
    private float previousPhaseIntensity;

    // Shadow management
    private LightShadows originalShadowType;
    private float originalShadowStrength = 1f;
    private float currentShadowFactor = 1f;    // 1 = full shadows, 0 = none (faded)
    private bool shadowsPhysicallyDisabled = false;

    private void Start()
    {
        if (globalLight == null)
        {
            Debug.LogWarning("TimeManager: No globalLight assigned.");
            enabled = false;
            return;
        }

        originalShadowType = globalLight.shadows;
        originalShadowStrength = globalLight.shadowStrength;

        currentState = GetCurrentDayState(hours);
        previousState = currentState;
        stateStartTotalMinutes = GetTotalMinutes();
        previousPhaseColor = globalLight.color;
        previousPhaseIntensity = globalLight.intensity;

        if (debugDayStateChanges)
            Debug.Log($"[TimeManager] Initial day state: {currentState} at {hours:00}:{minutes:00}");

        UpdateSkyboxTextures();
        UpdateTimeOfDayLerp();
        UpdateLightingContinuous(); // initializes light color/intensity properly
        UpdateSunRotation();

        // Capture a correct initial snapshot after first proper lighting calc
        previousPhaseColor = globalLight.color;
        previousPhaseIntensity = globalLight.intensity;
    }

    private void Update()
    {
        AdvanceTime();
        UpdateSunRotation();
        UpdateState();        // Might trigger new blend

        UpdateTimeOfDayLerp();
        UpdateSkyboxBlend();
        UpdateLightingContinuous();
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

            // Snapshot BEFORE we recalc new color (we still have old state's last color/intensity)
            previousPhaseColor = globalLight.color;
            previousPhaseIntensity = globalLight.intensity;

            stateStartTotalMinutes = GetTotalMinutes();

            if (debugDayStateChanges)
            {
                float rain = weatherSystem ? weatherSystem.rainIntensity : 0f;
                Debug.Log($"[TimeManager] Day state changed {previousState} -> {currentState} at {hours:00}:{minutes:00} (rain={rain:0.00})");
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
        mat.SetFloat("_Exposure1", 1f);
        mat.SetFloat("_Exposure2", 1f);
        mat.SetTexture("_OvercastTexture", GetOvercastSkyForState(currentState));
    }

    private void UpdateTimeOfDayLerp()
    {
        if (RenderSettings.skybox == null) return;

        float hourFraction = hours + minutes / 60f;
        float lerp = 0f;

        switch (currentState)
        {
            case DayState.Dawn:
                lerp = Mathf.InverseLerp(5f, 7f, hourFraction);
                break;
            case DayState.Day:
                lerp = Mathf.InverseLerp(7f, 18f, hourFraction);
                break;
            case DayState.Dusk:
                lerp = Mathf.InverseLerp(18f, 21f, hourFraction);
                break;
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
        DayState.Day => skyboxDay,
        DayState.Dusk => skyboxDusk,
        DayState.Night => skyboxNight,
        _ => skyboxNight
    };

    private Texture2D GetNextBaseSkyForLerp(DayState s) => s switch
    {
        DayState.Night => skyboxDawn,
        DayState.Dawn => skyboxDay,
        DayState.Day => skyboxDusk,
        DayState.Dusk => skyboxNight,
        _ => skyboxNight
    };

    private Texture2D GetOvercastSkyForState(DayState s) => s switch
    {
        DayState.Dawn => overcastDawn,
        DayState.Day => overcastDay,
        DayState.Dusk => overcastDusk,
        DayState.Night => overcastNight,
        _ => overcastNight
    };

    #endregion

    #region Lighting & Shadows

    private void UpdateLightingContinuous()
    {
        if (!globalLight) return;

        float rain = weatherSystem ? weatherSystem.rainIntensity : 0f;

        // Raw target color from gradients (WITHOUT phase blend)
        float segmentProgress = GetSegmentProgress();
        Color normalColor = GetNormalGradientForState(currentState).Evaluate(segmentProgress);
        Color overcastColor = GetOvercastGradientForState(currentState).Evaluate(segmentProgress);
        float overcastBlend = overcastBlendCurve.Evaluate(rain);
        Color targetPhaseColor = Color.Lerp(normalColor, overcastColor, overcastBlend);

        // Force a cold tint under rain, regardless of warm gradients
        if (forceColdTintInRain && rain > 0f)
        {
            float coldStrength = Mathf.Clamp01(rainColdTintStrengthByRain.Evaluate(rain));
            targetPhaseColor = Color.Lerp(targetPhaseColor, rainColdTint, coldStrength);

            float desat = Mathf.Clamp01(rainDesaturationByRain.Evaluate(rain));
            targetPhaseColor = Desaturate(targetPhaseColor, desat);
        }

        // Raw target intensity (WITHOUT phase blend)
        float dayProgress = GetDayProgress();
        float baseIntensity = lightIntensityOverDay.Evaluate(dayProgress);
        float rainMultiplier = rainLightDarkeningCurve.Evaluate(rain);
        float targetPhaseIntensity = baseIntensity * rainMultiplier;

        // Compute phase blend alphas (0..1) in in-game minutes
        float elapsedInState = GetTotalMinutes() - stateStartTotalMinutes;

        float colorBlendAlpha = stateColorBlendDurationMinutes <= 0f
            ? 1f
            : Mathf.Clamp01(elapsedInState / stateColorBlendDurationMinutes);

        float intensityBlendAlpha = stateIntensityBlendDurationMinutes <= 0f
            ? 1f
            : Mathf.Clamp01(elapsedInState / stateIntensityBlendDurationMinutes);

        // Final blended color & intensity
        Color blendedColor = Color.Lerp(previousPhaseColor, targetPhaseColor, colorBlendAlpha);
        float blendedIntensity = Mathf.Lerp(previousPhaseIntensity, targetPhaseIntensity, intensityBlendAlpha);

        // Shadows (bidirectional fade)
        UpdateShadowFade(rain);

        // Intensity compensation relative to shadow fade
        float dimComp = Mathf.Lerp(1f, shadowRemovalDimFactor, 1f - currentShadowFactor);
        globalLight.intensity = blendedIntensity * dimComp;
        globalLight.color = blendedColor;
        RenderSettings.fogColor = blendedColor;

        if (debugPhaseBlend)
        {
            if (elapsedInState < Mathf.Max(stateColorBlendDurationMinutes, stateIntensityBlendDurationMinutes))
            {
                Debug.Log($"[TimeManager] PhaseBlend state={currentState} elapsed={elapsedInState:0.0}m " +
                          $"colorA={colorBlendAlpha:0.00} intensA={intensityBlendAlpha:0.00} rain={rain:0.00}");
            }
        }
    }

    private void UpdateShadowFade(float rain)
    {
        float targetShadowFactor;
        if (rain <= shadowFadeStart) targetShadowFactor = 1f;
        else if (rain >= shadowFadeEnd) targetShadowFactor = 0f;
        else
        {
            float t = Mathf.InverseLerp(shadowFadeStart, shadowFadeEnd, rain);
            targetShadowFactor = 1f - t;
        }

        // Re-enable physically disabled shadows if we need >0 factor and conditions allow
        if (shadowsPhysicallyDisabled && targetShadowFactor > 0f && rain < (shadowFadeStart - shadowDisableHysteresis))
        {
            globalLight.shadows = originalShadowType;
            shadowsPhysicallyDisabled = false;
            if (debugShadowTransitions)
                Debug.Log($"[TimeManager] Re-enabling shadows (rain={rain:0.00})");
        }

        currentShadowFactor = Mathf.MoveTowards(
            currentShadowFactor,
            targetShadowFactor,
            shadowFadeLerpSpeed * Time.deltaTime
        );

        if (!shadowsPhysicallyDisabled)
        {
            globalLight.shadowStrength = originalShadowStrength * currentShadowFactor;

            if (disableShadowsCompletelyAtEnd &&
                targetShadowFactor == 0f &&
                currentShadowFactor <= minShadowStrengthBeforeDisable)
            {
                globalLight.shadows = LightShadows.None;
                shadowsPhysicallyDisabled = true;
                if (debugShadowTransitions)
                    Debug.Log($"[TimeManager] Shadows fully disabled (rain={rain:0.00}).");
            }
        }
        else
        {
            globalLight.shadowStrength = 0f;
        }

        if (debugShadowTransitions)
        {
            Debug.Log($"[TimeManager] ShadowFade rain={rain:0.00} target={targetShadowFactor:0.00} current={currentShadowFactor:0.00} physDisabled={shadowsPhysicallyDisabled}");
        }
    }

    private float GetSegmentProgress()
    {
        float hf = hours + minutes / 60f;
        return currentState switch
        {
            DayState.Dawn => Mathf.InverseLerp(5f, 7f, hf),
            DayState.Day => Mathf.InverseLerp(7f, 18f, hf),
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
        DayState.Day => gradientDawnToDay,
        DayState.Dusk => gradientDayToDusk,
        DayState.Night => gradientDuskToNight,
        _ => gradientDuskToNight
    };

    private Gradient GetOvercastGradientForState(DayState s) => s switch
    {
        DayState.Dawn => overcastGradientDawn,
        DayState.Day => overcastGradientDay,
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
        minShadowStrengthBeforeDisable = Mathf.Clamp01(minShadowStrengthBeforeDisable);
        shadowRemovalDimFactor = Mathf.Clamp(shadowRemovalDimFactor, 0.5f, 1f);
        shadowFadeLerpSpeed = Mathf.Max(0.01f, shadowFadeLerpSpeed);
        stateColorBlendDurationMinutes = Mathf.Max(0f, stateColorBlendDurationMinutes);
        stateIntensityBlendDurationMinutes = Mathf.Max(0f, stateIntensityBlendDurationMinutes);
    }
#endif
}