using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class RainController : MonoBehaviour
{
    [Header("Rain Intensity (0 = Off, 1 = Heavy Rain)")]
    [Range(0f, 1f)]
    public float rainIntensity = 0.5f;
    
    [Header("Particle System")]
    public ParticleSystem rainParticles;
    
    [Header("FMOD Rain Audio")]
    public EventReference rainEvent;
    public string globalParameterName = "RainIntensity";
    [Range(0f, 1f)]
    public float masterVolume = 1f;
    
    [Header("Intensity Settings")]
    public float minRainRate = 100f;
    public float maxRainRate = 800f;
    
    [Header("Particle Size Settings")]
    [Range(0.1f, 2f)]
    public float particleSize = 1f;
    [Range(0f, 0.5f)]
    public float sizeVariation = 0.2f;
    
    [Header("Current Settings (Read Only)")]
    [SerializeField] private Vector3 currentZoneSize;
    [SerializeField] private string status = "Configure zone in ParticleSystem Shape module";
    [SerializeField] private string audioStatus = "FMOD Event not loaded";
    
    private float lastIntensity = -1f;
    private float lastParticleSize = -1f;
    private float lastSizeVariation = -1f;
    private float lastMasterVolume = -1f;
    
    // FMOD Event Instance
    private EventInstance rainEventInstance;
    private bool isEventValid = false;
    
    void Start()
    {
        if (rainParticles == null)
            rainParticles = GetComponentInChildren<ParticleSystem>();
        
        if (rainParticles == null)
        {
            Debug.LogError("No ParticleSystem found!");
            return;
        }
        
        // Setup FMOD audio
        SetupRainAudio();
        
        // Reset any weird scaling we might have done
        rainParticles.transform.localScale = Vector3.one;
        
        // Read current settings
        ReadCurrentSettings();
        
        // Only control what we can safely control
        UpdateParticleSize();
        UpdateRain();
    }
    
    void SetupRainAudio()
    {
        if (rainEvent.IsNull)
        {
            audioStatus = "No FMOD event selected";
            Debug.LogWarning("Rain FMOD event is not assigned!");
            return;
        }
        
        try
        {
            // Create FMOD event instance using EventReference
            rainEventInstance = RuntimeManager.CreateInstance(rainEvent);
            
            if (rainEventInstance.isValid())
            {
                isEventValid = true;
                audioStatus = "FMOD Event loaded: " + rainEvent.Path;
                Debug.Log("FMOD Rain event loaded successfully: " + rainEvent.Path);
                
                // Set initial volume
                rainEventInstance.setVolume(masterVolume);
                
                // Start the event
                rainEventInstance.start();
                
                // Set initial global parameter
                UpdateRainAudio();
            }
            else
            {
                isEventValid = false;
                audioStatus = "Failed to load FMOD event: " + rainEvent.Path;
                Debug.LogError("Failed to create FMOD event instance: " + rainEvent.Path);
            }
        }
        catch (System.Exception e)
        {
            isEventValid = false;
            audioStatus = "FMOD Error: " + e.Message;
            Debug.LogError("FMOD Error setting up rain audio: " + e.Message);
        }
    }
    
    void ReadCurrentSettings()
    {
        var shape = rainParticles.shape;
        currentZoneSize = shape.scale;
        
        // Make sure basic settings are correct
        var main = rainParticles.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 5000;
        
        var emission = rainParticles.emission;
        emission.enabled = true;
        
        status = "Zone: " + currentZoneSize.x + "x" + currentZoneSize.z + " (Set manually in ParticleSystem)";
        
        Debug.Log("Rain system ready. Zone size: " + currentZoneSize);
    }
    
    void Update()
    {
        // Only update what we can safely change
        if (rainIntensity != lastIntensity)
        {
            UpdateRain();
            UpdateRainAudio();
            lastIntensity = rainIntensity;
        }
        
        if (masterVolume != lastMasterVolume)
        {
            UpdateRainVolume();
            lastMasterVolume = masterVolume;
        }
        
        if (particleSize != lastParticleSize || sizeVariation != lastSizeVariation)
        {
            UpdateParticleSize();
            lastParticleSize = particleSize;
            lastSizeVariation = sizeVariation;
        }
        
        // Check if someone changed the ParticleSystem manually
        var shape = rainParticles.shape;
        if (shape.scale != currentZoneSize)
        {
            currentZoneSize = shape.scale;
            status = "Zone: " + currentZoneSize.x + "x" + currentZoneSize.z + " (Set manually in ParticleSystem)";
        }
    }
    
    void UpdateRain()
    {
        if (rainParticles == null) return;
        
        var emission = rainParticles.emission;
        
        if (rainIntensity > 0f)
        {
            float targetRate = Mathf.Lerp(minRainRate, maxRainRate, rainIntensity);
            emission.rateOverTime = targetRate;
            
            if (!rainParticles.isPlaying)
            {
                rainParticles.Play();
            }
        }
        else
        {
            emission.rateOverTime = 0f;
            if (rainParticles.isPlaying)
            {
                rainParticles.Stop();
            }
        }
    }
    
    void UpdateRainAudio()
    {
        if (!isEventValid) return;
        
        try
        {
            // Set the GLOBAL parameter in FMOD (not local to the event instance)
            FMOD.Studio.System studioSystem = RuntimeManager.StudioSystem;
            FMOD.RESULT result = studioSystem.setParameterByName(globalParameterName, rainIntensity);
            
            if (result == FMOD.RESULT.OK)
            {
                audioStatus = "Global Parameter '" + globalParameterName + "': " + rainIntensity.ToString("F2");
                Debug.Log("FMOD Global parameter '" + globalParameterName + "' set to: " + rainIntensity);
            }
            else
            {
                audioStatus = "Failed to set global parameter: " + result.ToString();
                Debug.LogError("Failed to set FMOD global parameter '" + globalParameterName + "': " + result.ToString());
            }
        }
        catch (System.Exception e)
        {
            audioStatus = "FMOD Update Error: " + e.Message;
            Debug.LogError("Error updating FMOD global parameter: " + e.Message);
        }
    }
    
    void UpdateRainVolume()
    {
        if (!isEventValid) return;
        
        try
        {
            rainEventInstance.setVolume(masterVolume);
            Debug.Log("FMOD Rain volume set to: " + masterVolume);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error updating FMOD rain volume: " + e.Message);
        }
    }
    
    void UpdateParticleSize()
    {
        if (rainParticles == null) return;
        
        var main = rainParticles.main;
        
        if (sizeVariation > 0f)
        {
            float minSize = Mathf.Max(particleSize - sizeVariation, 0.1f);
            float maxSize = particleSize + sizeVariation;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        }
        else
        {
            main.startSize = particleSize;
        }
    }
    
    // Public methods for external control
    public void SetRainIntensity(float intensity)
    {
        rainIntensity = Mathf.Clamp01(intensity);
        UpdateRain();
        UpdateRainAudio();
    }
    
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        UpdateRainVolume();
    }
    
    // Method to get current global parameter value from FMOD (for debugging)
    public float GetCurrentGlobalParameter()
    {
        try
        {
            FMOD.Studio.System studioSystem = RuntimeManager.StudioSystem;
            float currentValue;
            FMOD.RESULT result = studioSystem.getParameterByName(globalParameterName, out currentValue);
            
            if (result == FMOD.RESULT.OK)
            {
                Debug.Log("Current FMOD global parameter '" + globalParameterName + "' value: " + currentValue);
                return currentValue;
            }
            else
            {
                Debug.LogError("Failed to get FMOD global parameter: " + result.ToString());
                return -1f;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error getting FMOD global parameter: " + e.Message);
            return -1f;
        }
    }
    
    [ContextMenu("Test Light Rain")]
    void TestLightRain()
    {
        SetRainIntensity(0.3f);
    }
    
    [ContextMenu("Test Heavy Rain")]
    void TestHeavyRain()
    {
        SetRainIntensity(0.9f);
    }
    
    [ContextMenu("Stop Rain")]
    void StopRain()
    {
        SetRainIntensity(0f);
    }
    
    [ContextMenu("Check Global Parameter Value")]
    void CheckGlobalParameterValue()
    {
        float value = GetCurrentGlobalParameter();
        Debug.Log("Global parameter check result: " + value);
    }
    
    [ContextMenu("Restart FMOD Event")]
    void RestartFMODEvent()
    {
        StopRainAudio();
        SetupRainAudio();
        UpdateRainAudio();
    }
    
    void StopRainAudio()
    {
        if (isEventValid)
        {
            try
            {
                rainEventInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                rainEventInstance.release();
                isEventValid = false;
                audioStatus = "FMOD Event stopped";
                Debug.Log("FMOD Rain event stopped");
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error stopping FMOD rain event: " + e.Message);
            }
        }
    }
    
    void OnDestroy()
    {
        // Clean up FMOD event when object is destroyed
        StopRainAudio();
    }
    
    // Validation to check if event is assigned
    void OnValidate()
    {
        if (rainEvent.IsNull)
        {
            audioStatus = "No FMOD event selected - click dropdown to choose";
        }
        else
        {
            audioStatus = "Event selected: " + rainEvent.Path + " | Global param: " + globalParameterName;
        }
    }
}