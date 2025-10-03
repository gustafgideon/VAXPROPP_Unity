using UnityEngine;

public class RandomAudioSpawner : MonoBehaviour
{
    public GameObject audioPrefab;
    public Transform[] spawnPoints;
    
    [Header("Distance Variation")]
    public float maxDistanceVariation = 2f;
    
    [Header("Timing Variation")]
    public float minInterval = 1f; // Minimum time between spawns
    public float maxInterval = 5f; // Maximum time between spawns
    
    private void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    private System.Collections.IEnumerator SpawnLoop()
    {
        while (true)
        {
            SpawnAtRandom();
            
            // Random interval between min and max
            float randomInterval = Random.Range(minInterval, maxInterval);
            yield return new WaitForSeconds(randomInterval);
        }
    }

    public void SpawnAtIndex(int index)
    {
        if (index >= 0 && index < spawnPoints.Length)
        {
            // Get the original spawn point position
            Vector3 originalPosition = spawnPoints[index].position;
            
            // Add random variation
            Vector3 randomOffset = Random.insideUnitSphere * maxDistanceVariation;
            Vector3 variedPosition = originalPosition + randomOffset;
            
            Instantiate(audioPrefab, variedPosition, Quaternion.identity);
        }
        else
        {
            Debug.LogWarning("spawn index out of range!");
        }
    }

    public void SpawnAtRandom()
    {
        int randomIndex = Random.Range(0, spawnPoints.Length);
        SpawnAtIndex(randomIndex);
    }
}