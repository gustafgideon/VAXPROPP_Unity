using UnityEngine;
using UnityEngine.Rendering;

public class VisualAmbianceManager : MonoBehaviour
{
    [Header("Manual Visual Control")]
    [SerializeField] private bool useManualControl = false;
    [SerializeField, Range(0f, 1f)] private float manualVisualParameter = 0f;
    
    [Header("Lighting Settings")]
    [SerializeField] private Light sunLight;
    [SerializeField] private Light moonLight;
    [SerializeField] private float maxSunIntensity = 1.5f;
    [SerializeField] private float maxMoonIntensity = 0.5f;
    
    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;
    
    private TimeOfDayManager timeOfDayManager;
    private bool isInitialized = false;
    private bool hasReceivedFirstUpdate = false;
    private float previousManualParameter = -1f;
    
    void Start()
    {
        InitializeSystem();
        ConnectToTimeOfDayManager();
        
        // Initialize lighting to current time to prevent flash
        StartCoroutine(InitializeLightingToCurrentTime());
    }
    
    void Update()
    {
        if (useManualControl && manualVisualParameter != previousManualParameter)
        {
            ApplyLighting(manualVisualParameter);
            previousManualParameter = manualVisualParameter;
            
            if (debugLogging)
            {
                Debug.Log($"🎛️ Manual visual parameter: {manualVisualParameter:F2}");
            }
        }
    }
    
    void OnDestroy()
    {
        if (timeOfDayManager != null)
        {
            timeOfDayManager.OnParameterValueChanged -= OnParameterValueChanged;
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
        if (useManualControl) return;
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
        
        if (debugLogging)
        {
            float currentTime = timeOfDayManager.GetCurrentTime();
            TimeOfDayManager.TimeOfDay phase = timeOfDayManager.GetCurrentTimeOfDay();
            Debug.Log($"🔍 FMOD: {fmodParameterValue:F3} | Time: {currentTime:F3} | Phase: {phase} | Visual: {visualParameter:F3}");
        }
        
        ApplyLighting(visualParameter);
    }
    
    private void ApplyLighting(float visualParameter)
    {
        // Sun: Bright during day (low visual parameter), dim during night (high visual parameter)
        if (sunLight != null)
        {
            float sunIntensity = (1f - visualParameter) * maxSunIntensity;
            sunLight.intensity = sunIntensity;
            
            // Color: White during day, blue during night
            Color dayColor = Color.white;
            Color nightColor = new Color(0.3f, 0.4f, 0.7f);
            sunLight.color = Color.Lerp(dayColor, nightColor, visualParameter);
        }
        
        // Moon: Dim during day, bright during night
        if (moonLight != null)
        {
            float moonIntensity = visualParameter * maxMoonIntensity;
            moonLight.intensity = moonIntensity;
            moonLight.color = new Color(0.7f, 0.7f, 1f);
        }
        
        // Ambient lighting
        Color dayAmbient = new Color(0.7f, 0.7f, 0.8f);
        Color nightAmbient = new Color(0.2f, 0.2f, 0.4f);
        RenderSettings.ambientLight = Color.Lerp(dayAmbient, nightAmbient, visualParameter);
        
        float ambientIntensity = Mathf.Lerp(1.5f, 0.3f, visualParameter);
        RenderSettings.ambientIntensity = ambientIntensity;
        
        if (debugLogging && Time.frameCount % 120 == 0) // Reduce log spam
        {
            Debug.Log($"🌍 Visual: {visualParameter:F3} → Sun: {sunLight?.intensity:F2} | Moon: {moonLight?.intensity:F2} | Ambient: {ambientIntensity:F2}");
        }
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
    
    [ContextMenu("Enable Manual Control")]
    public void EnableManualControl()
    {
        useManualControl = true;
        Debug.Log("🎛️ Manual visual control ENABLED");
    }
    
    [ContextMenu("Disable Manual Control")]
    public void DisableManualControl()
    {
        useManualControl = false;
        Debug.Log("🎛️ Manual visual control DISABLED");
    }
}