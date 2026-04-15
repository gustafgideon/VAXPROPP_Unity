using UnityEngine;

public class PlayerMovementAudio : MonoBehaviour
{
    [Header("References")]
    public PlayerAudio playerAudio;
    public GameObject playerFoot;
    public float rayDistance = 1f;
    public LayerMask surfaceLayerMask; // Include terrain, props, stairs

    [Header("Footstep Guard (Option A)")]
    [Tooltip("Animator that drives the locomotion blend tree")]
    public Animator animator;
    [Tooltip("Animator layer index to check clip weights on (usually 0)")]
    public int animEventLayer = 0;
    [Tooltip("Minimum time (s) between accepted footstep events to avoid double-play from blended clips")]
    public float footstepMinInterval = 0.12f;

    private Vector3 lastFootPosition;
    private float lastFootstepTime = -10f;

    private void Start()
    {
        if (playerFoot != null)
            lastFootPosition = playerFoot.transform.position;
    }

    // Called directly from Animation Event (no change required to your existing events)
    // Keep these names if your events already point to them.
    public void PlayerWalkAudio()
    {
        if (playerFoot == null) return;

        if (!ShouldPlayFootstep("walk")) return;

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
        playerAudio?.PlayerWalkAudio(playerFoot, surface, stairDirection);
    }
    
    public void PlayerRunAudio()
    {
        if (playerFoot == null) return;

        if (!ShouldPlayFootstep("run")) return;

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
        playerAudio?.PlayerRunAudio(playerFoot, surface, stairDirection);
    }

    // Core guard: checks dominant clip and cooldown.
    // type should be "walk" or "run" (case-insensitive)
    private bool ShouldPlayFootstep(string type)
    {
        // Cooldown first (quick reject)
        if (Time.time - lastFootstepTime < footstepMinInterval)
            return false;

        // If no animator assigned, fallback to cooldown-only behavior (prevents double-play in many cases)
        if (animator == null)
        {
            lastFootstepTime = Time.time;
            return true;
        }

        // Find dominant clip name across current and next state (handles transitions)
        string dominant = GetDominantClipName(animEventLayer);
        if (string.IsNullOrEmpty(dominant))
        {
            // No clip info available -> allow, but set cooldown
            lastFootstepTime = Time.time;
            return true;
        }

        string lower = dominant.ToLowerInvariant();
        string want = (type ?? "").ToLowerInvariant();

        // Decide allowed type by clip name heuristics:
        // - If dominant clip name contains "run" or "sprint" -> allow run only
        // - If dominant clip name contains "walk" or "step" -> allow walk only
        // - Otherwise, if the clip name doesn't clearly indicate, allow only if clip name matches the invoked type
        bool dominantIsRun = lower.Contains("run") || lower.Contains("sprint");
        bool dominantIsWalk = lower.Contains("walk") || lower.Contains("step");

        bool accept = false;
        if (dominantIsRun && want == "run") accept = true;
        else if (dominantIsWalk && want == "walk") accept = true;
        else if (!dominantIsRun && !dominantIsWalk)
        {
            // Fallback: if the clip name itself contains the type string, accept,
            // otherwise only accept if the clip exactly matches the type hint.
            if (lower.Contains(want)) accept = true;
            else accept = false;
        }

        if (!accept) return false;

        // Passed checks -> set cooldown and accept
        lastFootstepTime = Time.time;
        return true;
    }

    // Get the dominant clip name on a layer (checks current and next state's clip infos)
    private string GetDominantClipName(int layer)
    {
        if (animator == null) return null;

        float maxW = 0f;
        string best = null;

        AnimatorClipInfo[] current = animator.GetCurrentAnimatorClipInfo(layer);
        for (int i = 0; i < current.Length; i++)
        {
            var ci = current[i];
            if (ci.clip == null) continue;
            if (ci.weight > maxW)
            {
                maxW = ci.weight;
                best = ci.clip.name;
            }
        }

        if (animator.IsInTransition(layer))
        {
            AnimatorClipInfo[] next = animator.GetNextAnimatorClipInfo(layer);
            for (int i = 0; i < next.Length; i++)
            {
                var ni = next[i];
                if (ni.clip == null) continue;
                if (ni.weight > maxW)
                {
                    maxW = ni.weight;
                    best = ni.clip.name;
                }
            }
        }

        return best;
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
                case "Road": return "Road";
                case "Sand": return "Sand";
                case "Pavement": return "Pavement";
                case "WaterPuddle": return "WaterPuddle";
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

                // Clamp to valid range to avoid exceptions on edges
                mapX = Mathf.Clamp(mapX, 0, data.alphamapWidth - 1);
                mapZ = Mathf.Clamp(mapZ, 0, data.alphamapHeight - 1);

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
                    case 3: return "Road";
                    case 4: return "Sand";
                    case 5: return "Pavement";
                    case 6: return "WaterPuddle";
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
