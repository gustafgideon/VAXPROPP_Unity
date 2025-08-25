using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FMOD.Studio;
using FMODUnity;

public enum Location  
{  
   Forest, Factory
}

public class AmbianceManager : MonoBehaviour
{
   public static AmbianceManager Instance { get; private set; }

   [Header("Ambiance Emitter")]
   [SerializeField] private StudioEventEmitter forestAmbianceEmitter;
   [SerializeField] private StudioEventEmitter factoryAmbianceEmitter;

   [Header("Fade Settings")]
   [SerializeField] private float fadeTime = 2f; // Location change fade only

   [Header("Time of Day Integration")]
   [SerializeField] private bool respondToTimeOfDay = true;
   [SerializeField] private string timeOfDayParameterName = "TimeOfDay";
   [SerializeField] private bool useGlobalParameter = true; // When true, let TimeOfDayManager handle transitions
   
   [Header("Debug")]
   [SerializeField] private bool debugLogging = true;

   private StudioEventEmitter emitter;
   private StudioEventEmitter currentlyPlaying;
   private TimeOfDayManager timeOfDayManager;
   
   // Time of day crossfade tracking (only used for local parameters now)
   private bool isTimeTransitioning = false;
   private Coroutine timeTransitionCoroutine;

   private void Awake()
   {
      if (Instance != null && Instance != this) 
      {
         Destroy(this);
      }
      else
      {
         Instance = this;
      }
      DontDestroyOnLoad(this);
   }

   private void Start()
   {
      if (respondToTimeOfDay)
      {
         timeOfDayManager = FindObjectOfType<TimeOfDayManager>();
         if (timeOfDayManager != null)
         {
            if (useGlobalParameter)
            {
               // For global parameters, listen to transition events but don't handle the parameter ourselves
               timeOfDayManager.OnTransitionStarted += OnTransitionStarted;
               timeOfDayManager.OnTimeOfDayChanged += OnTimeOfDayChanged;
               timeOfDayManager.OnParameterValueChanged += OnParameterValueChanged;
            }
            else
            {
               // For local parameters, use the old system
               timeOfDayManager.OnTimeOfDayChanged += OnTimeOfDayChanged;
            }
            
            if (debugLogging) Debug.Log("✅ AmbianceManager connected to TimeOfDayManager");
         }
         else
         {
            Debug.LogWarning("❌ TimeOfDayManager not found!");
         }
      }
   }

   private void Update()
   {
      if (GameObject.FindWithTag("Player") != null)
      {
         transform.position = GameObject.FindWithTag("Player").transform.position;
      }
   }

   private void OnDestroy()
   {
      if (timeOfDayManager != null)
      {
         timeOfDayManager.OnTimeOfDayChanged -= OnTimeOfDayChanged;
         timeOfDayManager.OnTransitionStarted -= OnTransitionStarted;
         timeOfDayManager.OnParameterValueChanged -= OnParameterValueChanged;
      }
   }

   private void GetLocation(Location location)
   {
      switch (location)
      {
         case Location.Forest:
            emitter = forestAmbianceEmitter;
            break;
         case Location.Factory:
            emitter = factoryAmbianceEmitter;
            break;
      }
   }

   // Called when TimeOfDayManager starts a transition
   private void OnTransitionStarted(TimeOfDayManager.TimeOfDay targetTimeOfDay)
   {
      if (useGlobalParameter && debugLogging)
      {
         Debug.Log($"🌍 TimeOfDayManager started transition to: {targetTimeOfDay}");
      }
   }

   // Called during TimeOfDayManager's parameter updates
   private void OnParameterValueChanged(float parameterValue)
   {
      if (useGlobalParameter && debugLogging)
      {
         // Optional: Log parameter changes during transition
         // Debug.Log($"🎵 FMOD Parameter value: {parameterValue:F2}");
      }
   }

   // Called when time of day changes (now only after transition is complete)
   private void OnTimeOfDayChanged(TimeOfDayManager.TimeOfDay newTimeOfDay)
   {
      if (debugLogging) Debug.Log($"🌅 Time of day transition COMPLETED to: {newTimeOfDay}");
      
      if (useGlobalParameter)
      {
         // For global parameters, TimeOfDayManager has already handled the smooth transition
         // We just need to react to the completed change (if needed)
         // The FMOD parameter is already set correctly by TimeOfDayManager
         
         if (debugLogging) Debug.Log($"✅ Global parameter transition handled by TimeOfDayManager");
      }
      else
      {
         // Local parameter approach (original method)
         if (currentlyPlaying == null || !currentlyPlaying.IsActive || isTimeTransitioning)
         {
            return;
         }
         
         string newTimeLabel = newTimeOfDay == TimeOfDayManager.TimeOfDay.Day ? "Day" : "Night";
         timeTransitionCoroutine = StartCoroutine(TimeOfDayCrossfade(newTimeLabel));
      }
   }

   // Helper method to set global time parameter (only used for immediate application)
   private void SetGlobalTimeParameter(string timeLabel)
   {
      // Try labeled version first
      FMOD.RESULT labelResult = RuntimeManager.StudioSystem.setParameterByNameWithLabel(timeOfDayParameterName, timeLabel);
      
      if (labelResult == FMOD.RESULT.OK)
      {
         if (debugLogging) Debug.Log($"✅ Set global labeled parameter '{timeOfDayParameterName}' to '{timeLabel}'");
      }
      else
      {
         // Fallback to numeric
         float numericValue = timeLabel == "Day" ? 0f : 1f;
         FMOD.RESULT numericResult = RuntimeManager.StudioSystem.setParameterByName(timeOfDayParameterName, numericValue);
         
         if (numericResult == FMOD.RESULT.OK)
         {
            if (debugLogging) Debug.Log($"✅ Set global numeric parameter '{timeOfDayParameterName}' to {numericValue} ({timeLabel})");
         }
         else
         {
            Debug.LogError($"❌ Failed to set global parameter '{timeOfDayParameterName}'. Label: {labelResult}, Numeric: {numericResult}");
         }
      }
   }

   // Local parameter crossfade (only used when useGlobalParameter = false)
   private IEnumerator TimeOfDayCrossfade(string newTimeLabel)
   {
      isTimeTransitioning = true;
      
      // Use a default transition duration of 5 seconds for local parameter crossfades
      float transitionDuration = 5f;
      
      if (debugLogging) Debug.Log($"🎵 Starting {transitionDuration}s local crossfade to {newTimeLabel}");

      // Store reference to original
      StudioEventEmitter originalEmitter = currentlyPlaying;
      string oldTimeLabel = GetOppositeTimeLabel(newTimeLabel);

      // Create duplicate for crossfading
      GameObject tempObj = new GameObject($"TempCrossfade_{newTimeLabel}_{Time.time}");
      tempObj.transform.position = transform.position;
      
      StudioEventEmitter tempEmitter = tempObj.AddComponent<StudioEventEmitter>();
      tempEmitter.EventReference = originalEmitter.EventReference;
      
      if (debugLogging) Debug.Log($"🔄 Created temp emitter: {tempObj.name}");

      // Start the new version
      tempEmitter.Play();

      // Wait for both to be active
      yield return new WaitForSeconds(0.1f);

      // Verify both are active
      if (!originalEmitter.IsActive || !tempEmitter.IsActive)
      {
         Debug.LogError($"❌ Emitters not active! Original: {originalEmitter.IsActive}, Temp: {tempEmitter.IsActive}");
         CleanupFailedTransition(tempObj);
         yield break;
      }

      // Set different time parameters (this won't work with global parameters)
      if (debugLogging) Debug.Log($"🎛️ Setting original to {oldTimeLabel}, temp to {newTimeLabel}");
      SetEmitterTimeParameter(originalEmitter, oldTimeLabel);
      SetEmitterTimeParameter(tempEmitter, newTimeLabel);

      // Start both at appropriate volumes
      originalEmitter.EventInstance.setVolume(1f);
      tempEmitter.EventInstance.setVolume(0f);

      // Manual volume crossfade
      float timer = 0f;
      while (timer < transitionDuration)
      {
         timer += Time.deltaTime;
         float progress = timer / transitionDuration;
         
         float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
         float oldVolume = 1f - smoothProgress;
         float newVolume = smoothProgress;
         
         originalEmitter.EventInstance.setVolume(oldVolume);
         tempEmitter.EventInstance.setVolume(newVolume);

         if (debugLogging && timer % 2f < Time.deltaTime)
         {
            Debug.Log($"🔊 Crossfade progress: {progress:F2} | Old vol: {oldVolume:F2} | New vol: {newVolume:F2}");
         }

         yield return null;
      }

      // Finalize crossfade
      originalEmitter.EventInstance.setVolume(0f);
      tempEmitter.EventInstance.setVolume(1f);
      originalEmitter.Stop();
      yield return null;

      // Restart original with new settings
      SetEmitterTimeParameter(originalEmitter, newTimeLabel);
      originalEmitter.Play();
      yield return new WaitForSeconds(0.1f);
      originalEmitter.EventInstance.setVolume(1f);

      // Clean up
      tempEmitter.Stop();
      Destroy(tempObj);

      isTimeTransitioning = false;
      if (debugLogging) Debug.Log($"✅ Local crossfade to {newTimeLabel} complete");
   }

   private void CleanupFailedTransition(GameObject tempObj)
   {
      if (tempObj != null) Destroy(tempObj);
      isTimeTransitioning = false;
      Debug.LogError("❌ Transition failed - cleaned up");
   }

   private string GetOppositeTimeLabel(string timeLabel)
   {
      return timeLabel == "Day" ? "Night" : "Day";
   }

   private void SetEmitterTimeParameter(StudioEventEmitter targetEmitter, string timeLabel)
   {
      if (targetEmitter == null || !targetEmitter.IsActive)
      {
         if (debugLogging) Debug.LogWarning($"⚠️ Cannot set parameter on inactive emitter");
         return;
      }

      if (useGlobalParameter)
      {
         // For global parameters, don't set anything - TimeOfDayManager handles it
         if (debugLogging) Debug.Log($"ℹ️ Global parameter mode - TimeOfDayManager controls '{timeOfDayParameterName}'");
         return;
      }

      // Try local parameter setting (only when useGlobalParameter = false)
      FMOD.RESULT labelResult = targetEmitter.EventInstance.setParameterByNameWithLabel(timeOfDayParameterName, timeLabel);
      
      if (labelResult == FMOD.RESULT.OK)
      {
         if (debugLogging) Debug.Log($"✅ Set local labeled parameter '{timeOfDayParameterName}' to '{timeLabel}'");
      }
      else
      {
         float numericValue = timeLabel == "Day" ? 0f : 1f;
         FMOD.RESULT numericResult = targetEmitter.EventInstance.setParameterByName(timeOfDayParameterName, numericValue);
         
         if (numericResult == FMOD.RESULT.OK)
         {
            if (debugLogging) Debug.Log($"✅ Set local numeric parameter '{timeOfDayParameterName}' to {numericValue} ({timeLabel})");
         }
         else
         {
            Debug.LogError($"❌ Failed to set local parameter '{timeOfDayParameterName}'. Label: {labelResult}, Numeric: {numericResult}");
         }
      }
   }

   // Test methods - redirect to TimeOfDayManager when using global parameters
   [ContextMenu("Test Day Transition")]
   public void TestDayTransition()
   {
      if (useGlobalParameter)
      {
         Debug.Log("🔧 Global parameter mode - use TimeOfDayManager.ForceDay() instead");
         if (timeOfDayManager != null)
         {
            timeOfDayManager.ForceDay();
         }
      }
      else if (currentlyPlaying != null)
      {
         StartCoroutine(TimeOfDayCrossfade("Day"));
      }
      else
      {
         Debug.LogWarning("No ambiance playing to test");
      }
   }

   [ContextMenu("Test Night Transition")]
   public void TestNightTransition()
   {
      if (useGlobalParameter)
      {
         Debug.Log("🔧 Global parameter mode - use TimeOfDayManager.ForceNight() instead");
         if (timeOfDayManager != null)
         {
            timeOfDayManager.ForceNight();
         }
      }
      else if (currentlyPlaying != null)
      {
         StartCoroutine(TimeOfDayCrossfade("Night"));
      }
      else
      {
         Debug.LogWarning("No ambiance playing to test");
      }
   }

   // Location change methods
   public void ChangeAmbiance(Location newLocation)
   {
      GetLocation(newLocation);
      
      if (currentlyPlaying != null && currentlyPlaying != emitter)
      {
         StartCoroutine(CrossFade(currentlyPlaying, emitter));
      }
      else
      {
         StartCoroutine(FadeIn(emitter));
      }
      
      currentlyPlaying = emitter;
      ApplyCurrentTimeOfDay();
   }

   private void ApplyCurrentTimeOfDay()
   {
      if (timeOfDayManager != null)
      {
         string timeLabel = timeOfDayManager.GetCurrentTimeOfDay() == TimeOfDayManager.TimeOfDay.Day ? "Day" : "Night";
         
         if (useGlobalParameter)
         {
            // For global parameters, just set it immediately (no transition needed here)
            SetGlobalTimeParameter(timeLabel);
         }
         else if (currentlyPlaying != null && currentlyPlaying.IsActive)
         {
            SetEmitterTimeParameter(currentlyPlaying, timeLabel);
         }
      }
   }

   private IEnumerator CrossFade(StudioEventEmitter oldEmitter, StudioEventEmitter newEmitter)
   {
      if (!newEmitter.IsActive)
         newEmitter.Play();

      float timer = 0f;
      while (timer < fadeTime)
      {
         timer += Time.deltaTime;
         float progress = timer / fadeTime;
         
         oldEmitter.EventInstance.setVolume(1f - progress);
         newEmitter.EventInstance.setVolume(progress);
         
         yield return null;
      }
      
      oldEmitter.EventInstance.setVolume(0f);
      newEmitter.EventInstance.setVolume(1f);
      oldEmitter.Stop();
   }

   private IEnumerator FadeIn(StudioEventEmitter targetEmitter)
   {
      if (!targetEmitter.IsActive)
         targetEmitter.Play();

      float timer = 0f;
      while (timer < fadeTime)
      {
         timer += Time.deltaTime;
         float progress = timer / fadeTime;
         targetEmitter.EventInstance.setVolume(progress);
         yield return null;
      }
      
      targetEmitter.EventInstance.setVolume(1f);
   }

   public void PlayAudio(Location location)
   {
      GetLocation(location);
      if (!emitter.IsActive)
      {
         emitter.Play();
         currentlyPlaying = emitter;
         ApplyCurrentTimeOfDay();
         if (debugLogging) Debug.Log($"🎵 Started playing {location} ambiance");
      }
   }

   public void StopAudio(Location location)
   {
      GetLocation(location);
      if (emitter.IsActive)
      {
         emitter.Stop();
         if (currentlyPlaying == emitter)
            currentlyPlaying = null;
         if (debugLogging) Debug.Log($"🛑 Stopped {location} ambiance");
      }
   }

   public void SetParameter(Location location, string parameterName, float parameterValue)
   {
      GetLocation(location);
      if (emitter.IsActive)
          emitter.SetParameter(parameterName, parameterValue);
   }
}