using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using UnityEngine.Events;

[System.Serializable]
public class BreakableSettings
{
    [Header("Material Presets")]
    public MaterialType materialType = MaterialType.Custom;
    
    [Header("Custom Settings")]
    public float maxHealth = 100f;
    public float breakThreshold = 15f;      // Higher = harder to break instantly
    public float damageMultiplier = 10f;
    public float minDamageThreshold = 2f;   // Minimum impact to cause any damage
    
    public enum MaterialType
    {
        Custom,
        Glass,          // Fragile, breaks easily
        WoodenCrate,    // Medium durability
        MetalContainer, // Very durable
        Ceramic         // Fragile but harder than glass
    }
    
    public void ApplyPreset()
    {
        switch (materialType)
        {
            case MaterialType.Glass:
                maxHealth = 30f;
                breakThreshold = 6f;
                damageMultiplier = 20f;
                minDamageThreshold = 1f;
                break;
                
            case MaterialType.WoodenCrate:
                maxHealth = 300f;
                breakThreshold = 20f;      // Won't break from normal drops
                damageMultiplier = 1f;
                minDamageThreshold = 3f;   // Needs decent impact to damage
                break;
                
            case MaterialType.MetalContainer:
                maxHealth = 200f;
                breakThreshold = 25f;
                damageMultiplier = 5f;
                minDamageThreshold = 5f;
                break;
                
            case MaterialType.Ceramic:
                maxHealth = 50f;
                breakThreshold = 8f;
                damageMultiplier = 15f;
                minDamageThreshold = 1.5f;
                break;
        }
    }
}

[RequireComponent(typeof(Rigidbody))]
public class PhysicsAudioBehaviour : MonoBehaviour
{
    [Header("FMOD Events")]
    [SerializeField] private EventReference impactEvent;
    [SerializeField] private EventReference breakEvent;

    [Header("Impact Settings")]
    public float minImpactSpeed = 0.1f;
    public float maxImpactSpeed = 10f;
    public float impactCooldown = 0.05f;
    public float smoothing = 0.8f;
    public float massMultiplier = 0.5f;

    [Header("Breakable Settings")]
    public bool isBreakable = false;
    [SerializeField] private BreakableSettings breakableSettings = new BreakableSettings();
    [SerializeField] private float currentHealth;
    [SerializeField] private bool showHealthInInspector = true;

    [Header("Break Effects")]
    public GameObject brokenPrefab;
    public UnityEvent OnBreak;
    public UnityEvent<float> OnDamage;

    private Rigidbody rb;
    private float lastImpactTime;
    private float smoothedVelocity;
    private bool isBroken = false;

    // Properties for external access
    public float Health => currentHealth;
    public float MaxHealth => breakableSettings.maxHealth;
    public float HealthPercentage => breakableSettings.maxHealth > 0 ? currentHealth / breakableSettings.maxHealth : 0f;
    public bool IsBroken => isBroken;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        smoothedVelocity = 0f;
        
        if (isBreakable)
        {
            // Apply material preset if not custom
            if (breakableSettings.materialType != BreakableSettings.MaterialType.Custom)
            {
                breakableSettings.ApplyPreset();
            }
            currentHealth = breakableSettings.maxHealth;
        }
    }

    void Update()
    {
        float currentSpeed = rb.linearVelocity.magnitude;
        smoothedVelocity = Mathf.Lerp(smoothedVelocity, currentSpeed, 1 - smoothing);
    }

    void OnCollisionEnter(Collision collision)
    {
        TryPlayImpact(collision.relativeVelocity.magnitude);
    }

    void OnCollisionStay(Collision collision)
    {
        TryPlayImpact(collision.relativeVelocity.magnitude);
    }

    private void TryPlayImpact(float collisionSpeed)
    {
        if (isBroken) return;
        if (Time.time - lastImpactTime < impactCooldown) return;

        float impactStrength = Mathf.Max(collisionSpeed, smoothedVelocity) * (1f + rb.mass * massMultiplier);

        if (impactStrength >= minImpactSpeed)
        {
            lastImpactTime = Time.time;

            if (isBreakable && !isBroken)
            {
                ProcessBreakableDamage(impactStrength);
            }

            if (!isBroken)
            {
                PlayImpactSound(impactStrength);
            }
        }
    }

    private void PlayImpactSound(float impactStrength)
    {
        var impactInstance = RuntimeManager.CreateInstance(impactEvent);
        RuntimeManager.AttachInstanceToGameObject(impactInstance, transform, rb);

        float normalized = Mathf.Clamp01((impactStrength - minImpactSpeed) / (maxImpactSpeed - minImpactSpeed));
        impactInstance.setParameterByName("Impact", normalized);

        impactInstance.start();
        impactInstance.release();
    }

    private void ProcessBreakableDamage(float impactStrength)
    {
        // Only process damage if impact is above minimum threshold
        if (impactStrength < breakableSettings.minDamageThreshold) return;

        // Check for instant break (very high impact)
        if (impactStrength >= breakableSettings.breakThreshold)
        {
            Debug.Log($"Instant break! Impact: {impactStrength:F2}, Threshold: {breakableSettings.breakThreshold:F2}");
            BreakObject();
            return;
        }

        // Calculate damage based on impact strength
        float damage = (impactStrength - breakableSettings.minDamageThreshold) * breakableSettings.damageMultiplier;
        TakeDamage(damage);
    }

    public void TakeDamage(float damage)
    {
        if (!isBreakable || isBroken) return;

        float previousHealth = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - damage);
        
        Debug.Log($"Damage taken: {damage:F2}, Health: {previousHealth:F2} -> {currentHealth:F2}");
        
        OnDamage?.Invoke(HealthPercentage);

        if (currentHealth <= 0)
        {
            Debug.Log("Health depleted - breaking object");
            BreakObject();
        }
    }

    public void BreakObject()
    {
        if (!isBreakable || isBroken) return;

        isBroken = true;
        currentHealth = 0;

        Debug.Log("Object broken!");

        if (!breakEvent.IsNull)
        {
            var breakInstance = RuntimeManager.CreateInstance(breakEvent);
            RuntimeManager.AttachInstanceToGameObject(breakInstance, transform, rb);
            breakInstance.start();
            breakInstance.release();
        }

        OnBreak?.Invoke();

        if (brokenPrefab != null)
        {
            GameObject broken = Instantiate(brokenPrefab, transform.position, transform.rotation);
            Rigidbody[] brokenRbs = broken.GetComponentsInChildren<Rigidbody>();
            foreach (var brokenRb in brokenRbs)
            {
                brokenRb.linearVelocity = rb.linearVelocity;
                brokenRb.angularVelocity = rb.angularVelocity;
            }
        }

        gameObject.SetActive(false);
    }

    // Rest of the methods remain the same...
    public void SetHealth(float health)
    {
        if (!isBreakable) return;
        currentHealth = Mathf.Clamp(health, 0, breakableSettings.maxHealth);
        if (currentHealth <= 0)
        {
            BreakObject();
        }
    }

    public void Heal(float healAmount)
    {
        if (!isBreakable || isBroken) return;
        currentHealth = Mathf.Min(breakableSettings.maxHealth, currentHealth + healAmount);
    }

    public void ResetHealth()
    {
        if (!isBreakable) return;
        currentHealth = breakableSettings.maxHealth;
        isBroken = false;
        gameObject.SetActive(true);
    }

    void OnValidate()
    {
        if (isBreakable)
        {
            // Apply preset when changed in inspector
            if (breakableSettings.materialType != BreakableSettings.MaterialType.Custom)
            {
                breakableSettings.ApplyPreset();
            }
            currentHealth = Mathf.Clamp(currentHealth, 0, breakableSettings.maxHealth);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (isBreakable && Application.isPlaying)
        {
            Vector3 pos = transform.position + Vector3.up * 2f;
            float barWidth = 2f;
            float barHeight = 0.2f;
            
            Gizmos.color = Color.red;
            Gizmos.DrawCube(pos, new Vector3(barWidth, barHeight, 0.1f));
            
            Gizmos.color = Color.green;
            float healthWidth = barWidth * HealthPercentage;
            Vector3 healthPos = pos - Vector3.right * (barWidth - healthWidth) * 0.5f;
            Gizmos.DrawCube(healthPos, new Vector3(healthWidth, barHeight, 0.1f));
        }
    }
#endif
}