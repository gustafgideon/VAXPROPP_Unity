using UnityEngine;
using FMODUnity;
using FMOD.Studio;

[RequireComponent(typeof(StudioEventEmitter))]
public class OccludableFMODEvent : MonoBehaviour
{
    [Header("Occlusion Parameters")]
    [SerializeField] private string occlusionParameterName = "Occlusion";
    
    [Header("Occlusion Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float maxOcclusionStrength = 0.8f;
    [SerializeField] private float transitionSpeed = 2f;
    [SerializeField] private AnimationCurve occlusionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Volume Control")]
    [SerializeField] private bool useUnityVolumeControl = true;
    [SerializeField] private bool useFMODParameterControl = true; // Enable both!
    [Range(0.1f, 1f)]
    [SerializeField] private float minVolumeWhenOccluded = 0.25f;
    
    [Header("Event Management")]
    [SerializeField] private bool autoRestartStoppedEvents = true;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true; // Enable by default for testing
    
    private StudioEventEmitter eventEmitter;
    private float currentOcclusionValue = 0f;
    private float targetOcclusionValue = 0f;
    private float baseVolume = 1f;
    private bool isInitialized = false;
    
    public float CurrentOcclusionValue => currentOcclusionValue;
    
    private void Awake()
    {
        eventEmitter = GetComponent<StudioEventEmitter>();
    }
    
    private void Start()
    {
        StartCoroutine(InitializeWhenReady());
    }
    
    private System.Collections.IEnumerator InitializeWhenReady()
    {
        // Ensure event is playing
        if (!eventEmitter.IsPlaying())
        {
            eventEmitter.Play();
        }
        
        // Wait for the event to be valid
        int attempts = 0;
        while (!eventEmitter.EventInstance.isValid() && attempts < 100)
        {
            yield return new WaitForEndOfFrame();
            attempts++;
        }
        
        if (eventEmitter.EventInstance.isValid())
        {
            eventEmitter.EventInstance.getVolume(out baseVolume);
            
            // Test if the parameter exists
            FMOD.RESULT result = eventEmitter.EventInstance.getParameterByName(occlusionParameterName, out float currentValue);
            bool hasParameter = result == FMOD.RESULT.OK;
            
            if (showDebugInfo)
            {
                Debug.Log($"{gameObject.name} - Initialized:");
                Debug.Log($"  Event valid: {eventEmitter.EventInstance.isValid()}");
                Debug.Log($"  Base volume: {baseVolume}");
                Debug.Log($"  Has '{occlusionParameterName}' parameter: {hasParameter}");
                if (hasParameter)
                {
                    Debug.Log($"  Current {occlusionParameterName} value: {currentValue}");
                }
            }
            
            isInitialized = true;
            
            // Register with occlusion manager
            if (SoundOcclusionManager.Instance != null)
            {
                SoundOcclusionManager.Instance.RegisterSound(this);
            }
        }
        else
        {
            Debug.LogError($"{gameObject.name} - Failed to initialize FMOD event after {attempts} attempts");
        }
    }
    
    private void OnEnable()
    {
        if (isInitialized && SoundOcclusionManager.Instance != null)
        {
            SoundOcclusionManager.Instance.RegisterSound(this);
        }
    }
    
    private void OnDisable()
    {
        if (SoundOcclusionManager.Instance != null)
        {
            SoundOcclusionManager.Instance.UnregisterSound(this);
        }
    }
    
    private void Update()
    {
        if (!isInitialized) return;
        
        // Check if event stopped
        if (autoRestartStoppedEvents && !eventEmitter.IsPlaying())
        {
            if (showDebugInfo)
            {
                Debug.LogWarning($"{gameObject.name} - Event stopped, restarting...");
            }
            eventEmitter.Play();
        }
        
        // Smooth transition to target occlusion value
        if (Mathf.Abs(currentOcclusionValue - targetOcclusionValue) > 0.01f)
        {
            currentOcclusionValue = Mathf.Lerp(currentOcclusionValue, targetOcclusionValue, 
                Time.deltaTime * transitionSpeed);
            
            ApplyOcclusionEffects();
        }
    }
    
    public void SetOcclusionValue(float occlusionValue)
    {
        float newTarget = Mathf.Clamp01(occlusionValue) * maxOcclusionStrength;
        
        if (showDebugInfo && Mathf.Abs(newTarget - targetOcclusionValue) > 0.05f)
        {
            Debug.Log($"{gameObject.name} - Occlusion target changed: {targetOcclusionValue:F2} -> {newTarget:F2}");
        }
        
        targetOcclusionValue = newTarget;
    }
    
    private void ApplyOcclusionEffects()
    {
        if (!isInitialized || eventEmitter == null || !eventEmitter.IsPlaying()) return;
        
        EventInstance instance = eventEmitter.EventInstance;
        if (!instance.isValid()) return;
        
        float curveValue = occlusionCurve.Evaluate(currentOcclusionValue);
        
        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} - Applying occlusion: {currentOcclusionValue:F2} -> curve: {curveValue:F2}");
        }
        
        // Apply Unity volume control
        if (useUnityVolumeControl)
        {
            float volumeMultiplier = Mathf.Lerp(1f, minVolumeWhenOccluded, curveValue);
            float finalVolume = baseVolume * volumeMultiplier;
            finalVolume = Mathf.Max(finalVolume, 0.05f); // Safety minimum
            
            FMOD.RESULT volumeResult = instance.setVolume(finalVolume);
            
            if (showDebugInfo)
            {
                Debug.Log($"  Unity Volume: {finalVolume:F3} (base: {baseVolume:F2} * mult: {volumeMultiplier:F2}) Result: {volumeResult}");
            }
        }
        
        // Apply FMOD parameter
        if (useFMODParameterControl)
        {
            FMOD.RESULT paramResult = instance.setParameterByName(occlusionParameterName, curveValue);
            
            if (showDebugInfo)
            {
                Debug.Log($"  FMOD Parameter '{occlusionParameterName}': {curveValue:F3} Result: {paramResult}");
                
                if (paramResult != FMOD.RESULT.OK)
                {
                    Debug.LogWarning($"  Parameter '{occlusionParameterName}' failed: {paramResult}");
                }
            }
            
            // Verify the parameter was set
            if (paramResult == FMOD.RESULT.OK)
            {
                instance.getParameterByName(occlusionParameterName, out float verifyValue);
                if (showDebugInfo && Mathf.Abs(verifyValue - curveValue) > 0.01f)
                {
                    Debug.LogWarning($"  Parameter verification failed: set {curveValue:F3}, got {verifyValue:F3}");
                }
            }
        }
    }
    
    // Manual test functions
    [ContextMenu("Test 50% Occlusion")]
    public void Test50Occlusion()
    {
        SetOcclusionValue(0.5f);
        Debug.Log($"Testing 50% occlusion on {gameObject.name}");
    }
    
    [ContextMenu("Test Full Occlusion")]
    public void TestFullOcclusion()
    {
        SetOcclusionValue(1f);
        Debug.Log($"Testing full occlusion on {gameObject.name}");
    }
    
    [ContextMenu("Clear Occlusion")]
    public void ClearOcclusion()
    {
        SetOcclusionValue(0f);
        Debug.Log($"Clearing occlusion on {gameObject.name}");
    }
}