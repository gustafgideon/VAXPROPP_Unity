using UnityEngine;

public class RainController : MonoBehaviour
{
    [Header("Rain Intensity (0 = Off, 1 = Heavy Rain)")]
    [Range(0f, 1f)]
    public float rainIntensity = 0.5f;
    
    [Header("Particle System")]
    public ParticleSystem rainParticles;
    
    [Header("Intensity Settings")]
    public float minRainRate = 100f;
    public float maxRainRate = 800f;
    
    [Header("Particle Size Settings")]
    [Range(0.1f, 2f)]
    public float particleSize = 1f;
    [Range(0f, 0.5f)]
    public float sizeVariation = 0.2f;
    
    [Header("Current Settings (Read Only)")]
    [SerializeField] private Vector3 currentZoneSize;
    [SerializeField] private string status = "Configure zone in ParticleSystem Shape module";
    
    private float lastIntensity = -1f;
    private float lastParticleSize = -1f;
    private float lastSizeVariation = -1f;
    
    void Start()
    {
        if (rainParticles == null)
            rainParticles = GetComponentInChildren<ParticleSystem>();
        
        if (rainParticles == null)
        {
            Debug.LogError("No ParticleSystem found!");
            return;
        }
        
        // Reset any weird scaling we might have done
        rainParticles.transform.localScale = Vector3.one;
        
        // Read current settings
        ReadCurrentSettings();
        
        // Only control what we can safely control
        UpdateParticleSize();
        UpdateRain();
    }
    
    void ReadCurrentSettings()
    {
        var shape = rainParticles.shape;
        currentZoneSize = shape.scale;
        
        // Make sure basic settings are correct
        var main = rainParticles.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 5000;
        
        var emission = rainParticles.emission;
        emission.enabled = true;
        
        status = "Zone: " + currentZoneSize.x + "x" + currentZoneSize.z + " (Set manually in ParticleSystem)";
        
        Debug.Log("Rain system ready. Zone size: " + currentZoneSize);
    }
    
    void Update()
    {
        // Only update what we can safely change
        if (rainIntensity != lastIntensity)
        {
            UpdateRain();
            lastIntensity = rainIntensity;
        }
        
        if (particleSize != lastParticleSize || sizeVariation != lastSizeVariation)
        {
            UpdateParticleSize();
            lastParticleSize = particleSize;
            lastSizeVariation = sizeVariation;
        }
        
        // Check if someone changed the ParticleSystem manually
        var shape = rainParticles.shape;
        if (shape.scale != currentZoneSize)
        {
            currentZoneSize = shape.scale;
            status = "Zone: " + currentZoneSize.x + "x" + currentZoneSize.z + " (Set manually in ParticleSystem)";
        }
    }
    
    void UpdateRain()
    {
        if (rainParticles == null) return;
        
        var emission = rainParticles.emission;
        
        if (rainIntensity > 0f)
        {
            float targetRate = Mathf.Lerp(minRainRate, maxRainRate, rainIntensity);
            emission.rateOverTime = targetRate;
            
            if (!rainParticles.isPlaying)
            {
                rainParticles.Play();
            }
        }
        else
        {
            emission.rateOverTime = 0f;
            if (rainParticles.isPlaying)
            {
                rainParticles.Stop();
            }
        }
    }
    
    void UpdateParticleSize()
    {
        if (rainParticles == null) return;
        
        var main = rainParticles.main;
        
        if (sizeVariation > 0f)
        {
            float minSize = Mathf.Max(particleSize - sizeVariation, 0.1f);
            float maxSize = particleSize + sizeVariation;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        }
        else
        {
            main.startSize = particleSize;
        }
    }
    
    // NO MORE OnDrawGizmosSelected() - Clean parent object!
}