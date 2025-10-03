using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Run very late by default so we follow camera motion after it settles
[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public class TerminalDataFrame : MonoBehaviour
{
    public enum UpdateDriver
    {
        Update,
        LateUpdate,
        CameraPreRender
    }

    [Header("Update Timing")]
    [Tooltip("When to update the frame so it follows the camera without causing jitter. 'LateUpdate' is recommended (default). 'CameraPreRender' updates right before rendering and works with both Built-in and SRP.")]
    public UpdateDriver updateDriver = UpdateDriver.LateUpdate;

    [Header("Target")]
    [Tooltip("Object to frame. If not set, will default to this GameObject's transform.")]
    public Transform target;
    
    [Header("Frame Size")]
    [Tooltip("Uniform scale for the frame (does not change line thickness). 1 = original size.")]
    [Range(0.2f, 3f)]
    public float frameSize = 1f;

    [Header("Frame Settings")]
    public Color frameColor = Color.green;
    public Color textColor = Color.white;
    [Range(0.001f, 0.1f)]
    public float frameThickness = 0.01f;
    [Range(0.5f, 3f)]
    public float framePadding = 1.2f;
    [Tooltip("Extra distance beyond the object's front surface when 'Place In Front' is enabled, or distance from center when disabled.")]
    [Range(0.0f, 2f)]
    public float frameDistance = 0.2f;

    [Header("Occlusion")]
    [Tooltip("If true, positions the frame in front of the object's surface (relative to the camera) to avoid being clipped by the object.")]
    public bool placeInFrontOfTarget = true;
    [Tooltip("Extra margin in meters added to keep the frame clearly in front of the surface, preventing z-fighting/occlusion.")]
    [Range(0f, 0.2f)]
    public float frontMargin = 0.02f;

    [Header("Frame Shape")]
    [Range(0.5f, 3f)]
    public float frameHeight = 1.5f;
    [Range(0.5f, 3f)]
    public float frameWidth = 1f;
    [Tooltip("If true, uses frameWidth/frameHeight. If false, sizes to target bounds * framePadding.")]
    public bool useFixedSize = false;

    [Header("Text Settings")]
    [Range(10, 100)]
    public int textSize = 25;
    [Range(-2f, 2f)]
    public float textOffsetX = 0.3f;
    [Range(-2f, 2f)]
    public float textOffsetY = 0.4f;
    [Range(0.05f, 1f)]
    public float textUpdateSpeed = 0.15f;
    
    [Header("Text Length")]
    [Tooltip("Minimum number of characters in the random text.")]
    [Range(1, 20)]
    public int minTextLength = 4;
    [Tooltip("Maximum number of characters in the random text.")]
    [Range(1, 30)]
    public int maxTextLength = 12;
    
    [Header("Special Text")]
    [Tooltip("Chance (0-1) that 'VAXPROPP' appears in the text instead of random data.")]
    [Range(0f, 1f)]
    public float vaxproppChance = 0.3f;
    [Tooltip("Chance (0-1) that 'VAXPROPP' appears mirrored when it does appear.")]
    [Range(0f, 1f)]
    public float mirrorChance = 0.5f;

    [Header("Activation by Distance")]
    public bool useDistanceCheck = false;
    [Tooltip("Hide the frame if the camera/player is closer than this.")]
    [Range(0f, 200f)]
    public float showMinDistance = 0f;
    [Tooltip("Hide the frame if the camera/player is farther than this.")]
    [Range(0.01f, 1000f)]
    public float showMaxDistance = 15f;
    [Tooltip("If enabled, finds an object with this tag to measure distance from; otherwise uses the main Camera.")]
    public bool usePlayerTagForDistance = false;
    public string playerTag = "Player";

    [Header("Editor Preview")]
    public bool showInEditor = true;
    public Transform previewTarget;

    // Private variables
    private Bounds originalObjectBounds;
    private LineRenderer[] frameLines;
    private GameObject textDisplay;
    private TextMesh textMesh;
    private Coroutine dataUpdateCoroutine;
    private Transform targetObject;
    private Camera playerCamera;

    // Distance gating
    private Transform distanceTarget; // camera or player
    private bool isCurrentlyVisible = true;

    // A detached runtime root so size never inherits scaling from target/parents
    private Transform frameRoot;
    private bool initialized;

    // Enhanced data generation with more characters and symbols
    private readonly string[] dataCharacters = { 
        "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", 
        "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", 
        "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
        "!", "@", "#", "$", "%", "&", "*", "+", "-", "=", "?", "/", "\\",
        "[", "]", "{", "}", "(", ")", "<", ">", "^", "~", "|", "_", ":",
        ";", ".", ",", "'", "\"", "`"
    };

    // Special text constant
    private const string SPECIAL_TEXT = "VAXPROPP";

    // SRP/Built-in render callbacks (so we can update right before render if desired)
    private bool usingSRP => GraphicsSettings.currentRenderPipeline != null;

    void Awake()
    {
        // Resolve target
        if (target == null)
        {
            target = previewTarget != null ? previewTarget : transform;
        }
    }

    void OnEnable()
    {
        TrySubscribeRenderCallbacks();
    }

    void OnDisable()
    {
        UnsubscribeRenderCallbacks();
    }

    void Start()
    {
        // Auto-initialize at runtime for play mode visibility
        Initialize(textUpdateSpeed, target);
    }

    // Backward-compatible signature (size is not used; kept for compatibility)
    public void Initialize(float size, float speed, Transform tgt)
    {
        textUpdateSpeed = speed;
        Initialize(speed, tgt);
    }

    // New internal initializer
    public void Initialize(float speed, Transform tgt)
    {
        if (initialized) return;
        initialized = true;

        textUpdateSpeed = speed;
        targetObject = tgt != null ? tgt : transform;

        // Find camera (we only read from it; never write to it)
        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            playerCamera = FindObjectOfType<Camera>();
        }

        // Distance source (player or camera)
        if (usePlayerTagForDistance)
        {
            var playerObj = GameObject.FindWithTag(playerTag);
            if (playerObj != null)
            {
                distanceTarget = playerObj.transform;
            }
            else
            {
                Debug.LogWarning($"TerminalDataFrame on {gameObject.name}: No object found with tag '{playerTag}'. Falling back to Camera for distance.");
            }
        }
        if (distanceTarget == null && playerCamera != null)
        {
            distanceTarget = playerCamera.transform;
        }
        if (useDistanceCheck && distanceTarget == null)
        {
            Debug.LogWarning($"TerminalDataFrame on {gameObject.name}: No distance target (camera/player) found. Distance check disabled.");
            useDistanceCheck = false;
        }

        CalculateOriginalObjectBounds();
        CreateOrEnsureFrameRoot();
        CreateSharpRectangularFrame();
        CreateCornerText();
        StartContinuousDataUpdates();
        UpdateAllNow();
        UpdateVisibilityNow(force: true);
    }

    void TrySubscribeRenderCallbacks()
    {
        UnsubscribeRenderCallbacks();
        if (updateDriver != UpdateDriver.CameraPreRender) return;

        if (usingSRP)
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }
        else
        {
            Camera.onPreCull += OnCameraPreCull;
        }
    }

    void UnsubscribeRenderCallbacks()
    {
        if (usingSRP)
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        }
        else
        {
            Camera.onPreCull -= OnCameraPreCull;
        }
    }

    void CreateOrEnsureFrameRoot()
    {
        if (frameRoot == null)
        {
            var rootGO = new GameObject($"[TerminalDataFrame] {targetObject.name}");
            frameRoot = rootGO.transform;
            frameRoot.position = targetObject.position;
            frameRoot.rotation = Quaternion.identity;
            frameRoot.localScale = Vector3.one; // never inherit arbitrary scaling
            // Keep it out of the target/camera hierarchies to avoid side-effects
            frameRoot.SetParent(null, worldPositionStays: true);
        }
    }

    void CalculateOriginalObjectBounds()
    {
        if (targetObject == null) return;

        if (useFixedSize)
        {
            originalObjectBounds = new Bounds(targetObject.position, new Vector3(frameWidth, frameHeight, frameWidth));
        }
        else
        {
            Renderer[] renderers = targetObject.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                originalObjectBounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers)
                {
                    originalObjectBounds.Encapsulate(renderer.bounds);
                }
            }
            else
            {
                originalObjectBounds = new Bounds(targetObject.position, Vector3.one);
            }

            originalObjectBounds.size *= framePadding;
        }

        originalObjectBounds.center = targetObject.position;
    }

    void CreateSharpRectangularFrame()
    {
        // Cleanup if re-init
        if (frameLines != null)
        {
            for (int i = 0; i < frameLines.Length; i++)
            {
                if (frameLines[i] != null)
                {
                    DestroySafe(frameLines[i].gameObject);
                }
            }
        }

        frameLines = new LineRenderer[4];
        string[] lineNames = { "Top", "Right", "Bottom", "Left" };

        for (int i = 0; i < 4; i++)
        {
            GameObject lineObj = new GameObject($"FrameLine_{lineNames[i]}");
            lineObj.transform.SetParent(frameRoot, false);

            LineRenderer line = lineObj.AddComponent<LineRenderer>();
            line.material = CreateSharpLineMaterial();
            line.startColor = frameColor;
            line.endColor = frameColor;
            line.startWidth = frameThickness;
            line.endWidth = frameThickness;
            line.positionCount = 2;
            line.useWorldSpace = false;

            // View alignment so the frame is always facing the camera
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Tile;

            frameLines[i] = line;
        }
    }

    Material CreateSharpLineMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.color = frameColor;

        if (mat.HasProperty("_TintColor"))
            mat.SetColor("_TintColor", frameColor);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", frameColor);
        if (mat.HasProperty("_MainColor"))
            mat.SetColor("_MainColor", frameColor);

        // Render after opaques to reduce occlusion risk; still depth-tested.
        mat.renderQueue = 3000; // Transparent queue

        return mat;
    }

    void UpdateFrameToFaceCamera(Camera cam)
    {
        if (cam == null || frameLines == null || frameRoot == null || targetObject == null) return;

        Vector3 targetCenter = targetObject.position;
        Vector3 directionToCamera = (cam.transform.position - targetCenter).normalized;

        float distanceFromCenter;
        if (placeInFrontOfTarget)
        {
            // Compute distance to the object's front surface along the camera direction
            distanceFromCenter = GetFrontSurfaceDistance(originalObjectBounds, directionToCamera) + frontMargin + frameDistance;
        }
        else
        {
            distanceFromCenter = frameDistance;
        }

        Vector3 framePosition = targetCenter + directionToCamera * distanceFromCenter;

        frameRoot.position = framePosition;
        frameRoot.LookAt(cam.transform.position);

        UpdateRectangularFrameLines();
        UpdateFrameColors();
    }

    // Support function: distance from bounds center to its front face along a direction
    float GetFrontSurfaceDistance(Bounds bounds, Vector3 dir)
    {
        Vector3 e = bounds.extents;
        Vector3 a = new Vector3(Mathf.Abs(dir.x), Mathf.Abs(dir.y), Mathf.Abs(dir.z));
        // Support mapping of AABB along dir
        return Vector3.Dot(e, a);
    }

    void UpdateFrameColors()
    {
        if (frameLines == null) return;

        for (int i = 0; i < frameLines.Length; i++)
        {
            if (frameLines[i] != null)
            {
                frameLines[i].startColor = frameColor;
                frameLines[i].endColor = frameColor;
                if (frameLines[i].material != null)
                {
                    frameLines[i].material.color = frameColor;
                }
            }
        }
    }

    void UpdateRectangularFrameLines()
    {
        if (frameLines == null) return;

        float width = useFixedSize ? frameWidth : originalObjectBounds.size.x;
        float height = useFixedSize ? frameHeight : originalObjectBounds.size.y;

        // Apply uniform frame size scale
        width *= frameSize;
        height *= frameSize;

        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;

        frameLines[0].SetPosition(0, new Vector3(-halfWidth,  halfHeight, 0));
        frameLines[0].SetPosition(1, new Vector3( halfWidth,  halfHeight, 0));

        frameLines[1].SetPosition(0, new Vector3( halfWidth,  halfHeight, 0));
        frameLines[1].SetPosition(1, new Vector3( halfWidth, -halfHeight, 0));

        frameLines[2].SetPosition(0, new Vector3( halfWidth, -halfHeight, 0));
        frameLines[2].SetPosition(1, new Vector3(-halfWidth, -halfHeight, 0));

        frameLines[3].SetPosition(0, new Vector3(-halfWidth, -halfHeight, 0));
        frameLines[3].SetPosition(1, new Vector3(-halfWidth,  halfHeight, 0));
    }

    void CreateCornerText()
    {
        // Cleanup if re-init
        if (textDisplay != null)
        {
            DestroySafe(textDisplay);
            textDisplay = null;
            textMesh = null;
        }

        GameObject textObj = new GameObject("FrameText");
        textObj.transform.SetParent(frameRoot, false);

        textMesh = textObj.AddComponent<TextMesh>();
        textMesh.text = GenerateTextContent();
        textMesh.fontSize = textSize;
        textMesh.color = textColor;
        textMesh.anchor = TextAnchor.MiddleLeft;

        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null)
        {
            defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        if (defaultFont != null)
        {
            textMesh.font = defaultFont;
        }

        textDisplay = textObj;
        UpdateTextPosition();
    }

    void UpdateTextPosition()
    {
        if (textDisplay == null) return;

        float width = useFixedSize ? frameWidth : originalObjectBounds.size.x;
        float height = useFixedSize ? frameHeight : originalObjectBounds.size.y;

        // Apply uniform frame size scale
        width *= frameSize;
        height *= frameSize;

        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;

        // Scale the offset with frameSize so the margin stays proportional
        Vector3 textLocalPos = new Vector3(
            halfWidth + textOffsetX * frameSize,
            halfHeight + textOffsetY * frameSize,
            0
        );

        textDisplay.transform.localPosition = textLocalPos;
        textDisplay.transform.localRotation = Quaternion.identity;

        // Fixed world-space size (intentionally not scaled by frameSize)
        float textScale = 0.01f;
        textDisplay.transform.localScale = Vector3.one * textScale;

        if (textMesh != null)
        {
            textMesh.color = textColor;
            textMesh.fontSize = textSize;
        }
    }

    // Centralized per-frame tick (we only READ from the camera)
    void Tick(Camera camToUse)
    {
        if (!initialized) return;

        // Keep bounds centered if target moves
        originalObjectBounds.center = targetObject.position;

        // Update visibility first
        UpdateVisibilityNow(force: false);
        if (!isCurrentlyVisible) return;

        UpdateFrameToFaceCamera(camToUse);
        UpdateTextPosition();
    }

    void Update()
    {
        if (updateDriver == UpdateDriver.Update)
        {
            Tick(playerCamera);
        }
    }

    void LateUpdate()
    {
        if (updateDriver == UpdateDriver.LateUpdate)
        {
            Tick(playerCamera);
        }
    }

    // Built-in pipeline pre-cull
    void OnCameraPreCull(Camera cam)
    {
        if (updateDriver != UpdateDriver.CameraPreRender) return;
        if (playerCamera != null && cam != playerCamera) return; // only follow our main camera
        Tick(cam);
    }

    // SRP begin-camera-rendering
    void OnBeginCameraRendering(ScriptableRenderContext ctx, Camera cam)
    {
        if (updateDriver != UpdateDriver.CameraPreRender) return;
        if (playerCamera != null && cam != playerCamera) return; // only follow our main camera
        Tick(cam);
    }

    void UpdateVisibilityNow(bool force)
    {
        bool shouldShow = !useDistanceCheck || IsInShowRange();
        if (frameRoot == null) return;

        if (force || shouldShow != isCurrentlyVisible)
        {
            frameRoot.gameObject.SetActive(shouldShow);
            isCurrentlyVisible = shouldShow;
        }
    }

    bool IsInShowRange()
    {
        if (!useDistanceCheck) return true;
        if (distanceTarget == null || targetObject == null) return true;

        float d = Vector3.Distance(distanceTarget.position, targetObject.position);
        return d >= showMinDistance && d <= showMaxDistance;
    }

    void StartContinuousDataUpdates()
    {
        if (dataUpdateCoroutine != null)
            StopCoroutine(dataUpdateCoroutine);

        dataUpdateCoroutine = StartCoroutine(ContinuousUpdateLoop());
    }

    IEnumerator ContinuousUpdateLoop()
    {
        while (this != null && gameObject != null && gameObject.activeInHierarchy)
        {
            if (textMesh != null)
            {
                textMesh.text = GenerateTextContent();
            }

            if (frameLines != null && frameLines.Length > 0 && Random.value < 0.15f)
            {
                Color flickerColor = Random.value < 0.7f ? Color.red : frameColor;
                for (int i = 0; i < frameLines.Length; i++)
                {
                    if (frameLines[i] != null)
                    {
                        frameLines[i].startColor = flickerColor;
                        frameLines[i].endColor = flickerColor;
                        if (frameLines[i].material != null)
                            frameLines[i].material.color = flickerColor;
                    }
                }
            }

            yield return new WaitForSeconds(textUpdateSpeed);
        }
    }

    // Enhanced text content generation
    string GenerateTextContent()
    {
        // Check if we should show VAXPROPP instead of random data
        if (Random.value < vaxproppChance)
        {
            return GenerateVaxproppText();
        }
        else
        {
            // Generate random data with adjustable length
            int length = Random.Range(minTextLength, maxTextLength + 1);
            return GenerateRandomData(length);
        }
    }

    string GenerateVaxproppText()
    {
        string text = SPECIAL_TEXT;
        
        // Check if we should mirror it
        if (Random.value < mirrorChance)
        {
            text = MirrorText(text);
        }
        
        // Sometimes add random characters before/after
        if (Random.value < 0.4f)
        {
            int prefixLength = Random.Range(1, 4);
            string prefix = GenerateRandomData(prefixLength);
            text = prefix + text;
        }
        
        if (Random.value < 0.4f)
        {
            int suffixLength = Random.Range(1, 4);
            string suffix = GenerateRandomData(suffixLength);
            text = text + suffix;
        }
        
        return text;
    }

    string MirrorText(string input)
    {
        char[] chars = input.ToCharArray();
        System.Array.Reverse(chars);
        return new string(chars);
    }

    string GenerateRandomData(int length)
    {
        string result = "";
        for (int i = 0; i < length; i++)
        {
            result += dataCharacters[Random.Range(0, dataCharacters.Length)];
        }
        return result;
    }

    void OnDestroy()
    {
        if (dataUpdateCoroutine != null)
        {
            StopCoroutine(dataUpdateCoroutine);
            dataUpdateCoroutine = null;
        }

        UnsubscribeRenderCallbacks();

        // Clean up the runtime frame root
        if (Application.isPlaying)
        {
            DestroySafe(frameRoot != null ? frameRoot.gameObject : null);
        }
        else
        {
#if UNITY_EDITOR
            DestroyImmediateSafe(frameRoot != null ? frameRoot.gameObject : null);
#endif
        }
    }

    void UpdateAllNow()
    {
        CalculateOriginalObjectBounds();
        UpdateFrameToFaceCamera(playerCamera);
        UpdateTextPosition();
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!showInEditor) return;

        // Distance gizmos
        Transform pivot = previewTarget != null ? previewTarget : (target != null ? target : transform);
        if (pivot != null && useDistanceCheck)
        {
            Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(pivot.position, showMaxDistance);
            if (showMinDistance > 0f)
            {
                Gizmos.color = new Color(0.2f, 0.6f, 1f, 1f);
                Gizmos.DrawWireSphere(pivot.position, showMinDistance);
            }
        }

        DrawEditorFramePreview();
    }

    void OnDrawGizmos()
    {
        if (!showInEditor) return;

        bool isSelected = false;
        if (UnityEditor.Selection.activeGameObject == gameObject)
        {
            isSelected = true;
        }
        else
        {
            foreach (GameObject obj in UnityEditor.Selection.gameObjects)
            {
                if (obj == gameObject)
                {
                    isSelected = true;
                    break;
                }
            }
        }

        if (!isSelected) return;
        OnDrawGizmosSelected();
    }

    void DrawEditorFramePreview()
    {
        Transform preview = previewTarget != null ? previewTarget : (target != null ? target : transform);
        if (preview == null) return;

        Gizmos.color = frameColor;

        Camera sceneCamera = null;
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            sceneCamera = sceneView.camera;
        }

        Vector3 targetPosition = preview.position;
        Vector3 directionToCamera;
        if (sceneCamera == null)
        {
            directionToCamera = Vector3.forward;
        }
        else
        {
            directionToCamera = (sceneCamera.transform.position - targetPosition).normalized;
        }

        // Estimate bounds for preview
        Bounds previewBounds = GetEditorBounds(preview, targetPosition);
        float front = GetFrontSurfaceDistance(previewBounds, directionToCamera);
        float distanceFromCenter = (placeInFrontOfTarget ? (front + frontMargin + frameDistance) : frameDistance);

        Vector3 framePos = targetPosition + directionToCamera * distanceFromCenter;
        Quaternion rotation = sceneCamera == null ? Quaternion.identity : Quaternion.LookRotation(directionToCamera);

        float frameW = useFixedSize ? frameWidth : (previewBounds.size.x);
        float frameH = useFixedSize ? frameHeight : (previewBounds.size.y);

        DrawFrameRectangle(framePos, rotation, frameW, frameH);

        Vector3 textPos = framePos + rotation * new Vector3(frameW * 0.5f + textOffsetX, frameH * 0.5f + textOffsetY, 0);

        Gizmos.color = textColor;
        Gizmos.DrawWireSphere(textPos, 0.05f);

        UnityEditor.Handles.color = textColor;
        GUIStyle style = new GUIStyle();
        style.normal.textColor = textColor;
        style.fontSize = Mathf.Clamp(textSize / 2, 8, 20);
        style.fontStyle = FontStyle.Bold;
        UnityEditor.Handles.Label(textPos, "VAXPROPP", style);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(targetPosition, framePos);
    }

    Bounds GetEditorBounds(Transform t, Vector3 fallbackCenter)
    {
        if (useFixedSize)
        {
            return new Bounds(fallbackCenter, new Vector3(frameWidth, frameHeight, frameWidth));
        }

        Renderer[] renderers = t.GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            b.size *= framePadding;
            b.center = t.position;
            return b;
        }

        return new Bounds(fallbackCenter, Vector3.one * framePadding);
    }

    void DrawFrameRectangle(Vector3 position, Quaternion rotation, float width, float height)
    {
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;

        Vector3[] corners = new Vector3[4];
        corners[0] = position + rotation * new Vector3(-halfWidth, halfHeight, 0);
        corners[1] = position + rotation * new Vector3(halfWidth, halfHeight, 0);
        corners[2] = position + rotation * new Vector3(halfWidth, -halfHeight, 0);
        corners[3] = position + rotation * new Vector3(-halfWidth, -halfHeight, 0);

        Gizmos.DrawLine(corners[0], corners[1]);
        Gizmos.DrawLine(corners[1], corners[2]);
        Gizmos.DrawLine(corners[2], corners[3]);
        Gizmos.DrawLine(corners[3], corners[0]);
    }

    void OnValidate()
    {
        frontMargin = Mathf.Max(0f, frontMargin);
        if (showMaxDistance < showMinDistance)
            showMaxDistance = showMinDistance;

        // Ensure min <= max for text length
        if (minTextLength > maxTextLength)
            maxTextLength = minTextLength;

        // Clamp the VAXPROPP parameters
        vaxproppChance = Mathf.Clamp01(vaxproppChance);
        mirrorChance = Mathf.Clamp01(mirrorChance);

        // Re-subscribe if timing changed in inspector
        if (enabled)
        {
            TrySubscribeRenderCallbacks();
        }

        if (!Application.isPlaying) return;
        if (!initialized) return;

        CalculateOriginalObjectBounds();
        UpdateFrameColors();

        if (textMesh != null)
        {
            textMesh.color = textColor;
            textMesh.fontSize = textSize;
        }

        UpdateAllNow();
        UpdateVisibilityNow(force: true);
    }
#endif

    // Safe destroy helpers
    private void DestroySafe(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else
        {
#if UNITY_EDITOR
            DestroyImmediate(obj);
#endif
        }
    }

#if UNITY_EDITOR
    private void DestroyImmediateSafe(Object obj)
    {
        if (obj == null) return;
        DestroyImmediate(obj);
    }
#endif
}