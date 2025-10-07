using System;
using System.Collections.Generic;
using UnityEngine;

public class AmbianceTrigger : MonoBehaviour
{
    public enum Action
    {
        Start,                 // Start ambiance event (location)
        Stop,                  // Stop ambiance event (location)
        SetEventParameter,     // Set an event-local parameter
        SetGlobalParameter,    // Set a global FMOD parameter immediately
        SetGlobalParameterFade // Fade a global parameter over time
    }

    [Serializable]
    public struct AudioSettings
    {
        public Action action;
        public Location location;        // Used for Start / Stop / SetEventParameter
        public string parameterName;     // Used for parameter-related actions
        public float parameterValue;     // Target value (or end value for fade)
        public float fadeTime;           // Used only for SetGlobalParameterFade
        public float fadeStartOverride;  // If > -999, use this as start value instead of reading current
    }

    [Header("Player Tag")]
    [SerializeField] private string playerTag = "Player";

    [Header("Enter Actions")]
    public AudioSettings[] triggerEnterAudioSettings;

    [Header("Exit Actions")]
    public AudioSettings[] triggerExitAudioSettings;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;

    // Local collider counter for this volume
    private int localCounter = 0;

    // Global parameter reference counts: parameterName -> active volume count
    private static readonly Dictionary<string, int> GlobalParamRefCounts = new Dictionary<string, int>();

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        localCounter++;

        // Execute enter actions only on the FIRST effective entry (localCounter == 1)
        // (Keeps same semantics as your original "if (counter > 0)" but avoids double-running)
        if (localCounter == 1)
        {
            if (debugLogging) Debug.Log($"[AmbianceTrigger:{name}] ENTER (localCounter=1)");
            ProcessAudioSettings(triggerEnterAudioSettings, entering: true);
        }
        else
        {
            if (debugLogging) Debug.Log($"[AmbianceTrigger:{name}] ENTER (localCounter={localCounter}) - skip enter actions");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        localCounter = Mathf.Max(0, localCounter - 1);

        // Only when fully exited (localCounter == 0) execute exit actions
        if (localCounter == 0)
        {
            if (debugLogging) Debug.Log($"[AmbianceTrigger:{name}] EXIT (localCounter=0)");
            ProcessAudioSettings(triggerExitAudioSettings, entering: false);
        }
        else
        {
            if (debugLogging) Debug.Log($"[AmbianceTrigger:{name}] EXIT (localCounter={localCounter}) - still inside");
        }
    }

    private void ProcessAudioSettings(AudioSettings[] settingsArray, bool entering)
    {
        if (settingsArray == null) return;

        foreach (var s in settingsArray)
        {
            switch (s.action)
            {
                case Action.Start:
                    AmbianceManager.Instance.PlayAudio(s.location);
                    break;

                case Action.Stop:
                    AmbianceManager.Instance.StopAudio(s.location);
                    break;

                case Action.SetEventParameter:
                    if (!string.IsNullOrEmpty(s.parameterName))
                        AmbianceManager.Instance.SetParameter(s.location, s.parameterName, s.parameterValue);
                    break;

                case Action.SetGlobalParameter:
                    HandleGlobalParamImmediate(s.parameterName, s.parameterValue);
                    break;

                case Action.SetGlobalParameterFade:
                    HandleGlobalParamFade(s);
                    break;
            }
        }
    }

    private void HandleGlobalParamImmediate(string parameterName, float value)
    {
        if (string.IsNullOrEmpty(parameterName)) return;

        AmbianceManager.Instance.SetGlobalParameter(parameterName, value);
        if (debugLogging) Debug.Log($"[AmbianceTrigger:{name}] GlobalParam {parameterName} -> {value}");
    }

    private void HandleGlobalParamFade(AudioSettings s)
    {
        if (string.IsNullOrEmpty(s.parameterName)) return;

        float startValue;
        // Decide starting value
        if (s.fadeStartOverride > -999f)
        {
            startValue = s.fadeStartOverride;
        }
        else
        {
            // Try read actual current value
            if (FMODUnity.RuntimeManager.StudioSystem.getParameterDescriptionByName(s.parameterName, out var desc) == FMOD.RESULT.OK)
            {
                FMODUnity.RuntimeManager.StudioSystem.getParameterByID(desc.id, out float raw, out _);
                startValue = raw;
            }
            else
            {
                startValue = 0f;
            }
        }

        AmbianceManager.Instance.SetGlobalParameterFade(s.parameterName, startValue, s.parameterValue, s.fadeTime <= 0 ? 0.01f : s.fadeTime);
        if (debugLogging) Debug.Log($"[AmbianceTrigger:{name}] Fade GlobalParam {s.parameterName} {startValue} -> {s.parameterValue} in {s.fadeTime}s");
    }

    // OPTIONAL: Global reference counting API if you want "only set to 0 when outside ALL volumes"
    // Call these manually instead of direct immediate sets if you configure that pattern.

    private static void IncrementGlobalParamRef(string parameterName)
    {
        if (!GlobalParamRefCounts.ContainsKey(parameterName))
            GlobalParamRefCounts[parameterName] = 0;
        GlobalParamRefCounts[parameterName]++;
    }

    private static bool DecrementGlobalParamRef(string parameterName)
    {
        if (!GlobalParamRefCounts.ContainsKey(parameterName)) return true;
        GlobalParamRefCounts[parameterName] = Mathf.Max(0, GlobalParamRefCounts[parameterName] - 1);
        return GlobalParamRefCounts[parameterName] == 0;
    }

    // Example pattern if you decide to swap SetGlobalParameter usage for ref counted variant:
    // (Not automatically wired in to avoid unexpected behaviour. Use intentionally.)
    private void ExampleRefCountedEnter(string parameterName, float insideValue)
    {
        IncrementGlobalParamRef(parameterName);
        AmbianceManager.Instance.SetGlobalParameter(parameterName, insideValue);
    }

    private void ExampleRefCountedExit(string parameterName, float outsideValue)
    {
        if (DecrementGlobalParamRef(parameterName))
            AmbianceManager.Instance.SetGlobalParameter(parameterName, outsideValue);
    }
}