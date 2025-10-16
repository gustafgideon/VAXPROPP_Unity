using UnityEngine;
using UnityEngine.Rendering; // For RenderSettings.ambientMode
using FMODUnity;
using System.Collections;

public class TimeManager : MonoBehaviour
{
    [Header("Day Cycle")] [Tooltip("Real-time minutes for a full 24h in-game day.")] [Range(1f, 60f)] [SerializeField]
    private float fullDayLengthMinutes = 20f;

    [Header("Skybox Textures")] 
    [SerializeField] private Texture2D skyboxNightTexture;
    [SerializeField] private Texture2D skyboxDawnTexture;
    [SerializeField] private Texture2D skyboxDayTexture;
    [SerializeField] private Texture2D skyboxDuskTexture;
    
    [Header("Overcast Texture")] 
    [SerializeField] private Texture2D overcastTexture;

    [Header("Main Skybox Material")] 
    [SerializeField] private Material skyboxMaterial;

    [Header("Skybox Settings")] [Tooltip("Should the skybox rotate?")] [SerializeField]
    private bool enableSkyboxRotation = true;

    [Tooltip("Rotation speed of skybox in degrees per second")] [SerializeField]
    private float skyboxRotationSpeed = 0.5f;

    [Tooltip("How long skybox transitions take in minutes")] [Range(0.1f, 5f)] [SerializeField]
    private float skyboxBlendDurationMinutes = 1f;

    [Header("Sun / Main Directional Light")] [SerializeField]
    private Light globalLight;

    [Header("Moon / Secondary Light")] 
    [SerializeField] private Light moonLight;

    [Header("Sun Path")]
    [Tooltip("Maximum altitude of the sun above horizon (degrees). Night drives it below 0.")]
    [Range(0f, 90f)]
    [SerializeField]
    private float maxSunAltitude = 55f;

    [Header("Light Intensities")]
    [SerializeField] private float dayLightIntensity = 1.2f;
    [SerializeField] private float nightLightIntensity = 0.05f;
    [SerializeField] private float moonLightIntensity = 0.08f;
    [Tooltip("How much to reduce light intensity during rain (0-1)")]
    [SerializeField][Range(0f, 1f)] private float rainLightReduction = 0.6f;

    [Header("Smooth Transition Settings")]
    [Tooltip("How long light color transitions take when changing day states (in seconds)")]
    [SerializeField][Range(0.5f, 10f)] private float lightTransitionDuration = 3f;

    [Tooltip("How quickly the dusk red tint fades to night blue (higher = faster)")]
    [SerializeField][Range(0.1f, 5f)] private float duskToNightTintSpeed = 2f;

    [Header("Shadow Settings (Clear Weather)")]
    [Tooltip("Shadow strength during clear day (0-1)")]
    [SerializeField][Range(0f, 1f)] private float dayShadowStrength = 0.7f;
    [Tooltip("Shadow strength during clear night (0-1)")]
    [SerializeField][Range(0f, 1f)] private float nightShadowStrength = 0.9f;

    [Header("Shadow Settings (Overcast/Rain)")]
    [Tooltip("Shadow strength during overcast day (0-1)")]
    [SerializeField][Range(0f, 1f)] private float overcastDayShadowStrength = 0.4f;
    [Tooltip("Shadow strength during overcast night (0-1)")]
    [SerializeField][Range(0f, 1f)] private float overcastNightShadowStrength = 0.5f;
    [Tooltip("How soft shadows become during rain (higher = softer shadows)")]
    [SerializeField][Range(0f, 5f)] private float overcastShadowNormalBias = 0.4f;
    [Tooltip("Normal shadow bias value during clear weather")]
    [SerializeField][Range(0f, 5f)] private float clearShadowNormalBias = 0.2f;

    [Header("Light Color Gradients (Per Segment)")] [SerializeField]
    private Gradient gradientNightToDawn;

    [SerializeField] private Gradient gradientDawnToDay;
    [SerializeField] private Gradient gradientDayToDusk;
    [SerializeField] private Gradient gradientDuskToNight;

    [Header("Overcast Color Gradients (Per Segment)")] [SerializeField]
    private Gradient overcastGradientNight;

    [SerializeField] private Gradient overcastGradientDawn;
    [SerializeField] private Gradient overcastGradientDay;
    [SerializeField] private Gradient overcastGradientDusk;

    [Header("Ambient Light Colors")]
    [SerializeField] private Color daySkyColor = new Color(0.5f, 0.5f, 0.73f);
    [SerializeField] private Color dayEquatorColor = new Color(0.6f, 0.6f, 0.6f);
    [SerializeField] private Color dayGroundColor = new Color(0.4f, 0.38f, 0.35f);
    [SerializeField] private Color nightSkyColor = new Color(0.05f, 0.05f, 0.1f);
    [SerializeField] private Color nightEquatorColor = new Color(0.04f, 0.04f, 0.08f);
    [SerializeField] private Color nightGroundColor = new Color(0.02f, 0.02f, 0.04f);

    [Header("Fog Settings")]
    [SerializeField] private float dayFogDensity = 0.005f;
    [SerializeField] private float nightFogDensity = 0.03f;
    [Tooltip("Additional fog density during rain")]
    [SerializeField] private float rainFogDensityAdditive = 0.02f;

    [Header("Lightning Settings")]
    [SerializeField] private float lightningMaxIntensity = 8f;
    [SerializeField] private float lightningMinIntensity = 3f;
    [SerializeField] private float lightningDuration = 0.2f;
    [SerializeField] private Color lightningColor = new Color(0.8f, 0.9f, 1.0f);

    [Header("Close Lightning Boost")]
    [Tooltip("Extra intensity multiplier applied when thunderLevel == 3")]
    [SerializeField] private float closeLightningIntensityMultiplier = 1.3f;

    [Header("FMOD Settings")] [SerializeField]
    private string timeOfDayParameterName = "TimeOfDay";

    [Header("Weather System")] 
    [SerializeField] private WeatherSystemManager weatherSystem;

    [Header("Rain Visual Response")]
    [Tooltip("Non-linear exponent mapping for how quickly the skybox goes overcast as rain rises. <1 reacts faster.")]
    [SerializeField][Range(0.1f, 2f)] private float rainToOvercastExponent = 0.35f;

    [Tooltip("Linear gain applied before exponent to make overcast kick in earlier. 1 = no gain.")]
    [SerializeField][Range(0.1f, 8f)] private float rainToOvercastGain = 4f;

    [Tooltip("Rain value above which a minimum overcast blend is enforced.")]
    [SerializeField][Range(0f, 1f)] private float rainOvercastThreshold = 0.02f;

    [Tooltip("Minimum overcast blend applied whenever rain >= threshold.")]
    [SerializeField][Range(0f, 1f)] private float minOvercastBlend = 0.45f;

    [Tooltip("Cold tint applied to sun light while raining (makes light cold regardless of time of day).")]
    [SerializeField] private Color rainColdLightTint = new Color(0.65f, 0.72f, 1.0f);

    [Tooltip("Max strength of the cold tint under rain.")]
    [SerializeField][Range(0f, 1f)] private float rainColdTintMaxStrength = 0.85f;

    [Tooltip("Exponent shaping how quickly the cold tint ramps in with rain (<1 reacts fast).")]
    [SerializeField][Range(0.1f, 2f)] private float rainColdTintExponent = 0.4f;

    [Header("Night Brightness")]
    [Tooltip("Minimum directional light intensity enforced at night.")]
    [SerializeField][Range(0f, 1f)] private float minNightDirectionalIntensity = 0.15f;

    [Tooltip("Minimum ambient intensity at night (before global multiplier).")]
    [SerializeField][Range(0f, 1f)] private float nightAmbientFloor = 0.2f;

    [Tooltip("Global multiplier applied to RenderSettings.ambientIntensity.")]
    [SerializeField][Range(0f, 2f)] private float ambientGlobalMultiplier = 1.0f;

    [Tooltip("Multiplier applied to moon light intensity.")]
    [SerializeField][Range(0f, 3f)] private float moonLightMultiplier = 1.5f;

    [Header("Debug Options")] [SerializeField]
    private bool debugDayStateChanges = true;

    [SerializeField] private bool debugSkybox = false;

    private int minutes;
    private int hours = 5; // start at dawn
    private float tempSecond;
    private float skyboxRotationValue = 0f;

    private enum DayState
    {
        Night,
        Dawn,
        Day,
        Dusk
    }

    private DayState currentState;
    private DayState previousState;

    // Skybox blending
    private float blendFactor = 0f;
    private float blendStartTime;
    private bool isBlending = false;
    private Texture2D textureFrom;
    private Texture2D textureTo;

    // Cached sun height (0..1, 0 night, 1 high noon)
    private float sunHeight01 = 0f;
    
    // Lightning effect
    private Color originalLightColor;
    private float originalLightIntensity;
    private bool isLightningActive = false;

    // Smooth light color transition variables
    private Color previousLightColor;
    private Color targetLightColor;
    private bool isTransitioningLight = false;
    private float lightTransitionStartTime;
    private float lightTransitionProgress = 1f;

    // UI Helpers for debug phase logging
    private static string GetPhaseIcon(DayState s) => s switch
    {
        DayState.Dawn => "🌅",
        DayState.Day  => "☀️",
        DayState.Dusk => "🌇",
        DayState.Night=> "🌙",
        _ => ""
    };

    private static string GetPhaseText(DayState s) => s switch
    {
        DayState.Dawn => "DAWN",
        DayState.Day  => "DAY",
        DayState.Dusk => "DUSK",
        DayState.Night=> "NIGHT",
        _ => "UNKNOWN"
    };

    private void Start()
    {
        if (globalLight == null)
        {
            Debug.LogError("TimeManager: No globalLight assigned! Please assign a directional light.");
            enabled = false;
            return;
        }

        if (skyboxMaterial == null)
        {
            Debug.LogError("TimeManager: No skyboxMaterial assigned! Please assign your panoramic skybox material.");
            enabled = false;
            return;
        }

        if (!skyboxMaterial.shader.name.Contains("Panoramic"))
        {
            Debug.LogWarning("TimeManager: Skybox material doesn't use the Dual Panoramic shader. Blending might not work properly.");
        }

        currentState = GetCurrentDayState(hours);
        previousState = currentState;

        // Initialize skybox textures
        textureFrom = GetTextureForState(currentState);
        textureTo = textureFrom;

        // Setup initial skybox state
        skyboxMaterial.SetTexture("_Texture1", textureFrom);
        skyboxMaterial.SetTexture("_Texture2", textureFrom);
        skyboxMaterial.SetTexture("_OvercastTexture", overcastTexture);
        skyboxMaterial.SetFloat("_TimeOfDayLerp", 0);
        
        // Set the skybox material
        RenderSettings.skybox = skyboxMaterial;

        // Set ambient mode to trilight for better day/night control
        RenderSettings.ambientMode = AmbientMode.Trilight;

        UpdateSunRotation();

        // Initialize smooth light transitions
        float rain = weatherSystem ? weatherSystem.rainIntensity : 0f;
        Color initialColor = CalculateContinuousLightColor(rain);
        previousLightColor = initialColor;
        targetLightColor = initialColor;
        globalLight.color = initialColor;
        
        UpdateLightingContinuous();

        SetTimeOfDayParameter(currentState);

        if (debugDayStateChanges)
            Debug.Log($"[TimeManager] {GetPhaseIcon(currentState)} {GetPhaseText(currentState)} at {hours:00}:{minutes:00}");
            
        // Subscribe to thunder visuals (manual trigger comes from WeatherSystemManager.GenerateThunder)
        if (weatherSystem != null)
        {
            weatherSystem.OnThunderTriggered += CreateLightningEffect;
        }
        else
        {
            Debug.LogWarning("TimeManager: WeatherSystemManager reference not set. Lightning effects won't work.");
        }
    }

    private void Update()
    {
        AdvanceTime();
        UpdateSunRotation();
        UpdateState();

        // Update skybox blending if in progress
        if (isBlending)
        {
            UpdateSkyboxBlend();
        }

        // Update skybox rotation (separate from state updates)
        if (enableSkyboxRotation)
            RotateSkybox();

        // Update weather blend in shader with fast overcast response
        if (skyboxMaterial != null)
        {
            float rain = weatherSystem ? weatherSystem.rainIntensity : 0f;
            skyboxMaterial.SetFloat("_Blend", ComputeOvercastBlend(rain));
        }

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

            if (debugDayStateChanges)
            {
                Debug.Log($"[TimeManager] {GetPhaseIcon(currentState)} {GetPhaseText(currentState)} at {hours:00}:{minutes:00}");
            }

            StartSkyboxBlend(previousState, currentState);
            SetTimeOfDayParameter(newState);
        }
    }

    private void StartSkyboxBlend(DayState fromState, DayState toState)
    {
        if (skyboxMaterial == null) return;

        // Set the appropriate textures based on states
        textureFrom = GetTextureForState(fromState);
        textureTo = GetTextureForState(toState);
        
        skyboxMaterial.SetTexture("_Texture1", textureFrom);
        skyboxMaterial.SetTexture("_Texture2", textureTo);
        
        // Set overcast texture
        skyboxMaterial.SetTexture("_OvercastTexture", overcastTexture);
        
        // Set rain blend with fast overcast response
        float rain = weatherSystem ? weatherSystem.rainIntensity : 0f;
        skyboxMaterial.SetFloat("_Blend", ComputeOvercastBlend(rain));
        
        // Initialize blend
        blendFactor = 0f;
        blendStartTime = Time.time;
        isBlending = true;
        
        // Update shader with initial blend value
        skyboxMaterial.SetFloat("_TimeOfDayLerp", blendFactor);
        
        if (debugSkybox)
            Debug.Log($"[TimeManager] Started skybox blend: {fromState} -> {toState}, rain={rain:F2}");
    }

    private void UpdateSkyboxBlend()
    {
        if (!isBlending || skyboxMaterial == null) return;

        // Calculate blend progress over time
        float elapsedMinutes = (Time.time - blendStartTime) / 60f;
        blendFactor = Mathf.Clamp01(elapsedMinutes / skyboxBlendDurationMinutes);

        // Update the shader's time of day lerp parameter
        skyboxMaterial.SetFloat("_TimeOfDayLerp", blendFactor);

        // Check if blend complete
        if (blendFactor >= 1.0f)
        {
            isBlending = false;
            
            // When blend is complete, set Texture1 to the destination texture
            // for smooth transitions to the next state
            skyboxMaterial.SetTexture("_Texture1", textureTo);
            
            if (debugSkybox)
                Debug.Log($"[TimeManager] Completed skybox blend to {currentState}");
        }
    }

    private void RotateSkybox()
    {
        if (skyboxMaterial == null) return;

        // Simple continuous rotation for the skybox
        skyboxRotationValue += skyboxRotationSpeed * Time.deltaTime;
        if (skyboxRotationValue >= 360f)
            skyboxRotationValue -= 360f;

        // Apply rotation to both texture rotations in the shader
        skyboxMaterial.SetFloat("_Rotation1", skyboxRotationValue);
        skyboxMaterial.SetFloat("_Rotation2", skyboxRotationValue);
    }

    private Texture2D GetTextureForState(DayState s) => s switch
    {
        DayState.Dawn => skyboxDawnTexture,
        DayState.Day => skyboxDayTexture,
        DayState.Dusk => skyboxDuskTexture,
        DayState.Night => skyboxNightTexture,
        _ => skyboxNightTexture
    };

    private void UpdateSunRotation()
    {
        float dayProgress = GetDayProgress();

        // sin-based daily curve
        float t = (dayProgress - 0.25f) * Mathf.PI * 2f;
        float sunCurve = Mathf.Sin(t);

        // Cache [0..1] height for downstream lighting calcs
        sunHeight01 = Mathf.Clamp01(sunCurve * 0.5f + 0.5f);

        // IMPORTANT: Preserve sign before applying fractional power to avoid NaN
        float signedPow = Mathf.Sign(sunCurve) * Mathf.Pow(Mathf.Abs(sunCurve), 0.9f);
        float altitudeAngle = signedPow * maxSunAltitude;

        // Azimuth with slight offset
        float sunAngle = (dayProgress * 360f) + 15f;

        // Safety guards (belt-and-suspenders)
        if (float.IsNaN(altitudeAngle) || float.IsInfinity(altitudeAngle)) altitudeAngle = 0f;
        if (float.IsNaN(sunAngle) || float.IsInfinity(sunAngle)) sunAngle = 0f;

        globalLight.transform.rotation = Quaternion.Euler(altitudeAngle, sunAngle - 90f, 0f);
    }

    private void UpdateLightingContinuous()
    {
        if (!globalLight) return;
        
        // Skip lighting updates if lightning is active
        if (isLightningActive) return;

        float rain = weatherSystem ? weatherSystem.rainIntensity : 0f;

        // Calculate the target color using continuous time instead of discrete states
        Color newTargetColor = CalculateContinuousLightColor(rain);
        
        // Handle smooth transitions when day state changes or weather changes significantly
        float colorDifference = Mathf.Abs(newTargetColor.r - targetLightColor.r) + 
                              Mathf.Abs(newTargetColor.g - targetLightColor.g) + 
                              Mathf.Abs(newTargetColor.b - targetLightColor.b);
        
        if (colorDifference > 0.05f && !isTransitioningLight)
        {
            // Start a new transition
            previousLightColor = globalLight.color;
            targetLightColor = newTargetColor;
            isTransitioningLight = true;
            lightTransitionStartTime = Time.time;
            lightTransitionProgress = 0f;
        }
        
        // Update transition progress
        if (isTransitioningLight)
        {
            lightTransitionProgress = (Time.time - lightTransitionStartTime) / lightTransitionDuration;
            
            if (lightTransitionProgress >= 1f)
            {
                // Transition complete
                isTransitioningLight = false;
                lightTransitionProgress = 1f;
                globalLight.color = targetLightColor;
            }
            else
            {
                // Smooth transition in progress
                globalLight.color = Color.Lerp(previousLightColor, targetLightColor, 
                    Mathf.SmoothStep(0f, 1f, lightTransitionProgress));
            }
        }
        else
        {
            // No transition needed, update normally but smoothly
            targetLightColor = newTargetColor;
            globalLight.color = Color.Lerp(globalLight.color, targetLightColor, Time.deltaTime * 2f);
        }

        // Dramatic light intensity change with a night floor
        float baseIntensity;
        if (sunHeight01 > 0.15f) {
            // Day/dusk intensity
            baseIntensity = Mathf.Lerp(0.3f, dayLightIntensity, Mathf.Pow(sunHeight01, 0.7f));
        } else {
            // Night intensity (less dark than before)
            baseIntensity = Mathf.Lerp(nightLightIntensity, 0.35f, sunHeight01 / 0.15f);
        }

        // Reduce intensity in rain using configurable reduction factor
        float rainDarkening = Mathf.Lerp(1f, 1f - rainLightReduction, rain);
        float finalDirectional = baseIntensity * rainDarkening;

        // Enforce a minimum floor at night so it's not too dark
        if (sunHeight01 < 0.2f)
            finalDirectional = Mathf.Max(finalDirectional, minNightDirectionalIntensity);

        globalLight.intensity = finalDirectional;
        
        // Update shadow properties based on time of day AND weather
        UpdateShadowSettings(rain);

        // Handle moon light
        UpdateMoonLight();

        // Update ambient lighting
        UpdateAmbientLighting();
        
        // Update fog settings
        UpdateFogSettings();
    }

    private Color CalculateContinuousLightColor(float rain)
    {
        float totalMinutes = GetTotalMinutes();
        
        // Create smooth transitions across the entire day using a continuous curve
        Color normalColor = GetContinuousNormalColor(totalMinutes);
        Color overcastColor = GetContinuousOvercastColor(totalMinutes);
        
        // Blend between normal and overcast based on rain
        float overcastBlend = ComputeOvercastBlend(rain);
        Color baseColor = Color.Lerp(normalColor, overcastColor, overcastBlend);

        // Apply cold tint during rain
        float tintStrength = (rain > 0f)
            ? Mathf.Clamp01(Mathf.Pow(rain, rainColdTintExponent) * rainColdTintMaxStrength)
            : 0f;
        
        return Color.Lerp(baseColor, rainColdLightTint, tintStrength);
    }

    private Color GetContinuousNormalColor(float totalMinutes)
    {
        // Define the color points for the day cycle
        float dawn = 5f * 60f;      // 5:00 AM
        float day = 7f * 60f;       // 7:00 AM
        float dusk = 18f * 60f;     // 6:00 PM
        float night = 21f * 60f;    // 9:00 PM
        
        // Handle the 24-hour wrap-around
        float adjustedMinutes = totalMinutes;
        if (totalMinutes < dawn)
            adjustedMinutes += 1440f; // Add 24 hours for night calculation
        
        Color resultColor;
        
        if (adjustedMinutes >= dawn && adjustedMinutes < day)
        {
            // Dawn period
            float progress = (adjustedMinutes - dawn) / (day - dawn);
            resultColor = gradientNightToDawn.Evaluate(progress);
        }
        else if (adjustedMinutes >= day && adjustedMinutes < dusk)
        {
            // Day period
            float progress = (adjustedMinutes - day) / (dusk - day);
            resultColor = gradientDawnToDay.Evaluate(progress);
        }
        else if (adjustedMinutes >= dusk && adjustedMinutes < night)
        {
            // Dusk period
            float progress = (adjustedMinutes - dusk) / (night - dusk);
            resultColor = gradientDayToDusk.Evaluate(progress);
        }
        else
        {
            // Night period - this is where we fix the red-to-blue transition
            float nightDuration = dawn + 1440f - night; // Total night duration
            float nightProgress;
            
            if (adjustedMinutes >= night)
            {
                nightProgress = (adjustedMinutes - night) / nightDuration;
            }
            else
            {
                nightProgress = (adjustedMinutes + 1440f - night) / nightDuration;
            }
            
            // Apply the dusk-to-night transition speed to make blue appear faster
            nightProgress = Mathf.Pow(nightProgress, 1f / duskToNightTintSpeed);
            nightProgress = Mathf.Clamp01(nightProgress);
            
            resultColor = gradientDuskToNight.Evaluate(nightProgress);
        }
        
        return resultColor;
    }

    private Color GetContinuousOvercastColor(float totalMinutes)
    {
        // Similar logic but using overcast gradients
        float dawn = 5f * 60f;
        float day = 7f * 60f;
        float dusk = 18f * 60f;
        float night = 21f * 60f;
        
        float adjustedMinutes = totalMinutes;
        if (totalMinutes < dawn)
            adjustedMinutes += 1440f;
        
        Color resultColor;
        
        if (adjustedMinutes >= dawn && adjustedMinutes < day)
        {
            float progress = (adjustedMinutes - dawn) / (day - dawn);
            resultColor = overcastGradientDawn.Evaluate(progress);
        }
        else if (adjustedMinutes >= day && adjustedMinutes < dusk)
        {
            float progress = (adjustedMinutes - day) / (dusk - day);
            resultColor = overcastGradientDay.Evaluate(progress);
        }
        else if (adjustedMinutes >= dusk && adjustedMinutes < night)
        {
            float progress = (adjustedMinutes - dusk) / (night - dusk);
            resultColor = overcastGradientDusk.Evaluate(progress);
        }
        else
        {
            float nightDuration = dawn + 1440f - night;
            float nightProgress;
            
            if (adjustedMinutes >= night)
            {
                nightProgress = (adjustedMinutes - night) / nightDuration;
            }
            else
            {
                nightProgress = (adjustedMinutes + 1440f - night) / nightDuration;
            }
            
            // Apply same speed adjustment for overcast night colors
            nightProgress = Mathf.Pow(nightProgress, 1f / duskToNightTintSpeed);
            nightProgress = Mathf.Clamp01(nightProgress);
            
            resultColor = overcastGradientNight.Evaluate(nightProgress);
        }
        
        return resultColor;
    }

    private void UpdateShadowSettings(float rainIntensity)
    {
        // Calculate shadow strength based on both time of day and rain intensity
        float clearWeatherShadowStrength = Mathf.Lerp(nightShadowStrength, dayShadowStrength, sunHeight01);
        float overcastShadowStrength = Mathf.Lerp(overcastNightShadowStrength, overcastDayShadowStrength, sunHeight01);
        
        // Blend between clear and overcast shadow strengths based on rain intensity
        float targetShadowStrength = Mathf.Lerp(clearWeatherShadowStrength, overcastShadowStrength, Mathf.Clamp01(rainIntensity * 2f));
        
        // Apply shadow strength to global light
        globalLight.shadowStrength = targetShadowStrength;
        
        // Adjust shadow normal bias for softer shadows during rain (available on the Light component)
        float targetNormalBias = Mathf.Lerp(clearShadowNormalBias, overcastShadowNormalBias, Mathf.Clamp01(rainIntensity * 2f));
        globalLight.shadowNormalBias = targetNormalBias;
    }

    private void UpdateMoonLight()
    {
        // Only show moon when sun is down
        if (sunHeight01 < 0.2f && moonLight != null) {
            // Activate the moon light
            moonLight.gameObject.SetActive(true);
            
            // Position moon opposite to sun (approximately)
            moonLight.transform.rotation = Quaternion.Euler(-globalLight.transform.eulerAngles.x, 
                                                         (globalLight.transform.eulerAngles.y + 180) % 360, 
                                                         0f);
            
            // Dim blue-tinted light with a multiplier
            float actualMoonIntensity = moonLightIntensity * (1 - sunHeight01 * 5) * moonLightMultiplier;
            
            // Reduce moon intensity in rain too
            float rain = weatherSystem ? weatherSystem.rainIntensity : 0f;
            float rainDarkening = Mathf.Lerp(1f, 1f - (rainLightReduction * 0.7f), rain);  // slightly less reduction than sun
            
            moonLight.intensity = actualMoonIntensity * rainDarkening;
            moonLight.color = new Color(0.6f, 0.65f, 1f);
            
            // Ensure moon has shadows enabled but less detailed
            moonLight.shadows = LightShadows.Soft;
            moonLight.shadowStrength = 0.6f;
        } else if (moonLight != null) {
            moonLight.gameObject.SetActive(false);
        }
    }

    private void UpdateAmbientLighting()
    {
        float rain = weatherSystem ? weatherSystem.rainIntensity : 0f;
        
        // Ambient intensity with a higher floor at night
        float ambientIntensity = Mathf.Lerp(nightAmbientFloor, 1.0f, Mathf.Pow(sunHeight01, 0.5f));
        
        // Reduce ambient intensity further in rain
        ambientIntensity *= Mathf.Lerp(1f, 0.7f, rain);
        
        // Blend between day and night ambient settings
        RenderSettings.ambientSkyColor = Color.Lerp(nightSkyColor, daySkyColor, ambientIntensity) * ambientIntensity;
        RenderSettings.ambientEquatorColor = Color.Lerp(nightEquatorColor, dayEquatorColor, ambientIntensity) * ambientIntensity;
        RenderSettings.ambientGroundColor = Color.Lerp(nightGroundColor, dayGroundColor, ambientIntensity) * ambientIntensity;
        
        // Apply global multiplier (lets you brighten nights without oversaturating colors)
        RenderSettings.ambientIntensity = ambientIntensity * ambientGlobalMultiplier;
    }

    private void UpdateFogSettings()
    {
        if (RenderSettings.fog) {
            // Get rain intensity
            float rain = weatherSystem ? weatherSystem.rainIntensity : 0f;
            
            // Use the same smooth color calculation for fog
            Color targetColor = CalculateContinuousLightColor(rain);
            
            // Apply fog color (slightly darker than sky)
            RenderSettings.fogColor = targetColor * 0.7f;
            
            // Make fog thicker at night and even thicker during rain
            float baseFogDensity = Mathf.Lerp(nightFogDensity, dayFogDensity, sunHeight01);
            float rainAddedFog = rain * rainFogDensityAdditive;
            
            RenderSettings.fogDensity = baseFogDensity + rainAddedFog;
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

    // Fast overcast response curve with gain + minimum floor when raining
    private float ComputeOvercastBlend(float rain)
    {
        if (rain <= 0f) return 0f;

        // Apply gain before exponent so small rain values already look overcast
        float x = Mathf.Clamp01(rain * rainToOvercastGain);
        float nonLinear = Mathf.Pow(x, rainToOvercastExponent);

        // Enforce a minimum overcast blend once threshold is crossed
        float floor = (rain >= rainOvercastThreshold) ? minOvercastBlend : 0f;

        return Mathf.Clamp01(Mathf.Max(nonLinear, floor));
    }

    #region FMOD

    private void SetTimeOfDayParameter(DayState state)
    {
        if (string.IsNullOrEmpty(timeOfDayParameterName)) return;

        float value = state switch
        {
            DayState.Dawn => 0f,
            DayState.Day => 1f,
            DayState.Dusk => 2f,
            DayState.Night => 3f,
            _ => 0f
        };

        try
        {
            RuntimeManager.StudioSystem.setParameterByName(timeOfDayParameterName, value);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[TimeManager] Failed to set FMOD parameter: {e.Message}");
        }
    }

    #endregion

    #region Lightning Effect

    // Called by WeatherSystemManager when thunder occurs, intensity is 0..1
    public void CreateLightningEffect(float intensity)
    {
        // Visual lightning for Distant, Mid, and Close. Level 3 is stronger.
        if (weatherSystem != null && weatherSystem.thunderLevel != ThunderLevel.None)
        {
            if (!isLightningActive)
            {
                StartCoroutine(LightningFlashCoroutine(intensity));
            }
        }
    }
    
    private IEnumerator LightningFlashCoroutine(float intensity)
    {
        if (globalLight == null) yield break;
        
        isLightningActive = true;
        
        // Store original values
        originalLightColor = globalLight.color;
        originalLightIntensity = globalLight.intensity;
        
        // Base intensity from provided hint
        float actualIntensity = Mathf.Lerp(lightningMinIntensity, lightningMaxIntensity, Mathf.Clamp01(intensity));

        // Exaggerate if close thunder (level 3)
        bool isClose = weatherSystem != null && weatherSystem.thunderLevel == ThunderLevel.Close;
        if (isClose)
        {
            actualIntensity *= Mathf.Max(1f, closeLightningIntensityMultiplier);
            // Clamp to a reasonable upper bound (up to 1.5x max)
            actualIntensity = Mathf.Min(actualIntensity, lightningMaxIntensity * 1.5f);
        }
        
        // First flash - brightest
        globalLight.color = lightningColor;
        globalLight.intensity = actualIntensity;
        yield return new WaitForSeconds(lightningDuration * 0.4f);
        
        // Dim briefly
        globalLight.intensity = originalLightIntensity;
        yield return new WaitForSeconds(lightningDuration * 0.1f);
        
        // Second flash - less bright
        float secondFlash = actualIntensity * 0.6f;
        globalLight.intensity = secondFlash;
        yield return new WaitForSeconds(lightningDuration * 0.3f);
        
        // Dim briefly
        globalLight.intensity = originalLightIntensity * 0.7f;
        yield return new WaitForSeconds(lightningDuration * 0.05f);
        
        // Third flash - even less bright (slightly stronger if close)
        float thirdFlash = actualIntensity * (isClose ? 0.45f : 0.3f);
        globalLight.intensity = thirdFlash;
        yield return new WaitForSeconds(lightningDuration * 0.2f);
        
        // Optional tiny extra pop for close
        if (isClose)
        {
            globalLight.intensity = actualIntensity * 0.3f;
            yield return new WaitForSeconds(lightningDuration * 0.12f);
        }

        // Restore original light settings
        globalLight.color = originalLightColor;
        globalLight.intensity = originalLightIntensity;
        
        isLightningActive = false;
    }

    #endregion

    private void OnDestroy()
    {
        if (weatherSystem != null)
        {
            weatherSystem.OnThunderTriggered -= CreateLightningEffect;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        skyboxBlendDurationMinutes = Mathf.Max(0.1f, skyboxBlendDurationMinutes);
        skyboxRotationSpeed = Mathf.Max(0f, skyboxRotationSpeed);
        maxSunAltitude = Mathf.Clamp(maxSunAltitude, 0f, 90f);
        
        // Validate light intensities
        dayLightIntensity = Mathf.Max(0.1f, dayLightIntensity);
        nightLightIntensity = Mathf.Max(0.001f, nightLightIntensity);
        moonLightIntensity = Mathf.Max(0.001f, moonLightIntensity);
        rainLightReduction = Mathf.Clamp01(rainLightReduction);

        // Validate new smooth transition settings
        lightTransitionDuration = Mathf.Clamp(lightTransitionDuration, 0.5f, 10f);
        duskToNightTintSpeed = Mathf.Clamp(duskToNightTintSpeed, 0.1f, 5f);

        closeLightningIntensityMultiplier = Mathf.Max(1.0f, closeLightningIntensityMultiplier);

        rainToOvercastExponent = Mathf.Clamp(rainToOvercastExponent, 0.1f, 2f);
        rainToOvercastGain = Mathf.Clamp(rainToOvercastGain, 0.1f, 8f);
        rainOvercastThreshold = Mathf.Clamp01(rainOvercastThreshold);
        minOvercastBlend = Mathf.Clamp01(minOvercastBlend);
        rainColdTintMaxStrength = Mathf.Clamp01(rainColdTintMaxStrength);
        rainColdTintExponent = Mathf.Clamp(rainColdTintExponent, 0.1f, 2f);

        minNightDirectionalIntensity = Mathf.Clamp01(minNightDirectionalIntensity);
        nightAmbientFloor = Mathf.Clamp01(nightAmbientFloor);
        ambientGlobalMultiplier = Mathf.Clamp(ambientGlobalMultiplier, 0f, 2f);
        moonLightMultiplier = Mathf.Clamp(moonLightMultiplier, 0f, 3f);
    }
#endif
}