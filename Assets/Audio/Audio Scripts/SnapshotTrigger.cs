using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using System.Collections.Generic;

public class SnapshotTrigger : MonoBehaviour
{
    public enum SnapshotType
    {
        ReverbForest,
        ReverbDesert,
        ReverbPlant,
    }

    [Tooltip("Vilka snapshots som ska triggas när spelaren går in i triggern")]
    public List<SnapshotType> snapshotsToTrigger;

    private List<EventInstance> activeSnapshots = new List<EventInstance>();

    private Dictionary<SnapshotType, string> snapshotMap = new Dictionary<SnapshotType, string>
    {
        { SnapshotType.ReverbForest, "snapshot:/Reverb_Forest" },
        { SnapshotType.ReverbDesert, "snapshot:/Reverb_Desert" },
        { SnapshotType.ReverbPlant, "snapshot:/Reverb_Plant" },
        
    };

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            foreach (SnapshotType snapshot in snapshotsToTrigger)
            {
                if (snapshotMap.TryGetValue(snapshot, out string path))
                {
                    EventInstance instance = RuntimeManager.CreateInstance(path);
                    instance.start();
                    activeSnapshots.Add(instance);
                }
                else
                {
                    Debug.LogWarning($"Snapshot path not found for {snapshot}");
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            foreach (EventInstance snapshot in activeSnapshots)
            {
                snapshot.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                snapshot.release();
            }
            activeSnapshots.Clear();
        }
    }
}