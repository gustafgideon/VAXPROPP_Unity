using UnityEngine;
using FMODUnity;
using FMOD.Studio;

[RequireComponent(typeof(StudioEventEmitter))]
public class OccludableFMODEvent : MonoBehaviour
{
    [Header("Occlusion Parameters")]
    [SerializeField] private string occlusionParameterName = "Occlusion";
    [SerializeField] private string volumeParameterName = "Volume";
    [SerializeField] private bool useBuiltInLowpass = true;
    [SerializeField] private string lowpassParameterName = "Lowpass";
    
    [Header("Occlusion Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float maxOcclusionStrength = 1f;
    [SerializeField] private float transitionSpeed = 2f;
    [SerializeField] private AnimationCurve occlusionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Volume Control")]
    [SerializeField] private bool affectVolume = true;
    [Range(0f, 1f)]
    [SerializeField] private float minVolumeWhenOccluded = 0.2f;
    
    [Header("Low-pass Filter")]
    [SerializeField] private bool affectLowpass = true;
    [Range(0f, 1f)]
    [SerializeField] private float maxLowpassAmount = 0.8f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    
    private StudioEventEmitter eventEmitter;
    private EventInstance eventInstance;
    private float currentOcclusionValue = 0f;
    private float targetOcclusionValue = 0f;
    private bool hasOcclusionParameter = false;
    private bool hasVolumeParameter = false;
    private bool hasLowpassParameter = false;
    
    public float CurrentOcclusionValue => currentOcclusionValue;
    
    private void Start()
    {
        eventEmitter = GetComponent<StudioEventEmitter>();
        
        // Register with the occlusion manager
        if (SoundOcclusionManager.Instance != null)
        {
            SoundOcclusionManager.Instance.RegisterSound(this);
        }
        
        // Check for parameters when the event starts
        if (eventEmitter.EventInstance.isValid())
        {
            eventEmitter.EventInstance.getDescription(out EventDescription eventDescription);
            CheckForParameters(eventDescription);
        }
        else
        {
            // If event isn't valid yet, try checking parameters when it becomes available
            StartCoroutine(WaitForEventAndCheckParameters());
        }
    }
    
    private System.Collections.IEnumerator WaitForEventAndCheckParameters()
    {
        while (!eventEmitter.EventInstance.isValid())
        {
            yield return new WaitForEndOfFrame();
        }
        
        eventEmitter.EventInstance.getDescription(out EventDescription eventDescription);
        CheckForParameters(eventDescription);
    }
    
    private void OnEnable()
    {
        if (SoundOcclusionManager.Instance != null)
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
        // Smooth transition to target occlusion value
        if (Mathf.Abs(currentOcclusionValue - targetOcclusionValue) > 0.01f)
        {
            currentOcclusionValue = Mathf.Lerp(currentOcclusionValue, targetOcclusionValue, 
                Time.deltaTime * transitionSpeed);
            
            ApplyOcclusionEffects();
            
            if (showDebugInfo)
            {
                Debug.Log($"{gameObject.name} - Occlusion: {currentOcclusionValue:F2}");
            }
        }
    }
    
    private void CheckForParameters(EventDescription eventDescription)
    {
        // Check for occlusion parameter
        var result = eventDescription.getParameterDescriptionByName(occlusionParameterName, out FMOD.Studio.PARAMETER_DESCRIPTION paramDesc);
        hasOcclusionParameter = result == FMOD.RESULT.OK;
        
        // Check for volume parameter
        result = eventDescription.getParameterDescriptionByName(volumeParameterName, out paramDesc);
        hasVolumeParameter = result == FMOD.RESULT.OK;
        
        // Check for lowpass parameter
        result = eventDescription.getParameterDescriptionByName(lowpassParameterName, out paramDesc);
        hasLowpassParameter = result == FMOD.RESULT.OK;
        
        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} - Has Occlusion: {hasOcclusionParameter}, Has Volume: {hasVolumeParameter}, Has Lowpass: {hasLowpassParameter}");
        }
    }
    
    public void SetOcclusionValue(float occlusionValue)
    {
        targetOcclusionValue = Mathf.Clamp01(occlusionValue) * maxOcclusionStrength;
    }
    
    private void ApplyOcclusionEffects()
    {
        if (!eventEmitter.IsPlaying()) return;
        
        EventInstance instance = eventEmitter.EventInstance;
        if (!instance.isValid()) return;
        
        float curveValue = occlusionCurve.Evaluate(currentOcclusionValue);
        
        // Apply custom occlusion parameter
        if (hasOcclusionParameter)
        {
            instance.setParameterByName(occlusionParameterName, curveValue);
        }
        
        // Apply volume reduction
        if (affectVolume && hasVolumeParameter)
        {
            float volumeMultiplier = Mathf.Lerp(1f, minVolumeWhenOccluded, curveValue);
            instance.setParameterByName(volumeParameterName, volumeMultiplier);
        }
        
        // Apply low-pass filter
        if (affectLowpass)
        {
            if (hasLowpassParameter)
            {
                float lowpassAmount = curveValue * maxLowpassAmount;
                instance.setParameterByName(lowpassParameterName, lowpassAmount);
            }
            else if (useBuiltInLowpass)
            {
                // Use FMOD's built-in lowpass if no custom parameter exists
                // Note: This requires the event to have a built-in lowpass effect
                float lowpassFreq = Mathf.Lerp(22000f, 1000f, curveValue * maxLowpassAmount);
                var lowpassResult = instance.setParameterByName("lpf_cutoff", lowpassFreq);
                
                // If built-in lowpass doesn't exist, you might want to add a custom one in FMOD Studio
                if (lowpassResult != FMOD.RESULT.OK && showDebugInfo)
                {
                    Debug.LogWarning($"{gameObject.name} - Built-in lowpass parameter not found. Add a lowpass effect in FMOD Studio or create a custom '{lowpassParameterName}' parameter.");
                }
            }
        }
    }
    
    private void OnValidate()
    {
        if (eventEmitter == null)
            eventEmitter = GetComponent<StudioEventEmitter>();
    }
}