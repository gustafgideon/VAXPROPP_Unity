using UnityEngine;
using FMODUnity;

public class TimeOfDayManager : MonoBehaviour
{
    [Header("FMOD Settings")]
    [SerializeField] private string timeOfDayParameterName = "TimeOfDay";
    
    [Header("Time Settings")]
    [SerializeField] private float dayDurationInSeconds = 300f; // 5 minutes per cycle
    [SerializeField] private bool useRealTime = false;
    [SerializeField] private float dayStartTime = 0.25f; // 6 AM (0.25 of 24 hours)
    [SerializeField] private float nightStartTime = 0.75f; // 6 PM (0.75 of 24 hours)
    
    [Header("Transition Settings")]
    [SerializeField] private float transitionDurationInSeconds = 10f; // Smooth transition time
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // Curve for smooth transition
    
    // Public property to access transition duration
    public float TransitionDurationInSeconds => transitionDurationInSeconds;
    
    public enum TimeOfDay
    {
        Day,
        Night
    }
    
    private float currentTime = 0f; // 0-1 representing 24 hours
    private TimeOfDay currentTimeOfDay;
    private TimeOfDay targetTimeOfDay;
    
    // Transition variables
    private bool isTransitioning = false;
    private float transitionStartTime;
    private float transitionStartValue;
    private float transitionTargetValue;
    private float currentParameterValue = 0f;
    
    // Events
    public System.Action<TimeOfDay> OnTimeOfDayChanged; // Only fires when transition is COMPLETE
    public System.Action<float> OnTimeChanged;
    public System.Action<float> OnParameterValueChanged; // Fires during transition
    public System.Action<TimeOfDay> OnTransitionStarted; // New event for when transition starts
    
    void Start()
    {
        if (useRealTime)
        {
            System.DateTime now = System.DateTime.Now;
            currentTime = (now.Hour + now.Minute / 60f + now.Second / 3600f) / 24f;
        }
        
        currentTimeOfDay = GetTimeOfDayFromTime(currentTime);
        targetTimeOfDay = currentTimeOfDay;
        currentParameterValue = currentTimeOfDay == TimeOfDay.Day ? 0f : 1f;
        SetGlobalParameter(currentParameterValue);
    }
    
    void Update()
    {
        UpdateTime();
        UpdateTimeOfDay();
        UpdateTransition();
        OnTimeChanged?.Invoke(currentTime);
    }
    
    private void UpdateTime()
    {
        if (useRealTime)
        {
            System.DateTime now = System.DateTime.Now;
            currentTime = (now.Hour + now.Minute / 60f + now.Second / 3600f) / 24f;
        }
        else
        {
            currentTime += Time.deltaTime / dayDurationInSeconds;
            if (currentTime >= 1f) currentTime -= 1f; // Wrap around
        }
    }
    
    private void UpdateTimeOfDay()
    {
        TimeOfDay newTimeOfDay = GetTimeOfDayFromTime(currentTime);
        
        if (newTimeOfDay != targetTimeOfDay && !isTransitioning)
        {
            StartTransition(newTimeOfDay);
        }
    }
    
    private void UpdateTransition()
    {
        if (!isTransitioning) return;
        
        float elapsedTime = Time.time - transitionStartTime;
        float progress = Mathf.Clamp01(elapsedTime / transitionDurationInSeconds);
        
        // Apply the animation curve for smoother transition
        float curveProgress = transitionCurve.Evaluate(progress);
        currentParameterValue = Mathf.Lerp(transitionStartValue, transitionTargetValue, curveProgress);
        
        SetGlobalParameter(currentParameterValue);
        OnParameterValueChanged?.Invoke(currentParameterValue);
        
        // Only complete the transition when progress reaches 1
        if (progress >= 1f)
        {
            // Transition complete - NOW update the current time of day and fire events
            isTransitioning = false;
            currentTimeOfDay = targetTimeOfDay; // This is the key fix - only update when complete
            
            OnTimeOfDayChanged?.Invoke(currentTimeOfDay);
            Debug.Log($"Time of day transition completed to: {currentTimeOfDay}");
        }
    }
    
    private void StartTransition(TimeOfDay newTimeOfDay)
    {
        targetTimeOfDay = newTimeOfDay;
        isTransitioning = true;
        transitionStartTime = Time.time;
        transitionStartValue = currentParameterValue;
        transitionTargetValue = newTimeOfDay == TimeOfDay.Day ? 0f : 1f;
        
        Debug.Log($"Starting transition from {currentTimeOfDay} to {newTimeOfDay}");
        
        // Fire the transition started event immediately (but NOT OnTimeOfDayChanged)
        OnTransitionStarted?.Invoke(newTimeOfDay);
    }
    
    private TimeOfDay GetTimeOfDayFromTime(float time)
    {
        if (time >= dayStartTime && time < nightStartTime)
        {
            return TimeOfDay.Day;
        }
        else
        {
            return TimeOfDay.Night;
        }
    }
    
    private void SetGlobalParameter(float value)
    {
        RuntimeManager.StudioSystem.setParameterByName(timeOfDayParameterName, value);
    }
    
    // Public methods
    public float GetCurrentTime() => currentTime;
    public TimeOfDay GetCurrentTimeOfDay() => currentTimeOfDay;
    public TimeOfDay GetTargetTimeOfDay() => targetTimeOfDay;
    public float GetCurrentParameterValue() => currentParameterValue;
    public bool IsTransitioning() => isTransitioning;
    public float GetTransitionProgress()
    {
        if (!isTransitioning) return 1f;
        float elapsedTime = Time.time - transitionStartTime;
        return Mathf.Clamp01(elapsedTime / transitionDurationInSeconds);
    }
    
    public void ForceTimeOfDay(TimeOfDay timeOfDay)
    {
        if (timeOfDay == TimeOfDay.Day)
        {
            currentTime = dayStartTime + 0.1f;
        }
        else
        {
            currentTime = nightStartTime + 0.1f;
        }
        
        // Force immediate transition
        StopTransition();
        StartTransition(timeOfDay);
    }
    
    public void ForceTimeOfDayImmediate(TimeOfDay timeOfDay)
    {
        if (timeOfDay == TimeOfDay.Day)
        {
            currentTime = dayStartTime + 0.1f;
        }
        else
        {
            currentTime = nightStartTime + 0.1f;
        }
        
        // Set immediately without transition
        StopTransition();
        currentTimeOfDay = timeOfDay;
        targetTimeOfDay = timeOfDay;
        currentParameterValue = timeOfDay == TimeOfDay.Day ? 0f : 1f;
        SetGlobalParameter(currentParameterValue);
        OnTimeOfDayChanged?.Invoke(currentTimeOfDay);
        OnParameterValueChanged?.Invoke(currentParameterValue);
    }
    
    private void StopTransition()
    {
        isTransitioning = false;
    }
    
    // Debug methods for testing
    [ContextMenu("Force Day")]
    public void ForceDay() => ForceTimeOfDay(TimeOfDay.Day);
    
    [ContextMenu("Force Night")]
    public void ForceNight() => ForceTimeOfDay(TimeOfDay.Night);
    
    [ContextMenu("Force Day Immediate")]
    public void ForceDayImmediate() => ForceTimeOfDayImmediate(TimeOfDay.Day);
    
    [ContextMenu("Force Night Immediate")]
    public void ForceNightImmediate() => ForceTimeOfDayImmediate(TimeOfDay.Night);
}