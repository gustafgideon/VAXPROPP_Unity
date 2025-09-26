using UnityEngine;
using UnityEngine.Rendering;

public class VisualAmbianceManager : MonoBehaviour
{
    [Header("Lighting Settings")]
    [SerializeField] private Light sunLight;
    [SerializeField] private Light moonLight;
    [SerializeField] private float maxSunIntensity = 1.0f;     // Reduced from 1.5f
    [SerializeField] private float maxMoonIntensity = 0.3f;    // Reduced from 0.5f
    
    [Header("Skybox Settings")]
    [SerializeField] private Material daySkybox;
    [SerializeField] private Material nightSkybox;
    [SerializeField] private float skyboxBlendDuration = 0.5f; // How long the blend takes (in parameter space)
    
    [Header("Ambient Settings")]
    [SerializeField] private float dayAmbientIntensity = 0.8f; // Reduced from 1.0f
    [SerializeField] private float nightAmbientIntensity = 0.2f; // Reduced from 0.3f
    
    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;
    
    private TimeOfDayManager timeOfDayManager;
    private bool isInitialized = false;
    private bool hasReceivedFirstUpdate = false;
    private float previousManualParameter = -1f;
    private Material blendedSkybox;
    
    // Cache common property IDs for better performance
    private static readonly int _Tint = Shader.PropertyToID("_Tint");
    private static readonly int _Color = Shader.PropertyToID("_Color");
    private static readonly int _Exposure = Shader.PropertyToID("_Exposure");
    private static readonly int _Rotation = Shader.PropertyToID("_Rotation");
    private static readonly int _SkyTint = Shader.PropertyToID("_SkyTint");
    private static readonly int _AtmosphereThickness = Shader.PropertyToID("_AtmosphereThickness");
    private static readonly int _SunSize = Shader.PropertyToID("_SunSize");
    private static readonly int _SunSizeConvergence = Shader.PropertyToID("_SunSizeConvergence");
    private static readonly int _GroundColor = Shader.PropertyToID("_GroundColor");
    private static readonly int _SkyboxTint = Shader.PropertyToID("_SkyboxTint");
    
    void Start()
    {
        InitializeSystem();
        ConnectToTimeOfDayManager();
        
        // Create blended skybox material
        if (daySkybox != null && nightSkybox != null)
        {
            // Always start with the day skybox shader
            blendedSkybox = new Material(daySkybox);
            blendedSkybox.name = "BlendedSkybox";
            RenderSettings.skybox = blendedSkybox;
            
            if (debugLogging)
            {
                Debug.Log($"✅ Skybox materials initialized. Day: {daySkybox.name}, Night: {nightSkybox.name}");
            }
        }
        
        // Initialize lighting to current time to prevent flash
        StartCoroutine(InitializeLightingToCurrentTime());
    }
    
    void OnDestroy()
    {
        if (timeOfDayManager != null)
        {
            timeOfDayManager.OnParameterValueChanged -= OnParameterValueChanged;
        }
        
        // Clean up the created material to prevent memory leaks
        if (blendedSkybox != null)
        {
            Destroy(blendedSkybox);
        }
    }
    
    private System.Collections.IEnumerator InitializeLightingToCurrentTime()
    {
        // Wait a frame for TimeOfDayManager to be fully initialized
        yield return null;
        
        if (timeOfDayManager != null)
        {
            float currentTime = timeOfDayManager.GetCurrentTime();
            
            // Calculate what the visual parameter should be based on current time
            float initialVisualParameter = currentTime; // Since we use FMOD parameter directly
            
            if (debugLogging)
            {
                Debug.Log($"🌅 Initializing lighting to current time: {currentTime:F3} → Visual: {initialVisualParameter:F3}");
            }
            
            // Set initial lighting without any flash
            ApplyLighting(initialVisualParameter);
        }
    }
    
    private void InitializeSystem()
    {
        if (sunLight == null)
        {
            GameObject sunObj = GameObject.Find("Sun") ?? GameObject.Find("Directional Light");
            if (sunObj != null) sunLight = sunObj.GetComponent<Light>();
        }
        
        if (moonLight == null)
        {
            GameObject moonObj = GameObject.Find("Moon");
            if (moonObj != null) moonLight = moonObj.GetComponent<Light>();
        }
        
        isInitialized = true;
        Debug.Log("✅ VisualTimeOfDayManager initialized");
    }
    
    private void ConnectToTimeOfDayManager()
    {
        timeOfDayManager = FindObjectOfType<TimeOfDayManager>();
        if (timeOfDayManager != null)
        {
            timeOfDayManager.OnParameterValueChanged += OnParameterValueChanged;
            Debug.Log("✅ VisualTimeOfDayManager connected to TimeOfDayManager");
        }
        else
        {
            Debug.LogWarning("❌ TimeOfDayManager not found!");
        }
    }
    
    private void OnParameterValueChanged(float fmodParameterValue)
    {
        if (!isInitialized) return;
        
        // Mark that we've received our first update
        if (!hasReceivedFirstUpdate)
        {
            hasReceivedFirstUpdate = true;
            if (debugLogging)
            {
                Debug.Log($"🎯 First FMOD parameter update received: {fmodParameterValue:F3}");
            }
        }
        
        float visualParameter = fmodParameterValue;
        
        if (debugLogging && Mathf.Abs(previousManualParameter - visualParameter) > 0.01f)
        {
            float currentTime = timeOfDayManager.GetCurrentTime();
            TimeOfDayManager.TimeOfDay phase = timeOfDayManager.GetCurrentTimeOfDay();
            Debug.Log($"🔍 Parameter changed: {visualParameter:F3} | Time: {currentTime:F3} | Phase: {phase}");
            previousManualParameter = visualParameter;
        }
        
        ApplyLighting(visualParameter);
    }
    
    private void ApplyLighting(float visualParameter)
    {
        // Sun: Bright during day (low visual parameter), dim during night (high visual parameter)
        if (sunLight != null)
        {
            // Use SmoothStep for more natural intensity curve
            float sunIntensity = Mathf.SmoothStep(maxSunIntensity, 0f, visualParameter);
            sunLight.intensity = sunIntensity;
            
            // Color: White during day, blue during night
            Color dayColor = Color.white;
            Color nightColor = new Color(0.3f, 0.4f, 0.7f);
            sunLight.color = Color.Lerp(dayColor, nightColor, visualParameter);
        }
        
        // Moon: Dim during day, bright during night
        if (moonLight != null)
        {
            // Use SmoothStep for more natural intensity curve
            float moonIntensity = Mathf.SmoothStep(0f, maxMoonIntensity, visualParameter);
            moonLight.intensity = moonIntensity;
            moonLight.color = new Color(0.7f, 0.7f, 1f);
        }
        
        // Ambient lighting - adjusted brightness values
        Color dayAmbient = new Color(0.7f, 0.7f, 0.8f);
        Color nightAmbient = new Color(0.2f, 0.2f, 0.4f);
        RenderSettings.ambientLight = Color.Lerp(dayAmbient, nightAmbient, visualParameter);
        
        // Reduced ambient intensity overall
        float ambientIntensity = Mathf.Lerp(dayAmbientIntensity, nightAmbientIntensity, visualParameter);
        RenderSettings.ambientIntensity = ambientIntensity;
        
        // Skybox transition with improved blending
        UpdateSkybox(visualParameter);
        
        if (debugLogging && Time.frameCount % 120 == 0) // Reduce log spam
        {
            Debug.Log($"🌍 Visual: {visualParameter:F3} → Sun: {sunLight?.intensity:F2} | Moon: {moonLight?.intensity:F2} | Ambient: {ambientIntensity:F2}");
        }
    }
    
    private void UpdateSkybox(float visualParameter)
    {
        if (daySkybox == null || nightSkybox == null || blendedSkybox == null)
        {
            if (debugLogging && Time.frameCount % 600 == 0) // Reduced spam for this warning
            {
                Debug.LogWarning("❌ Day or Night skybox material is missing!");
            }
            return;
        }
        
        // Get the base materials for blending
        Material baseMaterial = (visualParameter < 0.5f) ? daySkybox : nightSkybox;
        Material targetMaterial = (visualParameter < 0.5f) ? nightSkybox : daySkybox;
        
        // Make sure blendedSkybox has the right shader
        if (blendedSkybox.shader != baseMaterial.shader)
        {
            blendedSkybox.shader = baseMaterial.shader;
            blendedSkybox.CopyPropertiesFromMaterial(baseMaterial);
        }
        
        // Calculate a smoother blend factor
        // This creates a more focused transition in the middle range
        float blendFactor;
        if (visualParameter < 0.5f)
        {
            // Day to night transition (0 to 0.5 maps to 0 to 1)
            blendFactor = Mathf.SmoothStep(0, 1, visualParameter * 2f);
        }
        else
        {
            // Night to day transition (0.5 to 1 maps to 1 to 0)
            blendFactor = Mathf.SmoothStep(1, 0, (visualParameter - 0.5f) * 2f);
        }
        
        // Blend all skybox properties
        BlendSkyboxProperties(baseMaterial, targetMaterial, blendFactor);
        
        // Apply a small reduction to the exposure for night skybox to ensure it's not too bright
        if (visualParameter > 0.5f && blendedSkybox.HasProperty(_Exposure))
        {
            float currentExposure = blendedSkybox.GetFloat(_Exposure);
            float targetExposure = currentExposure * 0.8f; // Reduce exposure by 20% for night
            blendedSkybox.SetFloat(_Exposure, Mathf.Lerp(currentExposure, targetExposure, (visualParameter - 0.5f) * 2f));
        }
        
        // Ensure our blended skybox is set as the active skybox
        RenderSettings.skybox = blendedSkybox;
        
        if (debugLogging && Time.frameCount % 120 == 0)
        {
            Debug.Log($"🌌 Skybox blend: {blendFactor:F2} from {(visualParameter < 0.5f ? "Day→Night" : "Night→Day")}");
        }
    }
    
    private void BlendSkyboxProperties(Material baseMaterial, Material targetMaterial, float blendFactor)
    {
        // Blend color properties
        BlendColorProperties(baseMaterial, targetMaterial, blendFactor);
        
        // Blend float properties
        BlendFloatProperties(baseMaterial, targetMaterial, blendFactor);
        
        // Copy textures from base material - textures don't blend
        CopyTexturesFrom(baseMaterial);
    }
    
    private void BlendColorProperties(Material baseMaterial, Material targetMaterial, float blendFactor)
    {
        TryBlendProperty<Color>(_Tint, baseMaterial, targetMaterial, blendFactor, Color.Lerp);
        TryBlendProperty<Color>(_Color, baseMaterial, targetMaterial, blendFactor, Color.Lerp);
        TryBlendProperty<Color>(_SkyTint, baseMaterial, targetMaterial, blendFactor, Color.Lerp);
        TryBlendProperty<Color>(_GroundColor, baseMaterial, targetMaterial, blendFactor, Color.Lerp);
        TryBlendProperty<Color>(_SkyboxTint, baseMaterial, targetMaterial, blendFactor, Color.Lerp);
    }
    
    private void BlendFloatProperties(Material baseMaterial, Material targetMaterial, float blendFactor)
    {
        TryBlendProperty<float>(_Exposure, baseMaterial, targetMaterial, blendFactor, Mathf.Lerp);
        TryBlendProperty<float>(_Rotation, baseMaterial, targetMaterial, blendFactor, Mathf.Lerp);
        TryBlendProperty<float>(_AtmosphereThickness, baseMaterial, targetMaterial, blendFactor, Mathf.Lerp);
        TryBlendProperty<float>(_SunSize, baseMaterial, targetMaterial, blendFactor, Mathf.Lerp);
        TryBlendProperty<float>(_SunSizeConvergence, baseMaterial, targetMaterial, blendFactor, Mathf.Lerp);
    }
    
    private void CopyTexturesFrom(Material sourceMaterial)
    {
        // Common skybox texture properties
        string[] textureProps = new string[] {
            "_MainTex", "_Tex", "_FrontTex", "_BackTex", "_LeftTex", "_RightTex", "_UpTex", "_DownTex"
        };
        
        foreach (string prop in textureProps)
        {
            int propId = Shader.PropertyToID(prop);
            if (sourceMaterial.HasProperty(propId) && blendedSkybox.HasProperty(propId))
            {
                try
                {
                    Texture tex = sourceMaterial.GetTexture(propId);
                    if (tex != null)
                    {
                        blendedSkybox.SetTexture(propId, tex);
                    }
                }
                catch { /* Ignore errors */ }
            }
        }
    }
    
    // Generic property blending using delegates
    private delegate T LerpFunction<T>(T a, T b, float t);
    
    private void TryBlendProperty<T>(int propertyID, Material baseMaterial, Material targetMaterial, float blendFactor, LerpFunction<T> lerpFunc)
    {
        if (!baseMaterial.HasProperty(propertyID) || !targetMaterial.HasProperty(propertyID))
            return;
            
        try
        {
            if (typeof(T) == typeof(Color))
            {
                Color baseValue = baseMaterial.GetColor(propertyID);
                Color targetValue = targetMaterial.GetColor(propertyID);
                blendedSkybox.SetColor(propertyID, (Color)(object)lerpFunc((T)(object)baseValue, (T)(object)targetValue, blendFactor));
            }
            else if (typeof(T) == typeof(float))
            {
                float baseValue = baseMaterial.GetFloat(propertyID);
                float targetValue = targetMaterial.GetFloat(propertyID);
                blendedSkybox.SetFloat(propertyID, (float)(object)lerpFunc((T)(object)baseValue, (T)(object)targetValue, blendFactor));
            }
        }
        catch { /* Ignore errors if property type doesn't match */ }
    }
    
    [ContextMenu("Force Initialize Lighting")]
    public void ForceInitializeLighting()
    {
        if (timeOfDayManager != null)
        {
            float currentTime = timeOfDayManager.GetCurrentTime();
            ApplyLighting(currentTime);
            Debug.Log($"🔧 Force initialized lighting to time: {currentTime:F3}");
        }
    }
    
    [ContextMenu("Check Skybox Properties")]
    public void CheckSkyboxProperties()
    {
        if (daySkybox == null || nightSkybox == null)
        {
            Debug.LogWarning("Cannot check skybox properties - day or night skybox is missing!");
            return;
        }
        
        Debug.Log("==== SKYBOX PROPERTIES ====");
        Debug.Log($"Day Skybox: {daySkybox.name} (Shader: {daySkybox.shader.name})");
        Debug.Log($"Night Skybox: {nightSkybox.name} (Shader: {nightSkybox.shader.name})");
        
        // List of property names to check
        string[] propertiesToCheck = {
            "_Tint", "_Color", "_Exposure", "_Rotation", "_SkyTint", "_AtmosphereThickness",
            "_SunSize", "_SunSizeConvergence", "_GroundColor", "_SkyboxTint",
            "_MainTex", "_Tex", "_FrontTex", "_BackTex", "_LeftTex", "_RightTex", "_UpTex", "_DownTex"
        };
        
        Debug.Log("==== PROPERTY DETAILS ====");
        foreach (string prop in propertiesToCheck)
        {
            bool dayHas = daySkybox.HasProperty(prop);
            bool nightHas = nightSkybox.HasProperty(prop);
            
            string type = "Unknown";
            if (dayHas)
            {
                try { daySkybox.GetColor(prop); type = "Color"; } 
                catch {
                    try { daySkybox.GetFloat(prop); type = "Float"; }
                    catch {
                        try { daySkybox.GetTexture(prop); type = "Texture"; }
                        catch { type = "Other"; }
                    }
                }
            }
            
            Debug.Log($"Property '{prop}': Day={dayHas}, Night={nightHas}, Type={type}");
        }
    }
}