using UnityEngine;

public class PlayerMovementAudio : MonoBehaviour
{
    [Header("References")]
    public PlayerAudio playerAudio;        // ScriptableObject
    public GameObject playerFoot;
    public GameObject playerHand;
    public float rayDistance = 1f;         // Raycast distance
    public LayerMask surfaceLayerMask;     // Include terrain and tagged objects

    // Called by Animation Event
    public void PlayerWalkAudio()
    {
        // Detect the surface first
        string currentSurface = DetectSurface();

        // Log the surface that will actually be used
        Debug.Log("Surface detected: " + currentSurface);

        // Call the ScriptableObject function to play the footstep
        playerAudio.PlayerWalkAudio(playerFoot, currentSurface);
    }


    private string DetectSurface()
    {
        string surface = "Player"; // fallback

        if (playerFoot == null) return surface;

        // Single raycast down from the foot
        if (Physics.Raycast(playerFoot.transform.position, Vector3.down, out RaycastHit hit, rayDistance, surfaceLayerMask))
        {
            // Priority 1: Check for tags
            switch (hit.collider.tag)
            {
                case "Grass": return "Grass";
                case "Dirt": return "Dirt";
                case "Wood": return "Wood";
            }

            // Priority 2: Check if it’s terrain
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

                // Map terrain layer index to surface name
                switch (dominantLayer)
                {
                    case 0: return "Grass";
                    case 1: return "Dirt";
                    case 2: return "Wood";
                    default: return "Concrete";
                }
            }
        }

        // If nothing hit or no matching tag/terrain
        return surface;
    }

    // Visualize the raycast in the Scene view
    private void OnDrawGizmos()
    {
        if (playerFoot != null)
        {
            Gizmos.color = Color.green;
            Vector3 origin = playerFoot.transform.position;
            Gizmos.DrawLine(origin, origin + Vector3.down * rayDistance);
            Gizmos.DrawSphere(origin + Vector3.down * rayDistance, 0.1f);
        }
    }
}
