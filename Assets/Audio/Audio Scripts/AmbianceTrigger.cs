using System;
using UnityEngine;

/// <summary>
/// Simplified AmbianceTrigger: only Start and Stop actions remain.
/// Start now calls ChangeAmbiance(...) so fades/crossfades are used.
/// </summary>
public class AmbianceTrigger : MonoBehaviour
{
    public enum Action
    {
        Start, // Start ambiance event (location) — now uses ChangeAmbiance (fades)
        Stop   // Stop ambiance event (location)
    }

    [Serializable]
    public struct AudioSettings
    {
        public Action action;
        public Location location; // Used for Start / Stop
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

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        localCounter++;

        // Execute enter actions only on the FIRST effective entry (localCounter == 1)
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
                    // Use ChangeAmbiance to trigger fades/crossfades
                    AmbianceManager.Instance.ChangeAmbiance(s.location);
                    if (debugLogging) Debug.Log($"[AmbianceTrigger:{name}] ChangeAmbiance(Start) {s.location} (entering={entering})");
                    break;

                case Action.Stop:
                    // Stop is immediate by design; see note below if you want a fade-to-silence
                    AmbianceManager.Instance.StopAudio(s.location);
                    if (debugLogging) Debug.Log($"[AmbianceTrigger:{name}] Stop {s.location} (entering={entering})");
                    break;
            }
        }
    }
}