using UnityEngine;
using UnityEngine.VFX;

public class RandomAudioSpwaner : MonoBehaviour
{
    public GameObject soundPrefab;
    public Transform[] spawnPoints;
    public float spawnInterval = 0f;

    private void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    private System.Collections.IEnumerator SpawnLoop()
    {
        while (true)
        {
            SpawnAtRandom();
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    public void SpawnAtIndex(int index)
    {
        if (index >= 0 && index < spawnPoints.Length)
        {
            Instantiate(soundPrefab, spawnPoints[index].position, Quaternion.identity);
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
