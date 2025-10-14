using UnityEngine;

public class PlayerMovementAudio : MonoBehaviour
{
    [Header("References")]
    public PlayerAudio playerAudio;
    public GameObject playerFoot;
    public float rayDistance = 1f;
    public LayerMask surfaceLayerMask; // Include terrain, props, stairs

    private Vector3 lastFootPosition;

    private void Start()
    {
        if (playerFoot != null)
            lastFootPosition = playerFoot.transform.position;
    }

    // Called from Animation Event
    public void PlayerWalkAudio()
    {
        if (playerFoot == null) return;

        string surface = DetectSurface(); // returns e.g. "Grass", "Concrete_Stair", "Wood"

        // Stair direction detection
        float stairDirection = 0f; // Up = 0, Down = 1
        if (surface.Contains("Stair"))
        {
            float deltaY = playerFoot.transform.position.y - lastFootPosition.y;
            stairDirection = (deltaY < -0.01f) ? 1f : 0f; // Down if foot moved downward
        }

        lastFootPosition = playerFoot.transform.position;

        // Call FMOD via ScriptableObject
        playerAudio.PlayerWalkAudio(playerFoot, surface, stairDirection);
    }
    
    public void PlayerRunAudio()
    {
        if (playerFoot == null) return;

        string surface = DetectSurface(); // returns e.g. "Grass", "Concrete_Stair", "Wood"

        // Stair direction detection
        float stairDirection = 0f; // Up = 0, Down = 1
        if (surface.Contains("Stair"))
        {
            float deltaY = playerFoot.transform.position.y - lastFootPosition.y;
            stairDirection = (deltaY < -0.01f) ? 1f : 0f; // Down if foot moved downward
        }

        lastFootPosition = playerFoot.transform.position;

        // Call FMOD via ScriptableObject
        playerAudio.PlayerRunAudio(playerFoot, surface, stairDirection);
    }

    // Raycast to detect surface
    private string DetectSurface()
    {
        if (playerFoot == null) return "Player";

        if (Physics.Raycast(playerFoot.transform.position, Vector3.down, out RaycastHit hit, rayDistance, surfaceLayerMask))
        {
            // Priority 1: Tags (props, stairs)
            switch(hit.collider.tag)
            {
                case "Grass": return "Grass";
                case "Dirt": return "Dirt";
                case "Wood": return "Wood";
                case "Concrete": return "Concrete";
                case "Sand": return "Sand";
                case"Stone": return "Stone";
                case "Concrete_Stair": return "Concrete_Stair";
                case "Wood_Stair": return "Wood_Stair";
            }

            // Priority 2: Terrain
            Terrain terrain = hit.collider.GetComponent<Terrain>();
            if (terrain != null)
            {
                TerrainData data = terrain.terrainData;
                Vector3 pos = hit.point - terrain.transform.position;

                int mapX = Mathf.FloorToInt((pos.x / data.size.x) * data.alphamapWidth);
                int mapZ = Mathf.FloorToInt((pos.z / data.size.z) * data.alphamapHeight);

                float[,,] splat = data.GetAlphamaps(mapX, mapZ, 1, 1);
                int dominant = 0;
                float maxWeight = 0f;

                for (int i = 0; i < splat.GetLength(2); i++)
                {
                    if (splat[0, 0, i] > maxWeight)
                    {
                        maxWeight = splat[0, 0, i];
                        dominant = i;
                    }
                }

                switch(dominant)
                {
                    case 0: return "Grass";
                    case 1: return "Dirt";
                    case 2: return "Wood";
                    case 3: return "Concrete";
                    case 4: return "Sand";
                    default: return "Player";
                }
            }
        }

        return "Player";
    }

    // Visualize raycast
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
