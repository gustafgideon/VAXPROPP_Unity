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

    [Header("FMOD Settings")] [SerializeField]
    private string timeOfDayParameterName = "TimeOfDay";

    [Header("Weather System")] 
    [SerializeField] private WeatherSystemManager weatherSystem;

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

        // Check if the shader is the correct one
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
        UpdateLightingContinuous();

        SetTimeOfDayParameter(currentState);

        if (debugDayStateChanges)
            Debug.Log($"[TimeManager] Initial state: {currentState} at {hours:00}:{minutes:00}");
            
        // Subscribe to the thunder event if we have a reference to the weather system
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

        // Update weather blend in shader
        if (weatherSystem != null && skyboxMaterial != null)
        {
            skyboxMaterial.SetFloat("_Blend", weatherSystem.rainIntensity);
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
                float rain = weatherSystem ? weatherSystem.rainIntensity : 0f;
                Debug.Log(
                    $"[TimeManager] Day state {previousState} -> {currentState} at {hours:00}:{minutes:00} (rain={rain:0.00})");
            }

            StartSkyboxBlend(previousState, currentState);
            SetTimeOfDayParameter(newState);
        }
    }

    #region Skybox Blending

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
        
        // Set rain blend
        float rain = weatherSystem ? weatherSystem.rainIntensity : 0f;
        skyboxMaterial.SetFloat("_Blend", rain);
        
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

    #endregion

    #region Lighting

    private void UpdateSunRotation()
    {
        // dayProgress 0..1
        float dayProgress = GetDayProgress();

        // Create more dramatic day cycle with adjusted curve
        // This creates a more natural arc motion
        float sunCurve = Mathf.Sin((dayProgress - 0.25f) * Mathf.PI * 2f);
        sunHeight01 = Mathf.Clamp01(sunCurve * 0.5f + 0.5f);
        
        // Use slightly different altitude curve for more realism
        // This makes the sun rise/set more quickly and stay higher during day
        float altitudeAngle = Mathf.Pow(sunCurve, 0.9f) * maxSunAltitude;
        
        // More realistic azimuth with minor offset
        // Ensures the sun doesn't rise/set exactly east/west
        float sunAngle = (dayProgress * 360f) + 15f; // slight offset for realism
        
        // Update the light's rotation
        globalLight.transform.rotation = Quaternion.Euler(altitudeAngle, sunAngle - 90f, 0f);
    }

    private void UpdateLightingContinuous()
    {
        if (!globalLight) return;
        
        // Skip lighting updates if lightning is active
        if (isLightningActive) return;

        float rain = weatherSystem ? weatherSystem.rainIntensity : 0f;

        // Get segment progress for gradient evaluation
        float segmentProgress = GetSegmentProgress();

        // Get color from gradient based on time of day
        Color normalColor = GetNormalGradientForState(currentState).Evaluate(segmentProgress);
        Color overcastColor = GetOvercastGradientForState(currentState).Evaluate(segmentProgress);

        // Blend between normal and overcast based on rain intensity
        float overcastBlend = Mathf.Clamp01(rain * 2f); // Full overcast at rain >= 0.5
        Color targetColor = Color.Lerp(normalColor, overcastColor, overcastBlend);

        // Apply color to directional light
        globalLight.color = targetColor;

        // Much more dramatic light intensity change
        float baseIntensity;
        if (sunHeight01 > 0.15f) {
            // Day/dusk intensity
            baseIntensity = Mathf.Lerp(0.3f, dayLightIntensity, Mathf.Pow(sunHeight01, 0.7f));
        } else {
            // Night intensity (much darker)
            baseIntensity = Mathf.Lerp(nightLightIntensity, 0.3f, sunHeight01 / 0.15f);
        }

        // Reduce intensity in rain using configurable reduction factor
        float rainDarkening = Mathf.Lerp(1f, 1f - rainLightReduction, rain);
        globalLight.intensity = baseIntensity * rainDarkening;
        
        // Update shadow properties based on time of day AND weather
        UpdateShadowSettings(rain);

        // Handle moon light
        UpdateMoonLight();

        // Update ambient lighting
        UpdateAmbientLighting();
        
        // Update fog settings
        UpdateFogSettings();
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
            
            // Very dim blue-tinted light
            float actualMoonIntensity = moonLightIntensity * (1 - sunHeight01 * 5);
            
            // Reduce moon intensity in rain too
            float rain = weatherSystem ? weatherSystem.rainIntensity : 0f;
            float rainDarkening = Mathf.Lerp(1f, 1f - (rainLightReduction * 0.7f), rain);  // slightly less reduction than sun
            
            moonLight.intensity = actualMoonIntensity * rainDarkening;
            moonLight.color = new Color(0.6f, 0.65f, 1f);
            
            // Make sure moon has shadows enabled but less detailed
            moonLight.shadows = LightShadows.Soft;
            moonLight.shadowStrength = 0.6f;
        } else if (moonLight != null) {
            moonLight.gameObject.SetActive(false);
        }
    }

    private void UpdateAmbientLighting()
    {
        float rain = weatherSystem ? weatherSystem.rainIntensity : 0f;
        
        // Dynamically adjust ambient light intensity based on time of day
        float ambientIntensity = Mathf.Lerp(0.1f, 1.0f, Mathf.Pow(sunHeight01, 0.5f));
        
        // Reduce ambient intensity further in rain
        ambientIntensity *= Mathf.Lerp(1f, 0.7f, rain);
        
        // Blend between day and night ambient settings
        RenderSettings.ambientSkyColor = Color.Lerp(nightSkyColor, daySkyColor, ambientIntensity) * ambientIntensity;
        RenderSettings.ambientEquatorColor = Color.Lerp(nightEquatorColor, dayEquatorColor, ambientIntensity) * ambientIntensity;
        RenderSettings.ambientGroundColor = Color.Lerp(nightGroundColor, dayGroundColor, ambientIntensity) * ambientIntensity;
        
        // Also adjust ambient intensity directly
        RenderSettings.ambientIntensity = ambientIntensity * 0.8f;
    }

    private void UpdateFogSettings()
    {
        if (RenderSettings.fog) {
            // Get rain intensity
            float rain = weatherSystem ? weatherSystem.rainIntensity : 0f;
            
            // Update fog color (this should already be in your code)
            float segmentProgress = GetSegmentProgress();
            Color normalColor = GetNormalGradientForState(currentState).Evaluate(segmentProgress);
            Color overcastColor = GetOvercastGradientForState(currentState).Evaluate(segmentProgress);
            float overcastBlend = Mathf.Clamp01(rain * 2f);
            Color targetColor = Color.Lerp(normalColor, overcastColor, overcastBlend);
            
            // Apply fog color
            RenderSettings.fogColor = targetColor * 0.7f; // Slightly darker than sky
            
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

    #endregion

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

    // New method to create lightning effect
    public void CreateLightningEffect(float intensity)
    {
        // Only at night or dusk/dawn for realism
        if (currentState == DayState.Night || currentState == DayState.Dusk || currentState == DayState.Dawn)
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
        
        // Calculate flash intensity based on provided intensity parameter
        float actualIntensity = Mathf.Lerp(lightningMinIntensity, lightningMaxIntensity, intensity);
        
        // First flash - brightest
        globalLight.color = lightningColor;
        globalLight.intensity = actualIntensity;
        
        // Short duration for first flash
        float firstFlashDuration = lightningDuration * 0.4f;
        yield return new WaitForSeconds(firstFlashDuration);
        
        // Dim briefly
        globalLight.intensity = originalLightIntensity;
        yield return new WaitForSeconds(lightningDuration * 0.1f);
        
        // Second flash - less bright
        globalLight.intensity = actualIntensity * 0.6f;
        yield return new WaitForSeconds(lightningDuration * 0.3f);
        
        // Dim briefly
        globalLight.intensity = originalLightIntensity * 0.7f;
        yield return new WaitForSeconds(lightningDuration * 0.05f);
        
        // Third flash - even less bright
        globalLight.intensity = actualIntensity * 0.3f;
        yield return new WaitForSeconds(lightningDuration * 0.2f);
        
        // Restore original light settings
        globalLight.color = originalLightColor;
        globalLight.intensity = originalLightIntensity;
        
        isLightningActive = false;
    }

    #endregion

    private void OnDestroy()
    {
        // Unsubscribe from events when this object is destroyed
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
        
        // Validate shadow settings
        dayShadowStrength = Mathf.Clamp01(dayShadowStrength);
        nightShadowStrength = Mathf.Clamp01(nightShadowStrength);
        overcastDayShadowStrength = Mathf.Clamp01(overcastDayShadowStrength);
        overcastNightShadowStrength = Mathf.Clamp01(overcastNightShadowStrength);
        
        // Validate fog densities
        dayFogDensity = Mathf.Max(0.0001f, dayFogDensity);
        nightFogDensity = Mathf.Max(dayFogDensity, nightFogDensity);
        rainFogDensityAdditive = Mathf.Max(0f, rainFogDensityAdditive);
        
        // Validate lightning settings
        lightningMaxIntensity = Mathf.Max(1f, lightningMaxIntensity);
        lightningMinIntensity = Mathf.Max(0.5f, lightningMinIntensity);
        lightningMinIntensity = Mathf.Min(lightningMinIntensity, lightningMaxIntensity - 0.5f);
        lightningDuration = Mathf.Max(0.05f, lightningDuration);
    }
#endif
}