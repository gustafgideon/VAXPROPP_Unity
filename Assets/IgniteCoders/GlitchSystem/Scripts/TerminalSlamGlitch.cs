using System.Collections;
using UnityEngine;

public class TerminalSlamGlitch : MonoBehaviour 
{
    [Header("Target")]
    [Tooltip("All transform-based effects (wobble, rotation/scale glitches) are applied to this transform. Assign a child like 'Visuals' to keep the root (and the frame) stable. If left empty, this GameObject is used.")]
    public Transform glitchTarget;

    [Header("Distance Settings")]
    public bool useDistanceCheck = true;
    [Range(1f, 50f)] public float maxGlitchDistance = 15f;
    [Range(0f, 20f)] public float minGlitchDistance = 2f;
    public string playerTag = "Player";
    
    [Header("Continuous Glitch Settings")]
    [Range(0.01f, 1f)] public float glitchSpeed = 0.1f;
    [Range(0.05f, 0.5f)] public float glitchDuration = 0.1f;
    [Range(0.01f, 0.3f)] public float restDuration = 0.05f;
    
    [Header("Corruption Assets")]
    public Mesh[] corruptedMeshes;
    public Material[] glitchMaterials;
    
    [Header("Advanced Settings")]
    public bool useMultipleAssets = true;
    public bool randomizeScale = true;
    [Range(0f, 0.5f)] public float scaleVariationAmount = 0.2f;
    [Range(0f, 1f)] public float intensityBasedOnDistance = 0.5f;

    [Header("Rotation Glitch")]
    public bool enableRotationGlitch = true;
    [Tooltip("Maximum random rotation offset per axis in degrees, scaled by intensity.")]
    [Range(0f, 180f)] public float maxRotationAngle = 25f;

    [Header("Material Glitch Integration")]
    [Tooltip("Automatically control MaterialGlitch component when player is in range")]
    public bool enableMaterialGlitch = true;
    [Tooltip("MaterialGlitch component reference (auto-found if null)")]
    public MaterialGlitch materialGlitchComponent;

    public enum WobbleApplyMode
    {
        Always,
        OnlyDuringRest,
        OnlyWhenOriginalMeshVisible,
        OnlyWhenInRange
    }

    [Header("Lightweight Object Wobble (applies to WHOLE visuals)")]
    public bool enableWobble = true;
    public WobbleApplyMode wobbleApplyMode = WobbleApplyMode.OnlyWhenInRange;
    [Range(0.1f, 60f)] public float wobbleFrequency = 12f;
    [Range(0f, 0.2f)] public float wobblePosAmplitude = 0.01f;
    [Range(0f, 20f)] public float wobbleRotAngle = 1.5f;
    [Range(0f, 0.2f)] public float wobbleScaleAmount = 0.02f;
    public bool wobbleIntensityWithDistance = true;

    // Private variables
    private Mesh originalMesh;
    private Material originalMaterial;
    private Vector3 originalScale;
    private Vector3 originalPosition; // NEW: Store original position
    private Quaternion originalRotation; // NEW: Store original rotation
    private MeshFilter meshFilter;
    private Renderer meshRenderer;
    private GameObject playerObject;
    
    // Continuous glitching state
    private bool isPlayerNear = false;
    private bool isCurrentlyGlitched = false;
    private bool isInRestPhase = false;
    private Coroutine continuousGlitchCoroutine;

    // Restore state (for transform-based glitches)
    private Quaternion lastRotationBeforeGlitch;
    private Vector3 lastScaleBeforeGlitch;

    // Wobble state (so it doesn't accumulate)
    private Vector3 lastWobblePos = Vector3.zero;
    private Quaternion lastWobbleRot = Quaternion.identity;
    private float lastWobbleScale = 1f;
    private bool wobbleApplied = false;

    // Seeds for decorrelated axes
    private float jSeedX, jSeedY, jSeedZ;

    void Start() 
    {
        if (glitchTarget == null) glitchTarget = transform;

        // Cache components (search under glitchTarget if needed)
        meshFilter = glitchTarget.GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = glitchTarget.GetComponentInChildren<MeshFilter>();
        meshRenderer = glitchTarget.GetComponent<Renderer>();
        if (meshRenderer == null) meshRenderer = glitchTarget.GetComponentInChildren<Renderer>();
        
        if (meshFilter != null) originalMesh = meshFilter.sharedMesh;
        if (meshRenderer != null) originalMaterial = meshRenderer.sharedMaterial;
        
        // NEW: Store original transform state
        originalScale = glitchTarget.localScale;
        originalPosition = glitchTarget.localPosition;
        originalRotation = glitchTarget.localRotation;
        
        if (useDistanceCheck)
        {
            playerObject = GameObject.FindWithTag(playerTag);
            if (playerObject == null)
            {
                Debug.LogWarning($"TerminalSlamGlitch on {gameObject.name}: No object found with tag '{playerTag}'. Distance checking disabled.");
                useDistanceCheck = false;
            }
        }

        // Auto-find MaterialGlitch component if not assigned
        if (enableMaterialGlitch && materialGlitchComponent == null)
        {
            materialGlitchComponent = GetComponent<MaterialGlitch>();
            if (materialGlitchComponent == null)
            {
                Debug.LogWarning($"TerminalSlamGlitch on {gameObject.name}: MaterialGlitch component not found. Material glitch effects disabled.");
                enableMaterialGlitch = false;
            }
        }

        // Wobble seeds
        jSeedX = Random.value * 1000f;
        jSeedY = Random.value * 1000f;
        jSeedZ = Random.value * 1000f;

        lastWobbleRot = Quaternion.identity;
        lastWobbleScale = 1f;
    }
    
    void Update() 
    {
        bool playerNearNow = IsPlayerInRange();
        if (playerNearNow && !isPlayerNear)
        {
            isPlayerNear = true;
            StartContinuousGlitching();
            
            // Start material glitch effects
            if (enableMaterialGlitch && materialGlitchComponent != null)
            {
                materialGlitchComponent.ForceStartAnimation();
            }
        }
        else if (!playerNearNow && isPlayerNear)
        {
            isPlayerNear = false;
            StopContinuousGlitching();
            
            // Stop material glitch effects
            if (enableMaterialGlitch && materialGlitchComponent != null)
            {
                materialGlitchComponent.ForceStopAnimation();
            }
            
            // NEW: Reset to original transform state
            ResetToOriginalTransform();
        }

        HandleWholeObjectWobble();
    }
    
    // NEW: Reset transform to original state
    void ResetToOriginalTransform()
    {
        if (glitchTarget != null)
        {
            // Remove any applied wobble first
            RemoveLastWobbleIfApplied();
            
            // Reset to original transform state
            glitchTarget.localPosition = originalPosition;
            glitchTarget.localRotation = originalRotation;
            glitchTarget.localScale = originalScale;
            
            Debug.Log($"[TerminalSlamGlitch] Reset {glitchTarget.name} to original transform state");
        }
    }
    
    bool IsPlayerInRange()
    {
        if (!useDistanceCheck || playerObject == null) return true;
        float distance = Vector3.Distance(transform.position, playerObject.transform.position);
        return distance >= minGlitchDistance && distance <= maxGlitchDistance;
    }
    
    float GetDistanceIntensity()
    {
        if (!useDistanceCheck || playerObject == null) return 1f;
        float distance = Vector3.Distance(transform.position, playerObject.transform.position);
        float distanceRange = Mathf.Max(0.0001f, maxGlitchDistance - minGlitchDistance);
        float normalizedDistance = Mathf.Clamp01((distance - minGlitchDistance) / distanceRange);
        return Mathf.Lerp(1f, intensityBasedOnDistance, normalizedDistance);
    }
    
    void StartContinuousGlitching()
    {
        if (continuousGlitchCoroutine != null) StopCoroutine(continuousGlitchCoroutine);
        continuousGlitchCoroutine = StartCoroutine(ContinuousGlitchLoop());
    }
    
    void StopContinuousGlitching()
    {
        if (continuousGlitchCoroutine != null)
        {
            StopCoroutine(continuousGlitchCoroutine);
            continuousGlitchCoroutine = null;
        }
        
        RestoreOriginal();
        isCurrentlyGlitched = false;
        isInRestPhase = false;

        RemoveLastWobbleIfApplied();
    }
    
    IEnumerator ContinuousGlitchLoop()
    {
        while (isPlayerNear)
        {
            float intensity = GetDistanceIntensity();
            isInRestPhase = false;
            ApplyGlitch(intensity);
            isCurrentlyGlitched = true;
            float currentGlitchDuration = glitchDuration / (1f + intensity);
            yield return new WaitForSeconds(currentGlitchDuration);
            RestoreOriginal();
            isCurrentlyGlitched = false;
            isInRestPhase = true;
            float currentRestDuration = restDuration / (1f + intensity);
            yield return new WaitForSeconds(currentRestDuration);
            isInRestPhase = false;
        }
    }
    
    void ApplyGlitch(float intensity) 
    {
        if (corruptedMeshes != null && corruptedMeshes.Length > 0 && meshFilter != null) 
        {
            meshFilter.mesh = useMultipleAssets
                ? corruptedMeshes[Random.Range(0, corruptedMeshes.Length)]
                : corruptedMeshes[0];
        }
        if (glitchMaterials != null && glitchMaterials.Length > 0 && meshRenderer != null) 
        {
            meshRenderer.material = useMultipleAssets
                ? glitchMaterials[Random.Range(0, glitchMaterials.Length)]
                : glitchMaterials[0];
        }

        lastRotationBeforeGlitch = glitchTarget.rotation;
        lastScaleBeforeGlitch = glitchTarget.localScale;

        if (enableRotationGlitch)
        {
            Vector3 deltaEuler = new Vector3(
                Random.Range(-maxRotationAngle, maxRotationAngle),
                Random.Range(-maxRotationAngle, maxRotationAngle),
                Random.Range(-maxRotationAngle, maxRotationAngle)
            ) * intensity;

            glitchTarget.rotation = lastRotationBeforeGlitch * Quaternion.Euler(deltaEuler);
        }
        
        if (randomizeScale)
        {
            float maxVariation = scaleVariationAmount * intensity;
            float scaleVariation = Random.Range(1f - maxVariation, 1f + maxVariation);
            glitchTarget.localScale = originalScale * scaleVariation;
        }
    }
    
    void RestoreOriginal() 
    {
        if (meshFilter != null && originalMesh != null) meshFilter.mesh = originalMesh;
        if (meshRenderer != null && originalMaterial != null) meshRenderer.material = originalMaterial;

        if (glitchTarget != null)
        {
            glitchTarget.rotation = lastRotationBeforeGlitch;
            glitchTarget.localScale = lastScaleBeforeGlitch;
        }
    }

    // Whole-object Wobble (on glitchTarget)
    void HandleWholeObjectWobble()
    {
        if (glitchTarget == null || !enableWobble)
        {
            RemoveLastWobbleIfApplied();
            return;
        }

        bool shouldApply = false;
        switch (wobbleApplyMode)
        {
            case WobbleApplyMode.Always:
                shouldApply = true;
                break;
            case WobbleApplyMode.OnlyDuringRest:
                shouldApply = isPlayerNear && !isCurrentlyGlitched && isInRestPhase;
                break;
            case WobbleApplyMode.OnlyWhenOriginalMeshVisible:
                shouldApply = (meshFilter != null && meshFilter.mesh == originalMesh);
                break;
            case WobbleApplyMode.OnlyWhenInRange:
                shouldApply = isPlayerNear;
                break;
        }

        if (!shouldApply)
        {
            RemoveLastWobbleIfApplied();
            return;
        }

        // Remove last frame's wobble from the current transform to avoid drift
        if (wobbleApplied)
        {
            glitchTarget.localPosition -= lastWobblePos;
            glitchTarget.localRotation = glitchTarget.localRotation * Quaternion.Inverse(lastWobbleRot);
            if (Mathf.Abs(lastWobbleScale - 1f) > 1e-6f) glitchTarget.localScale /= lastWobbleScale;
        }

        float intensity = wobbleIntensityWithDistance ? GetDistanceIntensity() : 1f;
        float t = Time.time;

        Vector3 posOff = new Vector3(
            Mathf.Sin((t + jSeedX) * wobbleFrequency * 1.12f),
            Mathf.Sin((t + jSeedY) * wobbleFrequency * 0.97f),
            Mathf.Sin((t + jSeedZ) * wobbleFrequency * 1.03f)
        ) * (wobblePosAmplitude * Mathf.Max(0f, intensity));

        Vector3 rotEuler = new Vector3(
            Mathf.Sin((t + jSeedX) * wobbleFrequency * 1.35f),
            Mathf.Sin((t + jSeedY) * wobbleFrequency * 0.88f),
            Mathf.Sin((t + jSeedZ) * wobbleFrequency * 1.21f)
        ) * (wobbleRotAngle * Mathf.Max(0f, intensity));
        Quaternion rotOff = Quaternion.Euler(rotEuler);

        float scaleOff = 1f + Mathf.Sin((t + jSeedY) * wobbleFrequency * 0.83f) * (wobbleScaleAmount * Mathf.Max(0f, intensity));
        scaleOff = Mathf.Max(0.0001f, scaleOff);

        glitchTarget.localPosition += posOff;
        glitchTarget.localRotation = glitchTarget.localRotation * rotOff;
        glitchTarget.localScale = glitchTarget.localScale * scaleOff;

        lastWobblePos = posOff;
        lastWobbleRot = rotOff;
        lastWobbleScale = scaleOff;
        wobbleApplied = true;
    }

    void RemoveLastWobbleIfApplied()
    {
        if (!wobbleApplied || glitchTarget == null) return;
        glitchTarget.localPosition -= lastWobblePos;
        glitchTarget.localRotation = glitchTarget.localRotation * Quaternion.Inverse(lastWobbleRot);
        if (Mathf.Abs(lastWobbleScale - 1f) > 1e-6f) glitchTarget.localScale /= lastWobbleScale;
        lastWobblePos = Vector3.zero;
        lastWobbleRot = Quaternion.identity;
        lastWobbleScale = 1f;
        wobbleApplied = false;
    }
    
    public void ForceStartGlitching()
    {
        isPlayerNear = true;
        StartContinuousGlitching();
        
        // Start material glitch effects
        if (enableMaterialGlitch && materialGlitchComponent != null)
        {
            materialGlitchComponent.ForceStartAnimation();
        }
    }
    
    public void ForceStopGlitching()
    {
        isPlayerNear = false;
        StopContinuousGlitching();
        
        // Stop material glitch effects
        if (enableMaterialGlitch && materialGlitchComponent != null)
        {
            materialGlitchComponent.ForceStopAnimation();
        }
        
        // NEW: Reset to original transform state
        ResetToOriginalTransform();
    }
    
    public bool IsCurrentlyGlitching()
    {
        return isPlayerNear && isCurrentlyGlitched;
    }
    
    public float GetDistanceToPlayer()
    {
        if (playerObject == null) return float.MaxValue;
        return Vector3.Distance(transform.position, playerObject.transform.position);
    }
    
    void OnDrawGizmosSelected()
    {
        if (!useDistanceCheck) return;
        Gizmos.color = isPlayerNear ? Color.red : Color.gray;
        Gizmos.DrawWireSphere(transform.position, maxGlitchDistance);
        if (minGlitchDistance > 0)
        {
            Gizmos.color = isPlayerNear ? Color.blue : Color.gray;
            Gizmos.DrawWireSphere(transform.position, minGlitchDistance);
        }
        if (isPlayerNear && playerObject != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, playerObject.transform.position);
        }
    }
    
    void OnDestroy()
    {
        if (continuousGlitchCoroutine != null) StopCoroutine(continuousGlitchCoroutine);
        RemoveLastWobbleIfApplied();
        
        // Stop material glitch effects
        if (enableMaterialGlitch && materialGlitchComponent != null)
        {
            materialGlitchComponent.ForceStopAnimation();
        }
    }

#if UNITY_EDITOR
    // Editor helper: Create/ensure a 'Visuals' container, move child renderers,
    // and migrate root-level renderers into children so the root stays clean.
    [ContextMenu("Setup Visuals container (migrate all Renderers)")]
    void SetupVisualsAndMigrateRenderers()
    {
        // Ensure Visuals child
        Transform visuals = glitchTarget != null ? glitchTarget : transform.Find("Visuals");
        if (visuals == null)
        {
            var visualsGO = new GameObject("Visuals");
            visuals = visualsGO.transform;
            visuals.SetParent(transform, false);
            visuals.localPosition = Vector3.zero;
            visuals.localRotation = Quaternion.identity;
            visuals.localScale = Vector3.one;
        }
        glitchTarget = visuals;

        int moved = 0, migrated = 0;

        // Move all child renderers (except root) under Visuals
        var childRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in childRenderers)
        {
            if (r == null) continue;
            if (r.transform == transform) continue; // handle root separately
            if (r.transform == visuals) continue;
            r.transform.SetParent(visuals, true);
            moved++;
        }

        // Migrate root-level renderers by cloning components into a new child
        var rootSkinned = GetComponents<SkinnedMeshRenderer>();
        foreach (var smr in rootSkinned)
        {
            if (smr == null || smr.transform != transform) continue;
            var go = new GameObject($"{name}_SkinnedVisual");
            go.transform.SetParent(visuals, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            UnityEditorInternal.ComponentUtility.CopyComponent(smr);
            UnityEditorInternal.ComponentUtility.PasteComponentAsNew(go);
            UnityEditor.Undo.DestroyObjectImmediate(smr);
            migrated++;
        }

        var rootMeshRenderers = GetComponents<MeshRenderer>();
        var rootMeshFilter = GetComponent<MeshFilter>();

        foreach (var mr in rootMeshRenderers)
        {
            if (mr == null || mr.transform != transform) continue;
            var go = new GameObject($"{name}_MeshVisual");
            go.transform.SetParent(visuals, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            if (rootMeshFilter != null)
            {
                UnityEditorInternal.ComponentUtility.CopyComponent(rootMeshFilter);
                UnityEditorInternal.ComponentUtility.PasteComponentAsNew(go);
            }

            UnityEditorInternal.ComponentUtility.CopyComponent(mr);
            UnityEditorInternal.ComponentUtility.PasteComponentAsNew(go);

            UnityEditor.Undo.DestroyObjectImmediate(mr);
            migrated++;
        }

        if (rootMeshFilter != null && GetComponent<MeshRenderer>() == null)
        {
            UnityEditor.Undo.DestroyObjectImmediate(rootMeshFilter);
        }

        // Refresh cached refs
        meshFilter = glitchTarget.GetComponentInChildren<MeshFilter>();
        meshRenderer = glitchTarget.GetComponentInChildren<Renderer>();
        if (meshFilter != null) originalMesh = meshFilter.sharedMesh;
        if (meshRenderer != null) originalMaterial = meshRenderer.sharedMaterial;
        
        // NEW: Update original transform state
        originalScale = glitchTarget.localScale;
        originalPosition = glitchTarget.localPosition;
        originalRotation = glitchTarget.localRotation;

        Debug.Log($"TerminalSlamGlitch on {name}: Moved {moved} renderer object(s) under 'Visuals' and migrated {migrated} root renderer(s). Glitch Target set to '{glitchTarget.name}'.");
    }
#endif
}