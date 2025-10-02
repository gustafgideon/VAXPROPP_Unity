using UnityEngine;

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

    [SerializeField] private string waterTag = "Water"; // Tag for water objects

    private bool isInWater = false;
    private Collider currentWaterCollider;
    private ParticleSystem wadeInstance; // either the scene-assigned wadeEffect (when instantiateWadeAsPrefab == false) or an instantiated copy

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
        if (isInWater && currentWaterCollider != null)
        {
            PositionWadeEffectAtSurface();
            EnsureWadePlaying();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if we're entering water and not already in it
        if (other.CompareTag(waterTag) && !isInWater)
        {
            currentWaterCollider = other;
            PlaySplashEffect(other);
            StartWadeEffect();
            isInWater = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Reset the water state when exiting water
        if (other.CompareTag(waterTag))
        {
            StopWadeEffect();
            isInWater = false;
            currentWaterCollider = null;
        }
    }

    private void PlaySplashEffect(Collider waterCollider)
    {
        if (splashEffect != null)
        {
            // Calculate the splash position at the water's surface
            Vector3 splashPosition = new Vector3(
                transform.position.x,
                waterCollider.bounds.max.y,
                transform.position.z
            );

            // Position the splash effect and play it
            splashEffect.transform.position = splashPosition;
            splashEffect.Play();
        }
        else
        {
            Debug.LogWarning("Splash effect not assigned to PlayerWaterSplash script!");
        }
    }

    private void StartWadeEffect()
    {
        if (wadeEffect == null)
        {
            // No wading effect assigned - nothing to start
            return;
        }

        // Determine initial position based on current water collider if available
        Vector3 startPos = transform.position;
        if (currentWaterCollider != null)
        {
            startPos.y = currentWaterCollider.bounds.max.y + wadeSurfaceOffset;
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
        if (wadeInstance == null || currentWaterCollider == null) return;

        Vector3 targetPos = new Vector3(
            transform.position.x,
            currentWaterCollider.bounds.max.y + wadeSurfaceOffset,
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
}