using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Camera))]
public class VisionManager : MonoBehaviour
{
    public enum VisionType
    {
        Normal,
        LowResRenderTexture,
        MaterialOnRenderTexture
    }

    [Serializable]
    public class Vision
    {
        public string name = "Vision";
        public VisionType type = VisionType.Normal;

        [Header("RenderTexture settings (for LowRes/Material)")]
        [Tooltip("Resolution scale relative to screen. 0.1 = 10% of screen resolution.")]
        [Range(0.01f, 1f)]
        public float resolutionScale = 0.2f;

        [Tooltip("Texture filter used to achieve a crisp pixelated look (Point) or smooth (Bilinear).")]
        public FilterMode filterMode = FilterMode.Point;

        [Header("Optional material applied to the fullscreen RawImage (for extra effects)")]
        public Material overlayMaterial;

        [Header("Trippy / tweak settings (affect how the RT is displayed)")]
        [Tooltip("Non-uniform scale applied to the fullscreen RawImage (stretch horizontally).")]
        public float stretchScaleX = 1f;

        [Tooltip("Non-uniform scale applied to the fullscreen RawImage (stretch vertically).")]
        public float stretchScaleY = 1f;

        [Tooltip("Rotation (degrees) applied to the fullscreen RawImage.")]
        public float rotationDegrees = 0f;

        [Tooltip("UV offset applied to the fullscreen RawImage texture (0..1 wraps).")]
        public Vector2 uvOffset = Vector2.zero;

        [Tooltip("UV tiling/scale applied to the fullscreen RawImage texture. Values >1 will tile.")]
        public Vector2 uvScale = Vector2.one;

        [Tooltip("If true, the RawImage's pivot will be kept at center when applying transforms.")]
        public bool keepPivotCentered = true;
    }

    [Tooltip("List of vision presets. Indexing used by Next/Prev and string lookup.")]
    public List<Vision> visions = new List<Vision>();

    [Tooltip("Main camera to control. If null, will use the camera on this GameObject.")]
    public Camera targetCamera;

    [Tooltip("Fullscreen RawImage (UI) used to display the camera RenderTexture. Create a full-screen RawImage in a Canvas.")]
    public RawImage fullscreenRawImage;

    [Tooltip("Optional: name of the default vision (must match one vision entry). If empty, first vision is used.")]
    public string defaultVisionName;

    // runtime
    private RenderTexture activeRT;
    private int currentIndex = 0;

    // singleton convenience (optional)
    public static VisionManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else if (Instance != this)
            Debug.LogWarning("Multiple VisionManager instances in scene. Using the first one.");

        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        if (fullscreenRawImage != null)
            fullscreenRawImage.raycastTarget = false; // allow clicks through if needed
    }

    void Start()
    {
        // Choose initial vision
        if (!string.IsNullOrEmpty(defaultVisionName))
        {
            int idx = visions.FindIndex(v => v.name == defaultVisionName);
            if (idx >= 0) SetVision(idx);
            else SetVision(0);
        }
        else
        {
            SetVision(0);
        }
    }

    void OnDisable()
    {
        ReleaseActiveRT();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        ReleaseActiveRT();
    }

    // PUBLIC CONTROL API

    public void NextVision()
    {
        if (visions.Count == 0) return;
        SetVision((currentIndex + 1) % visions.Count);
    }

    public void PrevVision()
    {
        if (visions.Count == 0) return;
        SetVision((currentIndex - 1 + visions.Count) % visions.Count);
    }

    public void SetVisionByName(string name)
    {
        int idx = visions.FindIndex(v => v.name == name);
        if (idx >= 0) SetVision(idx);
        else Debug.LogWarning($"VisionManager: vision named '{name}' not found.");
    }

    public void SetVision(int index)
    {
        if (visions.Count == 0)
        {
            Debug.LogWarning("VisionManager: no visions configured.");
            return;
        }

        index = Mathf.Clamp(index, 0, visions.Count - 1);
        currentIndex = index;
        ApplyVision(visions[index]);
    }

    public int GetCurrentIndex() => currentIndex;
    public Vision GetCurrentVision() => (visions.Count > 0 && currentIndex >= 0) ? visions[currentIndex] : null;

    // INTERNAL

    private void ApplyVision(Vision v)
    {
        // Clean previous
        ReleaseActiveRT();
        if (v == null)
        {
            // fallback to normal
            SetNormal();
            return;
        }

        switch (v.type)
        {
            case VisionType.Normal:
                SetNormal();
                break;

            case VisionType.LowResRenderTexture:
                CreateAndBindRT(v, useMaterial: false);
                break;

            case VisionType.MaterialOnRenderTexture:
                CreateAndBindRT(v, useMaterial: true);
                break;

            default:
                SetNormal();
                break;
        }
    }

    private void SetNormal()
    {
        if (targetCamera != null)
            targetCamera.targetTexture = null;

        if (fullscreenRawImage != null)
        {
            fullscreenRawImage.gameObject.SetActive(false);
            fullscreenRawImage.texture = null;
            fullscreenRawImage.material = null;

            // restore transforms
            ResetRawImageTransform(fullscreenRawImage);
        }
    }

    private void CreateAndBindRT(Vision v, bool useMaterial)
    {
        if (targetCamera == null)
        {
            Debug.LogError("VisionManager: targetCamera is not assigned.");
            return;
        }

        int width = Mathf.Max(1, Mathf.RoundToInt(Screen.width * v.resolutionScale));
        int height = Mathf.Max(1, Mathf.RoundToInt(Screen.height * v.resolutionScale));

        activeRT = new RenderTexture(width, height, 16)
        {
            filterMode = v.filterMode,
            wrapMode = TextureWrapMode.Repeat,
            antiAliasing = 1,
            name = $"VisionRT_{v.name}"
        };
        activeRT.Create();

        // Bind camera to render into RT
        targetCamera.targetTexture = activeRT;

        // Show it on fullscreen RawImage
        if (fullscreenRawImage == null)
        {
            Debug.LogWarning("VisionManager: fullscreenRawImage not assigned. Create a UI RawImage to display the RenderTexture.");
            return;
        }

        fullscreenRawImage.texture = activeRT;
        fullscreenRawImage.gameObject.SetActive(true);

        if (useMaterial && v.overlayMaterial != null)
            fullscreenRawImage.material = v.overlayMaterial;
        else
            fullscreenRawImage.material = null;

        // Apply display transforms (stretch, rotation, UV tiling/offset)
        ApplyDisplayTweaksToRawImage(fullscreenRawImage, v);
    }

    private void ReleaseActiveRT()
    {
        if (activeRT != null)
        {
            if (targetCamera != null && targetCamera.targetTexture == activeRT)
                targetCamera.targetTexture = null;

            if (fullscreenRawImage != null && fullscreenRawImage.texture == activeRT)
            {
                fullscreenRawImage.texture = null;
                fullscreenRawImage.material = null;
            }

            activeRT.Release();
            Destroy(activeRT);
            activeRT = null;
        }

        // Make sure RawImage transform is restored if we had one
        if (fullscreenRawImage != null)
            ResetRawImageTransform(fullscreenRawImage);
    }

    // Apply the user-configured tweaks to the RawImage transform and UVs
    private void ApplyDisplayTweaksToRawImage(RawImage raw, Vision v)
    {
        if (raw == null || v == null) return;

        RectTransform rt = raw.rectTransform;

        // Optionally keep pivot centered so transformations are visually centered
        if (v.keepPivotCentered)
        {
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        // Apply non-uniform scale for trippy stretching
        rt.localScale = new Vector3(v.stretchScaleX, v.stretchScaleY, 1f);

        // Apply rotation around Z
        rt.localEulerAngles = new Vector3(0f, 0f, v.rotationDegrees);

        // Apply UV tiling and offset using uvRect
        // uvRect.width/height are tiling amounts; uvRect.x/y are offsets
        raw.uvRect = new Rect(v.uvOffset.x, v.uvOffset.y, v.uvScale.x, v.uvScale.y);

        // If the assigned material supports custom properties, we could set them here
        // (e.g., a shader reading _MainTexOffset/_MainTexScale). We leave that to the material author.
    }

    private void ResetRawImageTransform(RawImage raw)
    {
        if (raw == null) return;
        RectTransform rt = raw.rectTransform;

        rt.localScale = Vector3.one;
        rt.localEulerAngles = Vector3.zero;
        raw.uvRect = new Rect(0f, 0f, 1f, 1f);

        // If you changed pivot or anchors, restore to default full-stretch anchors
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }
}
