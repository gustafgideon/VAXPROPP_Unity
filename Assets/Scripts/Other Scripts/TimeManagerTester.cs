using UnityEngine;

/// <summary>
/// Simple test component to validate TimeManager configuration changes.
/// Add this to a GameObject with TimeManager to test the new features.
/// </summary>
public class TimeManagerTester : MonoBehaviour
{
    [Header("Test Settings")]
    [SerializeField] private bool enableTesting = true;
    [SerializeField] private float testSpeedMultiplier = 5.0f;
    [SerializeField] private int testSunriseHour = 5;
    [SerializeField] private int testDayHour = 7;
    [SerializeField] private int testSunsetHour = 19;
    [SerializeField] private int testNightHour = 21;

    private TimeManager timeManager;

    void Start()
    {
        timeManager = GetComponent<TimeManager>();
        
        if (enableTesting && timeManager != null)
        {
            Debug.Log("🧪 TimeManager Tester: Starting validation tests");
            
            // Test 1: Verify configurable hours work
            TestConfigurableHours();
            
            // Test 2: Verify speed multiplier works  
            TestSpeedMultiplier();
        }
    }

    private void TestConfigurableHours()
    {
        Debug.Log("🧪 Testing configurable hour settings...");
        
        // Use reflection to test if the fields exist and can be set
        var timeManagerType = typeof(TimeManager);
        
        // Check if new fields exist
        var sunriseHourField = timeManagerType.GetField("sunriseHour", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var dayHourField = timeManagerType.GetField("dayHour", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var sunsetHourField = timeManagerType.GetField("sunsetHour", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var nightHourField = timeManagerType.GetField("nightHour", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (sunriseHourField != null && dayHourField != null && sunsetHourField != null && nightHourField != null)
        {
            Debug.Log("✅ All configurable hour fields found");
            
            // Get current values
            int currentSunrise = (int)sunriseHourField.GetValue(timeManager);
            int currentDay = (int)dayHourField.GetValue(timeManager);
            int currentSunset = (int)sunsetHourField.GetValue(timeManager);
            int currentNight = (int)nightHourField.GetValue(timeManager);
            
            Debug.Log($"📋 Current hours - Sunrise: {currentSunrise}, Day: {currentDay}, Sunset: {currentSunset}, Night: {currentNight}");
        }
        else
        {
            Debug.LogError("❌ Missing configurable hour fields");
        }
    }

    private void TestSpeedMultiplier()
    {
        Debug.Log("🧪 Testing time speed multiplier...");
        
        var timeManagerType = typeof(TimeManager);
        var speedField = timeManagerType.GetField("timeSpeedMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (speedField != null)
        {
            float currentSpeed = (float)speedField.GetValue(timeManager);
            Debug.Log($"✅ Speed multiplier field found. Current value: {currentSpeed}");
            
            // Set test speed and verify
            speedField.SetValue(timeManager, testSpeedMultiplier);
            float newSpeed = (float)speedField.GetValue(timeManager);
            Debug.Log($"📋 Speed multiplier set to: {newSpeed}");
        }
        else
        {
            Debug.LogError("❌ Speed multiplier field not found");
        }
    }

    [ContextMenu("Run Manual Test")]
    public void RunManualTest()
    {
        if (timeManager == null)
            timeManager = GetComponent<TimeManager>();
            
        TestConfigurableHours();
        TestSpeedMultiplier();
        
        Debug.Log("🧪 Manual test complete!");
    }
}