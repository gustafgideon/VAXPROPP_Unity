using UnityEngine;

/// <summary>
/// Player water effects: plays a one-shot splash and a looping "wade" effect while the player is in water.
/// Improved detection: besides relying on trigger events, this version periodically raycasts downward to detect
/// very shallow water beneath the player (configurable distance and interval). When a nearby water collider is
/// found (by tag or layerMask), it will trigger splash/wade the same as a normal trigger entry.
/// </summary>
public class PlayerWaterSplash : MonoBehaviour
{
    [Header("Splash (one-shot)")]
    [SerializeField] private ParticleSystem splashEffect; // Reference to your splash particle system (one-shot)

    [Header("Wade (looping while in water)")]
    [SerializeField] private ParticleSystem wadeEffect; // Reference to a looping particle system OR a prefab to instantiate
    [Tooltip("If true, wadeEffect will be instantiated at runtime (treat wadeEffect as a prefab). If false, the assigned ParticleSystem in the scene will be moved/played.")]
    [SerializeField] private bool instantiateWadeAsPrefab = false;
    [Tooltip("Vertical offset from the water surface for the wading effect.")]
    [SerializeField] private float wadeSurfaceOffset = 0.05f;

    [Header("Water detection")]
    [SerializeField] private string waterTag = "Water"; // Tag for water objects
    [Tooltip("If the player's collider doesn't actually intersect the water (very shallow water), periodically raycast downward this distance to detect water.")]
    [SerializeField] private float waterDetectionDistance = 0.5f;
    [Tooltip("How often (seconds) to check by raycast for shallow water when not already 'in water'.")]
    [SerializeField] private float detectionCheckInterval = 0.12f;
    [Tooltip("Optional - limit raycast to specific layers for performance (set to Water layer(s) if you use them). Default = Everything.")]
    [SerializeField] private LayerMask waterLayerMask = ~0;

    private bool isInWater = false;
    private Collider currentWaterCollider;
    private ParticleSystem wadeInstance; // either the scene-assigned wadeEffect (when instantiateWadeAsPrefab == false) or an instantiated copy

    // internal: last known surface Y (useful when we detected via raycast)
    private float lastDetectedSurfaceY = float.NegativeInfinity;

    // detection timer
    private float detectionTimer = 0f;

    private void Start()
    {
        // Ensure the splash effect is not playing at start
        if (splashEffect != null)
        {
            splashEffect.Stop();
        }

        // If we are using a scene-assigned wade effect, stop it at start
        if (!instantiateWadeAsPrefab && wadeEffect != null)
        {
            wadeInstance = wadeEffect;
            wadeInstance.Stop();
        }
    }

    private void Update()
    {
        // If we're in water, keep the wade effect positioned at the water surface under the player
        if (isInWater && (currentWaterCollider != null || !float.IsNegativeInfinity(lastDetectedSurfaceY)))
        {
            PositionWadeEffectAtSurface();
            EnsureWadePlaying();
        }

        // If not in water, periodically raycast downward to detect very shallow water beneath the player
        if (!isInWater)
        {
            detectionTimer += Time.deltaTime;
            if (detectionTimer >= detectionCheckInterval)
            {
                detectionTimer = 0f;
                TryDetectShallowWaterBelow();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if we're entering water and not already in it
        if (other.CompareTag(waterTag) && !isInWater)
        {
            currentWaterCollider = other;
            // set last detected surface Y for use when positioning
            lastDetectedSurfaceY = currentWaterCollider.bounds.max.y;
            PlaySplashEffectFromSurfaceY(lastDetectedSurfaceY);
            StartWadeEffect();
            isInWater = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Reset the water state when exiting water
        if (other.CompareTag(waterTag))
        {
            // Only stop if we're leaving the same collider we considered "current"
            if (currentWaterCollider == other)
            {
                StopWadeEffect();
                isInWater = false;
                currentWaterCollider = null;
                lastDetectedSurfaceY = float.NegativeInfinity;
            }
        }
    }

    /// <summary>
    /// Attempts to detect shallow water below the player using a downward raycast.
    /// If a collider with the waterTag is hit within waterDetectionDistance, treat it as an "enter".
    /// </summary>
    private void TryDetectShallowWaterBelow()
    {
        RaycastHit hit;
        Vector3 origin = transform.position;
        // Cast downward from the player's position
        if (Physics.Raycast(origin, Vector3.down, out hit, waterDetectionDistance, waterLayerMask, QueryTriggerInteraction.Collide))
        {
            Collider hitCol = hit.collider;
            if (hitCol != null && hitCol.CompareTag(waterTag))
            {
                // If we already are "in water" with a different collider, ignore. Otherwise treat as entering.
                if (!isInWater)
                {
                    currentWaterCollider = hitCol;
                    lastDetectedSurfaceY = hit.point.y;
                    PlaySplashEffectFromSurfaceY(lastDetectedSurfaceY);
                    StartWadeEffect();
                    isInWater = true;
                }
            }
        }
    }

    /// <summary>
    /// Play splash using surface Y coordinate (useful for raycast detection).
    /// </summary>
    private void PlaySplashEffectFromSurfaceY(float surfaceY)
    {
        if (splashEffect != null)
        {
            Vector3 splashPosition = new Vector3(transform.position.x, surfaceY, transform.position.z);
            splashEffect.transform.position = splashPosition;
            splashEffect.Play();
        }
        else
        {
            Debug.LogWarning("Splash effect not assigned to PlayerWaterEffects script!");
        }
    }

    /// <summary>
    /// (Legacy) Play splash using water collider bounds (keeps compatibility with trigger-based detection).
    /// </summary>
    private void PlaySplashEffect(Collider waterCollider)
    {
        if (waterCollider != null)
        {
            float surfaceY = waterCollider.bounds.max.y;
            PlaySplashEffectFromSurfaceY(surfaceY);
        }
    }

    private void StartWadeEffect()
    {
        if (wadeEffect == null)
        {
            // No wading effect assigned - nothing to start
            return;
        }

        // Determine initial position based on current water collider or last detected surface Y if available
        Vector3 startPos = transform.position;
        if (currentWaterCollider != null)
        {
            startPos.y = currentWaterCollider.bounds.max.y + wadeSurfaceOffset;
        }
        else if (!float.IsNegativeInfinity(lastDetectedSurfaceY))
        {
            startPos.y = lastDetectedSurfaceY + wadeSurfaceOffset;
        }

        if (instantiateWadeAsPrefab)
        {
            // Instantiate a copy of the prefab and keep reference to it
            if (wadeInstance == null)
            {
                wadeInstance = Instantiate(wadeEffect, startPos, Quaternion.identity);
                // Ensure loop is enabled for the instance
                var main = wadeInstance.main;
                main.loop = true;
            }
            else
            {
                wadeInstance.transform.position = startPos;
            }

            wadeInstance.Play();
        }
        else
        {
            // Use the scene-assigned particle system
            wadeInstance = wadeEffect;
            wadeInstance.transform.position = startPos;

            // Ensure loop is enabled
            var main = wadeInstance.main;
            main.loop = true;

            wadeInstance.Play();
        }
    }

    private void PositionWadeEffectAtSurface()
    {
        if (wadeInstance == null) return;

        float surfaceY = float.NaN;
        if (currentWaterCollider != null)
        {
            surfaceY = currentWaterCollider.bounds.max.y;
        }
        else if (!float.IsNegativeInfinity(lastDetectedSurfaceY))
        {
            surfaceY = lastDetectedSurfaceY;
        }

        if (float.IsNaN(surfaceY)) return;

        Vector3 targetPos = new Vector3(
            transform.position.x,
            surfaceY + wadeSurfaceOffset,
            transform.position.z
        );

        wadeInstance.transform.position = targetPos;
    }

    private void EnsureWadePlaying()
    {
        if (wadeInstance == null) return;
        if (!wadeInstance.isPlaying)
        {
            wadeInstance.Play();
        }
    }

    private void StopWadeEffect()
    {
        if (wadeInstance == null) return;

        if (instantiateWadeAsPrefab)
        {
            // If we instantiated it, destroy the instance to clean up
            Destroy(wadeInstance.gameObject);
            wadeInstance = null;
        }
        else
        {
            // If using a scene-assigned particle system, just stop it
            wadeInstance.Stop();
        }
    }

    private void OnDisable()
    {
        // Ensure effects are stopped/cleaned up if the component is disabled
        StopWadeEffect();

        if (splashEffect != null)
        {
            splashEffect.Stop();
        }
    }

    private void OnDestroy()
    {
        // Same cleanup on destroy
        StopWadeEffect();
    }

    // Optional: visual debug in editor to show raycast distance
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * waterDetectionDistance);
    }
#endif
}