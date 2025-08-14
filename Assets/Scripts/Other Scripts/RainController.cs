using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using System.Collections;

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
    
    [Header("Player Detection")]
    public Transform player;  // Direct reference to player transform
    public float checkInterval = 0.5f;  // How often to check player position (seconds)
    public bool enableDebugVisuals = true;  // Show debug gizmos
    
    [Header("Rain Zone Adjustment")]
    [Tooltip("Use manual rain zone instead of particle system shape")]
    public bool useManualRainZone = true;
    [Tooltip("Position of the rain zone center in world space")]
    public Vector3 manualRainZoneCenter;
    [Tooltip("Size of the rain zone in world space")]
    public Vector3 manualRainZoneSize = new Vector3(10, 10, 10);
    
    [Header("Audio Transition Settings")]
    [Tooltip("Time to fade in rain sound when entering zone (seconds)")]
    public float fadeInTime = 1.0f;
    [Tooltip("Time to fade out rain sound when exiting zone (seconds)")]
    public float fadeOutTime = 2.0f;
    
    [Header("Current Settings (Read Only)")]
    [SerializeField] private Vector3 currentZoneSize;
    [SerializeField] private string status = "Configure zone in ParticleSystem Shape module";
    [SerializeField] private string audioStatus = "FMOD Event not loaded";
    [SerializeField] private bool playerInZone = false;
    
    private float lastIntensity = -1f;
    private float lastParticleSize = -1f;
    private float lastSizeVariation = -1f;
    private float lastMasterVolume = -1f;
    private float nextCheckTime = 0f;
    private Coroutine fadeCoroutine = null;
    private float currentAudioIntensity = 0f;
    
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
        
        // Initialize manual rain zone if not set
        if (useManualRainZone && manualRainZoneCenter == Vector3.zero)
        {
            manualRainZoneCenter = transform.position;
        }
        
        // Find player if not assigned
        if (player == null)
        {
            // Try to find player by common names
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            
            if (player == null)
            {
                // Try to find by common player object names
                GameObject playerObj = GameObject.Find("Player");
                if (playerObj == null) playerObj = GameObject.Find("Character");
                if (playerObj == null) playerObj = GameObject.Find("FPSController");
                if (playerObj == null) playerObj = GameObject.Find("FirstPersonController");
                
                if (playerObj != null)
                {
                    player = playerObj.transform;
                }
            }
            
            if (player == null)
            {
                Debug.LogWarning("Player reference not found! Please assign it manually in the inspector.");
            }
            else
            {
                Debug.Log("Player found automatically: " + player.name);
            }
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
        
        // Start with player not in zone
        playerInZone = false;
        
        // Do an initial position check
        CheckPlayerPosition();
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
                
                // Start the event but set parameter to 0 (silent)
                rainEventInstance.start();
                
                // Set initial global parameter to 0 (silent)
                FMOD.Studio.System studioSystem = RuntimeManager.StudioSystem;
                studioSystem.setParameterByName(globalParameterName, 0f);
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
        
        if (useManualRainZone)
        {
            status = "Using manual rain zone: " + manualRainZoneSize.x + "x" + manualRainZoneSize.z;
        }
        else
        {
            status = "Zone: " + currentZoneSize.x + "x" + currentZoneSize.z + " (Set manually in ParticleSystem)";
        }
        
        Debug.Log("Rain system ready. " + (useManualRainZone ? 
            "Manual zone size: " + manualRainZoneSize : 
            "Particle zone size: " + currentZoneSize));
    }
    
    void Update()
    {
        // Check player position at regular intervals
        if (Time.time >= nextCheckTime)
        {
            CheckPlayerPosition();
            nextCheckTime = Time.time + checkInterval;
        }
        
        // Only update what we can safely change
        if (rainIntensity != lastIntensity)
        {
            UpdateRain();
            
            // Only update audio immediately if player is in zone and no fade is in progress
            if (playerInZone && fadeCoroutine == null)
            {
                FadeAudioParameter(rainIntensity, fadeInTime * 0.5f); // Use shorter time for intensity changes
            }
            
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
        if (!useManualRainZone)
        {
            var shape = rainParticles.shape;
            if (shape.scale != currentZoneSize)
            {
                currentZoneSize = shape.scale;
                status = "Zone: " + currentZoneSize.x + "x" + currentZoneSize.z + " (Set manually in ParticleSystem)";
            }
        }
    }
    
    void CheckPlayerPosition()
    {
        if (player == null) return;
        
        Vector3 zoneCenter;
        Vector3 zoneSize;
        
        // Get the rain zone boundaries
        if (useManualRainZone)
        {
            zoneCenter = manualRainZoneCenter;
            zoneSize = manualRainZoneSize;
        }
        else
        {
            zoneCenter = transform.position;
            zoneSize = currentZoneSize;
        }
        
        Vector3 zoneHalfSize = zoneSize * 0.5f;
        
        // Get player position
        Vector3 playerPos = player.position;
        
        // Check if player is inside the rain zone bounds
        bool insideX = playerPos.x >= (zoneCenter.x - zoneHalfSize.x) && playerPos.x <= (zoneCenter.x + zoneHalfSize.x);
        bool insideZ = playerPos.z >= (zoneCenter.z - zoneHalfSize.z) && playerPos.z <= (zoneCenter.z + zoneHalfSize.z);
        bool insideY = playerPos.y >= (zoneCenter.y - zoneHalfSize.y) && playerPos.y <= (zoneCenter.y + zoneHalfSize.y);
        
        bool isInZone = insideX && insideZ && insideY;
        
        // Log detailed position data for debugging
        Debug.Log("Player position check: " +
                  "\nPlayer: " + playerPos +
                  "\nZone Center: " + zoneCenter +
                  "\nZone Size: " + zoneSize +
                  "\nX Inside: " + insideX + " (" + (zoneCenter.x - zoneHalfSize.x) + " to " + (zoneCenter.x + zoneHalfSize.x) + ")" +
                  "\nY Inside: " + insideY + " (" + (zoneCenter.y - zoneHalfSize.y) + " to " + (zoneCenter.y + zoneHalfSize.y) + ")" +
                  "\nZ Inside: " + insideZ + " (" + (zoneCenter.z - zoneHalfSize.z) + " to " + (zoneCenter.z + zoneHalfSize.z) + ")" +
                  "\nResult: " + (isInZone ? "INSIDE ZONE" : "OUTSIDE ZONE"));
        
        // If player zone status changed
        if (isInZone != playerInZone)
        {
            playerInZone = isInZone;
            
            if (playerInZone)
            {
                Debug.Log("PLAYER ENTERED RAIN ZONE - Fading In");
                // Fade in the rain sound
                FadeAudioParameter(rainIntensity, fadeInTime);
                audioStatus = "Player entered rain zone - fading in over " + fadeInTime + " seconds";
            }
            else
            {
                Debug.Log("PLAYER EXITED RAIN ZONE - Fading Out");
                // Fade out the rain sound
                FadeAudioParameter(0f, fadeOutTime);
                audioStatus = "Player exited rain zone - fading out over " + fadeOutTime + " seconds";
            }
        }
    }
    
    void FadeAudioParameter(float targetValue, float fadeTime)
    {
        if (!isEventValid) return;
        
        // If already fading, stop that fade
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        
        // Start a new fade
        fadeCoroutine = StartCoroutine(FadeAudioCoroutine(targetValue, fadeTime));
    }
    
    IEnumerator FadeAudioCoroutine(float targetValue, float fadeTime)
    {
        if (!isEventValid) 
        {
            fadeCoroutine = null;
            yield break;
        }
        
        float startValue = currentAudioIntensity;
        float startTime = Time.time;
        
        while (Time.time < startTime + fadeTime)
        {
            // Calculate smooth step for nicer fades
            float t = (Time.time - startTime) / fadeTime;
            float smoothT = t * t * (3f - 2f * t);  // Smoothstep function
            
            // Calculate new value
            float newValue = Mathf.Lerp(startValue, targetValue, smoothT);
            
            // Set the FMOD parameter
            try
            {
                FMOD.Studio.System studioSystem = RuntimeManager.StudioSystem;
                studioSystem.setParameterByName(globalParameterName, newValue);
                currentAudioIntensity = newValue;
                
                // Update status text during fade
                if (targetValue > 0)
                    audioStatus = "Fading in: " + (newValue / targetValue * 100f).ToString("F0") + "%";
                else
                    audioStatus = "Fading out: " + (100f - (newValue * 100f)).ToString("F0") + "%";
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error during audio fade: " + e.Message);
                break;
            }
            
            yield return null;
        }
        
        // Make sure we end at exactly the target value
        try
        {
            FMOD.Studio.System studioSystem = RuntimeManager.StudioSystem;
            studioSystem.setParameterByName(globalParameterName, targetValue);
            currentAudioIntensity = targetValue;
            
            // Update status text after fade completes
            if (targetValue > 0)
                audioStatus = "Rain sound active (" + targetValue.ToString("F2") + ")";
            else
                audioStatus = "Rain sound muted";
                
            Debug.Log("Audio fade complete. Value: " + targetValue);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error setting final audio value: " + e.Message);
        }
        
        fadeCoroutine = null;
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
        
        // Only update audio if player is in zone
        if (playerInZone)
        {
            FadeAudioParameter(rainIntensity, 0.5f);  // Use a short fade for intensity changes
        }
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
    
    // Public methods to manually control player detection (can be called from other scripts)
    public void ForcePlayerEnterZone()
    {
        Debug.Log("Player manually set to be in rain zone");
        playerInZone = true;
        FadeAudioParameter(rainIntensity, fadeInTime);
    }
    
    public void ForcePlayerExitZone()
    {
        Debug.Log("Player manually set to be outside rain zone");
        playerInZone = false;
        FadeAudioParameter(0f, fadeOutTime);
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
    
    [ContextMenu("Test Fade In")]
    void TestFadeIn()
    {
        FadeAudioParameter(rainIntensity, fadeInTime);
    }
    
    [ContextMenu("Test Fade Out")]
    void TestFadeOut()
    {
        FadeAudioParameter(0f, fadeOutTime);
    }
    
    [ContextMenu("Check Global Parameter Value")]
    void CheckGlobalParameterValue()
    {
        float value = GetCurrentGlobalParameter();
        Debug.Log("Global parameter check result: " + value);
    }
    
    [ContextMenu("Force Player Enter Zone")]
    void TestPlayerEnterZone()
    {
        ForcePlayerEnterZone();
    }
    
    [ContextMenu("Force Player Exit Zone")]
    void TestPlayerExitZone()
    {
        ForcePlayerExitZone();
    }
    
    [ContextMenu("Restart FMOD Event")]
    void RestartFMODEvent()
    {
        ReleaseRainAudio();
        SetupRainAudio();
    }
    
    [ContextMenu("Log Zone and Player Positions")]
    void LogPositions()
    {
        if (player == null)
        {
            Debug.Log("Player reference is missing!");
            return;
        }
        
        Vector3 zoneCenter = useManualRainZone ? manualRainZoneCenter : transform.position;
        Vector3 zoneSize = useManualRainZone ? manualRainZoneSize : currentZoneSize;
        Vector3 zoneHalfSize = zoneSize * 0.5f;
        Vector3 playerPos = player.position;
        
        Debug.Log("DETAILED POSITION INFO:" +
                  "\nPlayer Position: " + playerPos +
                  "\nRain Zone Center: " + zoneCenter +
                  "\nRain Zone Size: " + zoneSize +
                  "\nRain Zone Type: " + (useManualRainZone ? "Manual" : "From Particle System") +
                  "\nRain Zone Bounds:" +
                  "\n  X: " + (zoneCenter.x - zoneHalfSize.x) + " to " + (zoneCenter.x + zoneHalfSize.x) +
                  "\n  Y: " + (zoneCenter.y - zoneHalfSize.y) + " to " + (zoneCenter.y + zoneHalfSize.y) +
                  "\n  Z: " + (zoneCenter.z - zoneHalfSize.z) + " to " + (zoneCenter.z + zoneHalfSize.z));
    }
    
    [ContextMenu("Set Manual Zone to Match Visible Rain")]
    void MatchZoneToVisibleRain()
    {
        // Use this if you need to adjust the manual zone to match where the rain actually appears
        useManualRainZone = true;
        
        // You can customize these values based on where your rain actually appears
        manualRainZoneCenter = transform.position;
        manualRainZoneSize = currentZoneSize;
        
        Debug.Log("Manual rain zone set to match the particle system settings. Adjust in inspector if needed.");
    }
    
    void ReleaseRainAudio()
    {
        if (isEventValid)
        {
            try
            {
                rainEventInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                rainEventInstance.release();
                isEventValid = false;
                audioStatus = "FMOD Event released";
                Debug.Log("FMOD Rain event released");
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error releasing FMOD rain event: " + e.Message);
            }
        }
    }
    
    void OnDestroy()
    {
        // Clean up FMOD event when object is destroyed
        ReleaseRainAudio();
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
    
    // Draw debug visualizations in the editor
    void OnDrawGizmos()
    {
        if (!enableDebugVisuals) return;
        
        // Draw the particle system zone in blue
        Gizmos.color = Color.blue;
        if (rainParticles != null)
        {
            var shape = rainParticles.shape;
            Vector3 particleZoneSize = shape.scale;
            Gizmos.DrawWireCube(transform.position, particleZoneSize);
        }
        
        // Draw the detection zone in yellow
        Gizmos.color = Color.yellow;
        if (useManualRainZone)
        {
            Gizmos.DrawWireCube(manualRainZoneCenter, manualRainZoneSize);
        }
        
        // If player is assigned, draw a line to show if player is in or out of zone
        if (player != null)
        {
            // Use red if outside zone, green if inside
            Gizmos.color = playerInZone ? Color.green : Color.red;
            Vector3 zoneCenter = useManualRainZone ? manualRainZoneCenter : transform.position;
            Gizmos.DrawLine(zoneCenter, player.position);
            Gizmos.DrawSphere(player.position, 0.5f);
        }
    }
}