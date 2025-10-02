using UnityEngine;

public class PlayerMovementAudio : MonoBehaviour
{
    [Header("References")]
    public PlayerAudio playerAudio;          // Assign your ScriptableObject
    public GameObject playerFeet;
    public float rayDistance = 1.5f;         // Distance to check below the player
    public LayerMask surfaceLayerMask;       // Assign your dedicated Terrain layer here

    // ----------------------
    // Call this from Animation Event
    // ----------------------
    public void PlayerWalkAudio()
    {
        string currentSurface = DetectTerrainLayer();
        playerAudio.PlayerWalkAudio(gameObject, currentSurface);
    }

    // ----------------------
    // Terrain layer detection
    // ----------------------
    // In PlayerWalkFootstep
    private string DetectTerrainLayer()
    {
        string surface = "Player";
        
        RaycastHit hit;

        if (Physics.Raycast(transform.position, Vector3.down, out hit, rayDistance, surfaceLayerMask))
        {
            Terrain terrain = hit.collider.GetComponent<Terrain>();
            if (terrain != null)
            {
                TerrainData terrainData = terrain.terrainData;
                Vector3 terrainPos = hit.point - terrain.transform.position;

                int mapX = Mathf.FloorToInt((terrainPos.x / terrainData.size.x) * terrainData.alphamapWidth);
                int mapZ = Mathf.FloorToInt((terrainPos.z / terrainData.size.z) * terrainData.alphamapHeight);

                float[,,] splatmap = terrainData.GetAlphamaps(mapX, mapZ, 1, 1);

                float maxWeight = 0f;
                int dominantLayer = 0;

                for (int i = 0; i < splatmap.GetLength(2); i++)
                {
                    if (splatmap[0, 0, i] > maxWeight)
                    {
                        maxWeight = splatmap[0, 0, i];
                        dominantLayer = i;
                    }
                }

                // Just return the name for this terrain layer
                switch (dominantLayer)
                {
                    case 0: surface = "Grass"; break;
                    case 1: surface = "Dirt"; break;
                    case 2: surface = "Sand"; break;
                    default: surface = "Concrete"; break;
                }
            }
        }

        return surface;
    }
    
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * rayDistance);
        Gizmos.DrawSphere(transform.position + Vector3.down * rayDistance, 0.1f);
    }
}
