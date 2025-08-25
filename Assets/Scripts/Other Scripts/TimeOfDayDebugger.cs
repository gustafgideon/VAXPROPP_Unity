using UnityEngine;

public class TimeOfDayManagerDebugger : MonoBehaviour
{
    [Header("Debug Info")]
    [SerializeField] private bool continuousLogging = true;
    [SerializeField] private float logInterval = 2f; // Log every 2 seconds
    
    private TimeOfDayManager timeOfDayManager;
    private float lastLogTime;
    private float previousTime = -1f;
    
    void Start()
    {
        timeOfDayManager = FindObjectOfType<TimeOfDayManager>();
        if (timeOfDayManager == null)
        {
            Debug.LogError("❌ No TimeOfDayManager found!");
            return;
        }
        
        Debug.Log("🔍 TimeOfDayManager Debug Started");
        LogCurrentState();
    }
    
    void Update()
    {
        if (continuousLogging && timeOfDayManager != null && Time.time - lastLogTime > logInterval)
        {
            LogCurrentState();
            lastLogTime = Time.time;
        }
    }
    
    private void LogCurrentState()
    {
        if (timeOfDayManager == null) return;
        
        // Use only the methods we know exist
        float currentTime = timeOfDayManager.GetCurrentTime();
        TimeOfDayManager.TimeOfDay currentTimeOfDay = timeOfDayManager.GetCurrentTimeOfDay();
        
        Debug.Log($"🕒 Time: {currentTime:F3} | Phase: {currentTimeOfDay}");
        
        // Check if time is progressing
        if (previousTime >= 0f && Mathf.Approximately(currentTime, previousTime))
        {
            Debug.LogWarning("⚠️ Time appears to be stuck! Check if time progression is enabled.");
        }
        else if (previousTime >= 0f)
        {
            float timeChange = currentTime - previousTime;
            Debug.Log($"⏰ Time changed by: {timeChange:F4}");
        }
        
        previousTime = currentTime;
    }
    
    [ContextMenu("Force Log Current State")]
    public void ForceLogCurrentState()
    {
        LogCurrentState();
    }
    
    [ContextMenu("Check TimeOfDayManager Settings")]
    public void CheckSettings()
    {
        if (timeOfDayManager == null) return;
        
        Debug.Log("⚙️ Checking TimeOfDayManager settings...");
        
        float currentTime = timeOfDayManager.GetCurrentTime();
        TimeOfDayManager.TimeOfDay currentPhase = timeOfDayManager.GetCurrentTimeOfDay();
        
        Debug.Log($"🕒 Current Time: {currentTime:F3}");
        Debug.Log($"🌅 Current Phase: {currentPhase}");
        
        // Check what fields are available in inspector
        var fields = timeOfDayManager.GetType().GetFields();
        foreach (var field in fields)
        {
            if (field.Name.Contains("Time") || field.Name.Contains("time"))
            {
                var value = field.GetValue(timeOfDayManager);
                Debug.Log($"📋 {field.Name}: {value}");
            }
        }
    }
    
    [ContextMenu("Test Time Progression")]
    public void TestTimeProgression()
    {
        if (timeOfDayManager == null) return;
        
        Debug.Log("🧪 Starting time progression test...");
        
        float startTime = timeOfDayManager.GetCurrentTime();
        Debug.Log($"🧪 Start Time: {startTime:F3}");
        
        // Enable continuous logging for 10 seconds
        continuousLogging = true;
        logInterval = 1f; // Log every second
        
        Invoke("StopTimeTest", 10f);
    }
    
    private void StopTimeTest()
    {
        Debug.Log("🧪 Time progression test complete.");
        logInterval = 2f; // Back to normal
    }
}