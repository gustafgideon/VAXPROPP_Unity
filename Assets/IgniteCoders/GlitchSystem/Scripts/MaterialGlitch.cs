using System.Collections;
using UnityEngine;

[RequireComponent(typeof(TerminalSlamGlitch))]
public class MaterialGlitch : MonoBehaviour
{
    [Header("Debug")]
    public bool enableDebugLogs = false; // Reduced default logging
    
    [Header("Cartoonish Glitch Effects")]
    [Tooltip("Enable animated UV scrolling/distortion")]
    public bool enableUVAnimation = true;
    
    [Tooltip("Enable color shifting/flickering")]
    public bool enableColorGlitch = true;
    
    [Tooltip("Enable emission pulsing")]
    public bool enableEmissionPulse = true;
    
    [Tooltip("Enable scale/offset jittering")]
    public bool enableTextureJitter = true;
    
    [Tooltip("Enable matrix-style glitch lines")]
    public bool enableGlitchLines = true;

    [Header("UV Animation Settings")]
    [Range(0.1f, 20f)] public float uvScrollSpeed = 5f;
    [Range(0f, 5f)] public float uvDistortionAmount = 2f;
    [Range(0.1f, 10f)] public float distortionFrequency = 3f;

    [Header("Color Glitch Settings")]
    public Color[] glitchColors = { Color.red, Color.cyan, Color.magenta, Color.yellow, Color.green };
    [Range(0.1f, 10f)] public float colorFlickerSpeed = 3f;
    [Range(0f, 1f)] public float colorIntensity = 1f;
    public bool useRandomColors = true;

    [Header("Emission Settings")]
    [Range(0f, 10f)] public float maxEmissionIntensity = 5f;
    [Range(0.1f, 10f)] public float emissionPulseSpeed = 4f;
    public Color emissionColor = Color.white;

    [Header("Texture Jitter Settings")]
    [Range(0f, 10f)] public float maxTextureOffset = 2f;
    [Range(0f, 5f)] public float maxTextureScale = 3f;
    [Range(0.1f, 20f)] public float jitterSpeed = 8f;

    [Header("Glitch Lines Settings")]
    [Range(0.1f, 10f)] public float lineScrollSpeed = 3f;
    [Range(0.001f, 0.2f)] public float lineThickness = 0.05f;
    [Range(0f, 1f)] public float lineIntensity = 0.8f;
    [Range(1f, 50f)] public float lineFrequency = 15f;
    public Color lineColor = Color.green;
    [Range(0f, 1f)] public float lineRandomness = 0.7f;

    [Header("Cartoon Style Settings")]
    [Tooltip("Makes effects more abrupt and less smooth")]
    public bool useSteppedAnimation = true;
    [Range(2, 20)] public int animationSteps = 4;
    
    [Tooltip("Multiply all effect intensities when glitching is active")]
    [Range(0.1f, 5f)] public float glitchIntensityMultiplier = 2f;

    [Header("Extreme Effects")]
    [Tooltip("Enable extreme color overrides")]
    public bool enableExtremeEffects = true;
    [Range(0f, 1f)] public float extremeEffectChance = 0.3f;

    [Header("Material Control Mode")]
    [Tooltip("How this script is controlled")]
    public ControlMode controlMode = ControlMode.SlaveToTerminalGlitch;
    
    public enum ControlMode
    {
        SlaveToTerminalGlitch, // Controlled by TerminalSlamGlitch (recommended)
        Independent,           // Original behavior with distance checking
        ForceAlwaysOn          // Always animate (for testing)
    }

    [Header("Independent Mode Settings (only if not Slave mode)")]
    [Tooltip("Only animate when glitch materials are active (not original)")]
    public bool onlyAnimateGlitchMaterials = true;
    [Tooltip("Create runtime copies of glitch materials for animation")]
    public bool createGlitchMaterialCopies = true;

    // Private variables
    private TerminalSlamGlitch terminalGlitch;
    private Renderer targetRenderer;
    private Material[] originalMaterials;
    
    // Animation state
    private bool isAnimating = false;
    private Coroutine animationCoroutine;
    private bool wasGlitchingLastFrame = false;
    
    // Material tracking (for Independent mode)
    private Material[] lastKnownMaterials;
    private System.Collections.Generic.Dictionary<Material, Material> glitchMaterialCopies;
    
    // Original properties for restoration
    private System.Collections.Generic.Dictionary<Material, MaterialProperties> materialProperties;

    private struct MaterialProperties
    {
        public Vector2 mainTexOffset;
        public Vector2 mainTexScale;
        public Color color;
        public Color emissionColor;
        public bool hasMainTex;
        public bool hasColor;
        public bool hasEmission;
    }

    void Start()
    {
        if (enableDebugLogs) Debug.Log($"[MaterialGlitch] Starting on {gameObject.name} in {controlMode} mode");
        
        terminalGlitch = GetComponent<TerminalSlamGlitch>();
        if (terminalGlitch == null)
        {
            Debug.LogError($"[MaterialGlitch] No TerminalSlamGlitch component found on {gameObject.name}!");
            enabled = false;
            return;
        }

        // Find the renderer (use the same logic as TerminalSlamGlitch)
        Transform target = terminalGlitch.glitchTarget != null ? terminalGlitch.glitchTarget : transform;
        targetRenderer = target.GetComponent<Renderer>();
        if (targetRenderer == null)
            targetRenderer = target.GetComponentInChildren<Renderer>();
            
        if (targetRenderer == null)
        {
            Debug.LogError($"[MaterialGlitch] No Renderer found on {gameObject.name} or its children!");
            enabled = false;
            return;
        }

        if (enableDebugLogs) Debug.Log($"[MaterialGlitch] Found renderer: {targetRenderer.name}");

        SetupMaterialTracking();
        
        // Start animation based on control mode
        if (controlMode == ControlMode.ForceAlwaysOn)
        {
            StartGlitchAnimation();
        }
    }

    void SetupMaterialTracking()
    {
        originalMaterials = targetRenderer.sharedMaterials;
        materialProperties = new System.Collections.Generic.Dictionary<Material, MaterialProperties>();

        // Setup for Independent mode
        if (controlMode == ControlMode.Independent)
        {
            lastKnownMaterials = new Material[originalMaterials.Length];
            glitchMaterialCopies = new System.Collections.Generic.Dictionary<Material, Material>();
        }

        if (enableDebugLogs) Debug.Log($"[MaterialGlitch] Setting up material tracking for {originalMaterials.Length} materials");

        // Store original material properties
        for (int i = 0; i < originalMaterials.Length; i++)
        {
            if (originalMaterials[i] != null)
            {
                StoreOriginalProperties(originalMaterials[i]);
                
                if (controlMode == ControlMode.Independent)
                {
                    lastKnownMaterials[i] = originalMaterials[i];
                }
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[MaterialGlitch] Tracked original material {i}: {originalMaterials[i].name}");
                }
            }
        }

        // If we have access to glitch materials, prepare them too
        if (terminalGlitch.glitchMaterials != null && terminalGlitch.glitchMaterials.Length > 0)
        {
            foreach (var glitchMat in terminalGlitch.glitchMaterials)
            {
                if (glitchMat != null)
                {
                    StoreOriginalProperties(glitchMat);
                    
                    if (controlMode == ControlMode.Independent && createGlitchMaterialCopies)
                    {
                        Material copy = new Material(glitchMat);
                        copy.name = glitchMat.name + " (Animated Copy)";
                        glitchMaterialCopies[glitchMat] = copy;
                        
                        if (enableDebugLogs)
                            Debug.Log($"[MaterialGlitch] Created animated copy for glitch material: {glitchMat.name}");
                    }
                }
            }
        }

        if (enableDebugLogs) Debug.Log("[MaterialGlitch] Material tracking setup complete");
    }

    void StoreOriginalProperties(Material mat)
    {
        if (materialProperties.ContainsKey(mat)) return;

        MaterialProperties props = new MaterialProperties();
        
        // Check for common texture properties
        props.hasMainTex = mat.HasProperty("_MainTex") || mat.HasProperty("_BaseMap");
        props.hasColor = mat.HasProperty("_Color") || mat.HasProperty("_BaseColor");
        props.hasEmission = mat.HasProperty("_EmissionColor");

        // Store texture offset/scale
        if (mat.HasProperty("_MainTex"))
        {
            props.mainTexOffset = mat.GetTextureOffset("_MainTex");
            props.mainTexScale = mat.GetTextureScale("_MainTex");
        }
        else if (mat.HasProperty("_BaseMap"))
        {
            props.mainTexOffset = mat.GetTextureOffset("_BaseMap");
            props.mainTexScale = mat.GetTextureScale("_BaseMap");
        }

        // Store colors
        if (mat.HasProperty("_Color"))
            props.color = mat.GetColor("_Color");
        else if (mat.HasProperty("_BaseColor"))
            props.color = mat.GetColor("_BaseColor");

        if (props.hasEmission)
            props.emissionColor = mat.GetColor("_EmissionColor");

        materialProperties[mat] = props;
    }

    void Update()
    {
        // Only run Update logic for Independent mode
        if (controlMode != ControlMode.Independent) return;

        // Check if materials have changed (glitch system swapped them)
        CheckForMaterialChanges();
        
        bool shouldAnimate = ShouldAnimateCurrentState();
        
        // Debug current state
        if (enableDebugLogs && shouldAnimate != wasGlitchingLastFrame)
        {
            string currentMats = "";
            if (targetRenderer.materials != null)
            {
                for (int i = 0; i < targetRenderer.materials.Length; i++)
                {
                    currentMats += targetRenderer.materials[i]?.name + " ";
                }
            }
            
            Debug.Log($"[MaterialGlitch] Animation state changed: {shouldAnimate} | Current materials: {currentMats}");
            wasGlitchingLastFrame = shouldAnimate;
        }
        
        if (shouldAnimate && !isAnimating)
        {
            StartGlitchAnimation();
        }
        else if (!shouldAnimate && isAnimating)
        {
            StopGlitchAnimation();
        }
    }

    void CheckForMaterialChanges()
    {
        if (controlMode != ControlMode.Independent) return;

        Material[] currentMaterials = targetRenderer.materials;
        bool materialsChanged = false;

        if (currentMaterials.Length != lastKnownMaterials.Length)
        {
            materialsChanged = true;
        }
        else
        {
            for (int i = 0; i < currentMaterials.Length; i++)
            {
                if (currentMaterials[i] != lastKnownMaterials[i])
                {
                    materialsChanged = true;
                    break;
                }
            }
        }

        if (materialsChanged)
        {
            if (enableDebugLogs)
            {
                string newMats = "";
                for (int i = 0; i < currentMaterials.Length; i++)
                {
                    newMats += currentMaterials[i]?.name + " ";
                }
                Debug.Log($"[MaterialGlitch] Materials changed to: {newMats}");
            }

            // If we're creating copies and these are glitch materials, replace them with our animated copies
            if (createGlitchMaterialCopies && isAnimating)
            {
                Material[] newMaterials = new Material[currentMaterials.Length];
                bool replacedAny = false;

                for (int i = 0; i < currentMaterials.Length; i++)
                {
                    if (currentMaterials[i] != null && glitchMaterialCopies.ContainsKey(currentMaterials[i]))
                    {
                        newMaterials[i] = glitchMaterialCopies[currentMaterials[i]];
                        replacedAny = true;
                        
                        if (enableDebugLogs)
                            Debug.Log($"[MaterialGlitch] Replaced {currentMaterials[i].name} with animated copy");
                    }
                    else
                    {
                        newMaterials[i] = currentMaterials[i];
                    }
                }

                if (replacedAny)
                {
                    targetRenderer.materials = newMaterials;
                }
            }

            // Store new properties for any new materials
            for (int i = 0; i < currentMaterials.Length; i++)
            {
                if (currentMaterials[i] != null)
                {
                    StoreOriginalProperties(currentMaterials[i]);
                }
            }

            // Update our tracking
            lastKnownMaterials = new Material[currentMaterials.Length];
            for (int i = 0; i < currentMaterials.Length; i++)
            {
                lastKnownMaterials[i] = currentMaterials[i];
            }
        }
    }

    bool ShouldAnimateCurrentState()
    {
        if (controlMode == ControlMode.ForceAlwaysOn) return true;
        if (controlMode == ControlMode.SlaveToTerminalGlitch) return false; // Controlled externally

        // Independent mode logic
        if (!onlyAnimateGlitchMaterials)
        {
            return terminalGlitch.IsCurrentlyGlitching();
        }

        // Check if any current materials are glitch materials
        Material[] currentMaterials = targetRenderer.materials;
        foreach (var mat in currentMaterials)
        {
            if (mat != null)
            {
                // Check if this is a glitch material or our copy of one
                if (terminalGlitch.glitchMaterials != null)
                {
                    foreach (var glitchMat in terminalGlitch.glitchMaterials)
                    {
                        if (mat == glitchMat || (glitchMaterialCopies.ContainsKey(glitchMat) && mat == glitchMaterialCopies[glitchMat]))
                        {
                            return terminalGlitch.IsCurrentlyGlitching();
                        }
                    }
                }
            }
        }

        return false;
    }

    void StartGlitchAnimation()
    {
        if (enableDebugLogs) Debug.Log("[MaterialGlitch] Starting glitch animation");
        
        if (animationCoroutine != null)
            StopCoroutine(animationCoroutine);
            
        isAnimating = true;
        animationCoroutine = StartCoroutine(AnimateGlitchEffects());
    }

    void StopGlitchAnimation()
    {
        if (enableDebugLogs) Debug.Log("[MaterialGlitch] Stopping glitch animation");
        
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }
        
        isAnimating = false;
        RestoreAllMaterialProperties();
    }

    IEnumerator AnimateGlitchEffects()
    {
        if (enableDebugLogs) Debug.Log("[MaterialGlitch] Animation coroutine started");
        
        while (isAnimating)
        {
            float intensity = glitchIntensityMultiplier;
            
            // Animate whatever materials are currently active
            Material[] currentMaterials = targetRenderer.materials;
            for (int i = 0; i < currentMaterials.Length; i++)
            {
                if (currentMaterials[i] != null)
                {
                    AnimateMaterial(currentMaterials[i], i, intensity);
                }
            }
            
            yield return new WaitForSeconds(0.02f); // ~50 FPS update rate
        }
        
        if (enableDebugLogs) Debug.Log("[MaterialGlitch] Animation coroutine ended");
    }

    void AnimateMaterial(Material mat, int index, float intensity)
    {
        float time = Time.time;
        
        if (!materialProperties.ContainsKey(mat))
        {
            StoreOriginalProperties(mat);
        }
        
        MaterialProperties original = materialProperties[mat];

        // Matrix-style glitch lines using color manipulation
        Color currentColor = original.color;
        
        if (enableGlitchLines)
        {
            // Create line patterns using time-based calculations
            float screenY = (time * lineScrollSpeed) % 1f;
            float screenX = (time * lineScrollSpeed * 0.3f) % 1f;
            
            // Calculate line presence
            bool horizontalLine = (Mathf.Sin(screenY * lineFrequency * Mathf.PI) > (1f - lineThickness * 50f));
            bool verticalLine = (Mathf.Sin(screenX * lineFrequency * 0.7f * Mathf.PI) > (1f - lineThickness * 30f));
            
            // Add randomness
            horizontalLine = horizontalLine && (Mathf.PerlinNoise(time * 10f, screenY * 5f) > (1f - lineRandomness));
            verticalLine = verticalLine && (Mathf.PerlinNoise(screenX * 5f, time * 8f) > (1f - lineRandomness));
            
            if (horizontalLine || verticalLine)
            {
                // Apply line color effect
                float lineStrength = lineIntensity * intensity;
                if (useSteppedAnimation)
                {
                    lineStrength = StepValue(lineStrength, animationSteps);
                }
                
                currentColor = Color.Lerp(currentColor, lineColor, lineStrength);
                
                // Add flickering effect
                float flicker = Mathf.Sin(time * lineScrollSpeed * 20f) * 0.3f + 0.7f;
                currentColor *= flicker;
            }
        }

        // EXTREME COLOR GLITCHING - Very visible
        if (enableColorGlitch && original.hasColor)
        {
            Color glitchColor;
            
            if (enableExtremeEffects && Random.value < extremeEffectChance)
            {
                // Extreme random colors
                glitchColor = new Color(Random.value, Random.value, Random.value, 1f);
            }
            else
            {
                glitchColor = GetGlitchColor(time);
            }
            
            // Much more aggressive color mixing
            Color finalColor = Color.Lerp(currentColor, glitchColor, colorIntensity * intensity);
            
            if (useSteppedAnimation)
            {
                finalColor = StepColor(finalColor, animationSteps);
            }
            
            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", finalColor);
            }
            else if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", finalColor);
            }
        }
        else if (enableGlitchLines && original.hasColor)
        {
            // Apply just the line effect if color glitch is disabled
            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", currentColor);
            }
            else if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", currentColor);
            }
        }

        // EXTREME UV ANIMATION - Very visible
        if (enableUVAnimation && original.hasMainTex)
        {
            Vector2 scrollOffset = new Vector2(
                Mathf.Sin(time * uvScrollSpeed) * uvDistortionAmount * intensity,
                Mathf.Cos(time * uvScrollSpeed * 0.7f) * uvDistortionAmount * intensity
            );
            
            if (useSteppedAnimation)
            {
                scrollOffset = StepValue(scrollOffset, animationSteps);
            }
            
            Vector2 finalOffset = original.mainTexOffset + scrollOffset;
            
            if (mat.HasProperty("_MainTex"))
            {
                mat.SetTextureOffset("_MainTex", finalOffset);
            }
            else if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTextureOffset("_BaseMap", finalOffset);
            }
        }

        // EXTREME EMISSION PULSING - Very visible
        if (enableEmissionPulse)
        {
            float pulse = (Mathf.Sin(time * emissionPulseSpeed) + 1f) * 0.5f;
            pulse = Mathf.Pow(pulse, 2f); // Make it more snappy
            
            // Add line-based pulsing
            if (enableGlitchLines)
            {
                float linePulse = Mathf.Sin(time * lineScrollSpeed * 15f) * 0.5f + 0.5f;
                pulse = Mathf.Max(pulse, linePulse * lineIntensity);
            }
            
            if (useSteppedAnimation)
            {
                pulse = StepValue(pulse, animationSteps);
            }
            
            Color emission = emissionColor * (pulse * maxEmissionIntensity * intensity);
            
            // Mix in line color for emission
            if (enableGlitchLines)
            {
                emission = Color.Lerp(emission, lineColor * maxEmissionIntensity * intensity, 0.3f);
            }
            
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", emission);
                mat.EnableKeyword("_EMISSION");
            }
        }

        // EXTREME TEXTURE JITTERING - Very visible
        if (enableTextureJitter && original.hasMainTex)
        {
            float jitterX = Mathf.Sin(time * jitterSpeed + index) * maxTextureOffset * intensity;
            float jitterY = Mathf.Cos(time * jitterSpeed * 1.3f + index) * maxTextureOffset * intensity;
            float scaleJitter = 1f + Mathf.Sin(time * jitterSpeed * 0.8f + index) * (maxTextureScale - 1f) * intensity;
            
            if (useSteppedAnimation)
            {
                jitterX = StepValue(jitterX, animationSteps);
                jitterY = StepValue(jitterY, animationSteps);
                scaleJitter = StepValue(scaleJitter, animationSteps);
            }
            
            Vector2 jitteredOffset = original.mainTexOffset + new Vector2(jitterX, jitterY);
            Vector2 jitteredScale = original.mainTexScale * scaleJitter;
            
            if (mat.HasProperty("_MainTex"))
            {
                mat.SetTextureOffset("_MainTex", jitteredOffset);
                mat.SetTextureScale("_MainTex", jitteredScale);
            }
            else if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTextureOffset("_BaseMap", jitteredOffset);
                mat.SetTextureScale("_BaseMap", jitteredScale);
            }
        }
    }

    Color GetGlitchColor(float time)
    {
        if (useRandomColors && glitchColors.Length > 0)
        {
            float colorTime = time * colorFlickerSpeed;
            int colorIndex = Mathf.FloorToInt(colorTime) % glitchColors.Length;
            
            if (useSteppedAnimation)
            {
                return glitchColors[colorIndex];
            }
            else
            {
                int nextColorIndex = (colorIndex + 1) % glitchColors.Length;
                float t = colorTime - Mathf.Floor(colorTime);
                return Color.Lerp(glitchColors[colorIndex], glitchColors[nextColorIndex], t);
            }
        }
        
        // Fallback to HSV shifting
        float hue = (time * colorFlickerSpeed) % 1f;
        return Color.HSVToRGB(hue, 1f, 1f); // Full saturation and brightness
    }

    Vector2 StepValue(Vector2 value, int steps)
    {
        return new Vector2(
            StepValue(value.x, steps),
            StepValue(value.y, steps)
        );
    }

    float StepValue(float value, int steps)
    {
        return Mathf.Round(value * steps) / steps;
    }

    Color StepColor(Color color, int steps)
    {
        return new Color(
            StepValue(color.r, steps),
            StepValue(color.g, steps),
            StepValue(color.b, steps),
            color.a
        );
    }

    void RestoreAllMaterialProperties()
    {
        if (enableDebugLogs) Debug.Log("[MaterialGlitch] Restoring all material properties");
        
        // Restore all materials we know about
        foreach (var kvp in materialProperties)
        {
            Material mat = kvp.Key;
            MaterialProperties original = kvp.Value;
            
            if (mat != null)
            {
                RestoreMaterialProperties(mat, original);
            }
        }
    }

    void RestoreMaterialProperties(Material mat, MaterialProperties original)
    {
        if (original.hasMainTex)
        {
            if (mat.HasProperty("_MainTex"))
            {
                mat.SetTextureOffset("_MainTex", original.mainTexOffset);
                mat.SetTextureScale("_MainTex", original.mainTexScale);
            }
            else if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTextureOffset("_BaseMap", original.mainTexOffset);
                mat.SetTextureScale("_BaseMap", original.mainTexScale);
            }
        }
        
        if (original.hasColor)
        {
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", original.color);
            else if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", original.color);
        }
        
        if (original.hasEmission && mat.HasProperty("_EmissionColor"))
        {
            mat.SetColor("_EmissionColor", original.emissionColor);
        }
    }

    void OnDestroy()
    {
        // Clean up runtime material copies
        if (glitchMaterialCopies != null)
        {
            foreach (var kvp in glitchMaterialCopies)
            {
                if (kvp.Value != null)
                {
                    DestroyImmediate(kvp.Value);
                }
            }
        }
    }

    // Public methods for external control (used by TerminalSlamGlitch)
    public void ForceStartAnimation()
    {
        if (enableDebugLogs) Debug.Log("[MaterialGlitch] Force starting animation (called externally)");
        StartGlitchAnimation();
    }

    public void ForceStopAnimation()
    {
        if (enableDebugLogs) Debug.Log("[MaterialGlitch] Force stopping animation (called externally)");
        StopGlitchAnimation();
    }

    public void SetGlitchIntensity(float intensity)
    {
        glitchIntensityMultiplier = intensity;
    }

    // Debug helpers
    [ContextMenu("Test Animation")]
    void TestAnimation()
    {
        if (isAnimating)
        {
            ForceStopAnimation();
        }
        else
        {
            ForceStartAnimation();
        }
        Debug.Log($"[MaterialGlitch] Test animation: {isAnimating}");
    }

    [ContextMenu("Switch to Slave Mode")]
    void SwitchToSlaveMode()
    {
        controlMode = ControlMode.SlaveToTerminalGlitch;
        Debug.Log("[MaterialGlitch] Switched to Slave mode - will be controlled by TerminalSlamGlitch");
    }

    [ContextMenu("Switch to Independent Mode")]
    void SwitchToIndependentMode()
    {
        controlMode = ControlMode.Independent;
        Debug.Log("[MaterialGlitch] Switched to Independent mode - will use own distance checking");
    }
}