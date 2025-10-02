using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Camera))]
public class VisionManager : MonoBehaviour
{
    [Serializable]
    public class VisionPreset
    {
        public string name = "Normal";
        
        [Header("Render Texture Settings")]
        [Range(0.1f, 1f)]
        public float resolutionScale = 1f;
        public FilterMode filterMode = FilterMode.Bilinear;
        
        [Header("Post-Processing Material (optional)")]
        public Material postProcessMaterial;
        
        [Header("Display Transform Effects")]
        [Tooltip("Stretch the display horizontally")]
        [Range(0.1f, 3f)]
        public float stretchX = 1f;
        
        [Tooltip("Stretch the display vertically")]
        [Range(0.1f, 3f)]
        public float stretchY = 1f;
        
        [Tooltip("Rotate the display (degrees)")]
        [Range(-180f, 180f)]
        public float rotation = 0f;
        
        [Header("UV Tiling & Offset")]
        [Tooltip("UV tiling - values > 1 will repeat the image")]
        public Vector2 uvTiling = Vector2.one;
        
        [Tooltip("UV offset - shifts the image")]
        public Vector2 uvOffset = Vector2.zero;
        
        [Header("Color Tint")]
        public Color tint = Color.white;
    }

    [Header("Configuration")]
    public List<VisionPreset> presets = new List<VisionPreset>();
    
    [Header("References")]
    public Camera targetCamera;
    public RawImage overlayImage;
    
    [Header("Transition Settings")]
    [Range(0f, 2f)]
    public float transitionDuration = 0.3f;
    public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Default")]
    public string defaultPresetName = "Normal";

    // Runtime state
    private Dictionary<string, RenderTexture> renderTextures = new Dictionary<string, RenderTexture>();
    private VisionPreset currentPreset;
    private VisionPreset targetPreset;
    private VisionPreset normalPreset;
    private Coroutine transitionCoroutine;
    private CanvasGroup overlayCanvasGroup;

    public static VisionManager Instance { get; private set; }

    void Awake()
    {
        Instance = this;
        
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();
            
        if (overlayImage != null)
        {
            overlayImage.raycastTarget = false;
            
            // Add CanvasGroup for fade transitions
            overlayCanvasGroup = overlayImage.GetComponent<CanvasGroup>();
            if (overlayCanvasGroup == null)
                overlayCanvasGroup = overlayImage.gameObject.AddComponent<CanvasGroup>();
        }

        // Create a "normal" preset if none exists
        normalPreset = new VisionPreset { name = "Normal", resolutionScale = 1f };
    }

    void Start()
    {
        // Initialize all render textures
        InitializeRenderTextures();
        
        // Set initial vision (no transition)
        if (!string.IsNullOrEmpty(defaultPresetName))
            SetVisionImmediate(defaultPresetName);
        else if (presets.Count > 0)
            SetVisionImmediate(presets[0].name);
    }

    void OnDestroy()
    {
        if (Instance == this) 
            Instance = null;
        
        CleanupRenderTextures();
    }

    void OnDisable()
    {
        CleanupRenderTextures();
    }

    // PUBLIC API
    public void SetVision(string presetName)
    {
        VisionPreset preset = presets.Find(p => p.name == presetName);
        
        if (preset == null)
        {
            Debug.LogWarning($"Vision preset '{presetName}' not found. Using normal vision.");
            preset = normalPreset;
        }

        if (preset == currentPreset) return;

        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);
            
        transitionCoroutine = StartCoroutine(TransitionToPreset(preset));
    }

    public void SetVisionImmediate(string presetName)
    {
        VisionPreset preset = presets.Find(p => p.name == presetName);
        
        if (preset == null)
        {
            Debug.LogWarning($"Vision preset '{presetName}' not found. Using normal vision.");
            preset = normalPreset;
        }

        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);
            
        ApplyPreset(preset);
        currentPreset = preset;
    }

    public void SetVisionByIndex(int index)
    {
        if (presets.Count == 0 || index < 0 || index >= presets.Count)
        {
            Debug.LogWarning("Invalid vision index.");
            return;
        }
        
        SetVision(presets[index].name);
    }

    // TRANSITION LOGIC
    private IEnumerator TransitionToPreset(VisionPreset newPreset)
    {
        targetPreset = newPreset;
        
        if (transitionDuration <= 0f)
        {
            ApplyPreset(newPreset);
            currentPreset = newPreset;
            yield break;
        }

        // Fade out
        float elapsed = 0f;
        while (elapsed < transitionDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (transitionDuration / 2f);
            float alpha = Mathf.Lerp(1f, 0f, transitionCurve.Evaluate(t));
            
            if (overlayCanvasGroup != null)
                overlayCanvasGroup.alpha = alpha;
                
            yield return null;
        }

        // Switch preset at midpoint
        ApplyPreset(newPreset);
        currentPreset = newPreset;

        // Fade in
        elapsed = 0f;
        while (elapsed < transitionDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (transitionDuration / 2f);
            float alpha = Mathf.Lerp(0f, 1f, transitionCurve.Evaluate(t));
            
            if (overlayCanvasGroup != null)
                overlayCanvasGroup.alpha = alpha;
                
            yield return null;
        }

        if (overlayCanvasGroup != null)
            overlayCanvasGroup.alpha = 1f;
            
        transitionCoroutine = null;
    }

    // CORE LOGIC
    private void InitializeRenderTextures()
    {
        foreach (var preset in presets)
        {
            // Skip the "Normal" preset - it renders directly to screen
            if (preset.name == "Normal" && preset.resolutionScale >= 0.99f && preset.postProcessMaterial == null)
                continue;
            
            int w = Mathf.Max(16, Mathf.RoundToInt(Screen.width * preset.resolutionScale));
            int h = Mathf.Max(16, Mathf.RoundToInt(Screen.height * preset.resolutionScale));
            
            RenderTexture rt = new RenderTexture(w, h, 24)
            {
                name = $"Vision_{preset.name}",
                filterMode = preset.filterMode,
                antiAliasing = 1
            };
            rt.Create();
            
            renderTextures[preset.name] = rt;
        }
    }

    private void ApplyPreset(VisionPreset preset)
    {
        if (preset == null || targetCamera == null) return;
        
        // Check if this preset uses a render texture
        bool usesRT = renderTextures.ContainsKey(preset.name);
        
        if (usesRT)
        {
            RenderTexture rt = renderTextures[preset.name];
            
            // Assign RT to camera
            targetCamera.targetTexture = rt;
            
            // Show overlay
            if (overlayImage != null)
            {
                overlayImage.texture = rt;
                overlayImage.material = preset.postProcessMaterial;
                overlayImage.color = preset.tint;
                overlayImage.gameObject.SetActive(true);
                
                // Apply transform effects
                ApplyTransformEffects(preset);
            }
        }
        else
        {
            // Normal rendering (no RT)
            targetCamera.targetTexture = null;
            
            if (overlayImage != null)
            {
                overlayImage.gameObject.SetActive(false);
                overlayImage.texture = null;
                overlayImage.material = null;
                ResetTransformEffects();
            }
        }
    }

    private void ApplyTransformEffects(VisionPreset preset)
    {
        if (overlayImage == null) return;
        
        RectTransform rt = overlayImage.rectTransform;
        
        // Apply scale (stretch)
        rt.localScale = new Vector3(preset.stretchX, preset.stretchY, 1f);
        
        // Apply rotation
        rt.localEulerAngles = new Vector3(0f, 0f, preset.rotation);
        
        // Apply UV tiling and offset
        overlayImage.uvRect = new Rect(
            preset.uvOffset.x, 
            preset.uvOffset.y, 
            preset.uvTiling.x, 
            preset.uvTiling.y
        );
        
        // Ensure pivot is centered for proper rotation/scale
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }

    private void ResetTransformEffects()
    {
        if (overlayImage == null) return;
        
        RectTransform rt = overlayImage.rectTransform;
        rt.localScale = Vector3.one;
        rt.localEulerAngles = Vector3.zero;
        overlayImage.uvRect = new Rect(0f, 0f, 1f, 1f);
        overlayImage.color = Color.white;
    }

    private void CleanupRenderTextures()
    {
        if (targetCamera != null)
            targetCamera.targetTexture = null;
            
        if (overlayImage != null)
        {
            overlayImage.texture = null;
            overlayImage.material = null;
        }

        foreach (var rt in renderTextures.Values)
        {
            if (rt != null)
            {
                rt.Release();
                Destroy(rt);
            }
        }
        
        renderTextures.Clear();
    }
}