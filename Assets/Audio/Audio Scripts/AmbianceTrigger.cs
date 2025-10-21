using System;
using UnityEngine;

public class AmbianceTrigger : MonoBehaviour
{
    public enum Action
    {
        Start,         // ChangeAmbiance(location) on enter
        FadeToSilence  // FadeOutCurrent() on enter (use in a dedicated "silence" volume)
    }

    [Serializable]
    public struct AudioSettings
    {
        public Action action;
        public Location location; // Only used for Action.Start
    }

    
    [SerializeField] private string playerTag = "Player";

    [Header("Enter Actions")]
    public AudioSettings[] triggerEnterAudioSettings;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;

    // Local collider counter for this volume (prevents duplicate enters with nested colliders)
    private int localCounter = 0;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        localCounter++;

        // Execute enter actions only on the FIRST effective entry (localCounter == 1)
        if (localCounter == 1)
        {
            if (debugLogging) Debug.Log($"[AmbianceTrigger:{name}] ENTER (localCounter=1)");
            ProcessEnterAudioSettings(triggerEnterAudioSettings);
        }
        else
        {
            if (debugLogging) Debug.Log($"[AmbianceTrigger:{name}] ENTER (localCounter={localCounter}) - skip enter actions");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        // We do not perform any audio actions on exit in this approach.
        localCounter = Mathf.Max(0, localCounter - 1);
        if (debugLogging) Debug.Log($"[AmbianceTrigger:{name}] EXIT (localCounter={localCounter})");
    }

    private void ProcessEnterAudioSettings(AudioSettings[] settingsArray)
    {
        if (settingsArray == null) return;

        foreach (var s in settingsArray)
        {
            switch (s.action)
            {
                case Action.Start:
                    AmbianceManager.Instance.ChangeAmbiance(s.location);
                    if (debugLogging) Debug.Log($"[AmbianceTrigger:{name}] ChangeAmbiance(Start) {s.location}");
                    break;

                case Action.FadeToSilence:
                    AmbianceManager.Instance.FadeOutCurrent();
                    if (debugLogging) Debug.Log($"[AmbianceTrigger:{name}] FadeOutCurrent (FadeToSilence)");
                    break;
            }
        }
    }
}