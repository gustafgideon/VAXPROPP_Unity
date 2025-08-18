using UnityEngine;

/// <summary>
/// Demo setup script for the humanoid character avatar system
/// Creates a test scene with character integration and pickup objects
/// </summary>
public class HumanoidCharacterDemo : MonoBehaviour
{
    [Header("Demo Setup")]
    [SerializeField] private bool autoSetupOnStart = true;
    [SerializeField] private GameObject playerControllerPrefab;
    [SerializeField] private Material characterMaterial;
    [SerializeField] private Material pickupObjectMaterial;
    
    [Header("Test Objects")]
    [SerializeField] private int numberOfTestCubes = 5;
    [SerializeField] private float spawnRadius = 10f;
    [SerializeField] private Vector3 spawnCenter = Vector3.zero;
    
    private GameObject playerInstance;
    private GameObject characterInstance;
    
    void Start()
    {
        if (autoSetupOnStart)
        {
            SetupDemo();
        }
    }
    
    /// <summary>
    /// Sets up the complete demo scene
    /// </summary>
    public void SetupDemo()
    {
        Debug.Log("Setting up Humanoid Character Demo...");
        
        SetupPlayerController();
        SetupHumanoidCharacter();
        SetupTestObjects();
        SetupTempParent();
        
        Debug.Log("Demo setup complete!");
    }
    
    /// <summary>
    /// Creates or finds the PlayerController instance
    /// </summary>
    private void SetupPlayerController()
    {
        // Try to find existing PlayerController
        PlayerController existingController = FindObjectOfType<PlayerController>();
        
        if (existingController != null)
        {
            playerInstance = existingController.gameObject;
            Debug.Log("Found existing PlayerController: " + playerInstance.name);
        }
        else if (playerControllerPrefab != null)
        {
            // Create from prefab
            playerInstance = Instantiate(playerControllerPrefab);
            playerInstance.name = "PlayerController";
            Debug.Log("Created PlayerController from prefab");
        }
        else
        {
            // Create basic PlayerController setup
            playerInstance = CreateBasicPlayerController();
            Debug.Log("Created basic PlayerController");
        }
    }
    
    /// <summary>
    /// Creates a basic PlayerController setup if no prefab is provided
    /// </summary>
    private GameObject CreateBasicPlayerController()
    {
        GameObject player = new GameObject("PlayerController");
        
        // Add required components
        CharacterController cc = player.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.5f;
        cc.center = Vector3.up;
        
        PlayerInput playerInput = player.AddComponent<PlayerInput>();
        PlayerController controller = player.AddComponent<PlayerController>();
        
        // Create camera setup
        GameObject cameraHolder = new GameObject("CameraHolder");
        cameraHolder.transform.SetParent(player.transform);
        cameraHolder.transform.localPosition = Vector3.zero;
        
        GameObject firstPersonPos = new GameObject("FirstPersonPosition");
        firstPersonPos.transform.SetParent(cameraHolder.transform);
        firstPersonPos.transform.localPosition = new Vector3(0, 1.6f, 0);
        
        GameObject thirdPersonPos = new GameObject("ThirdPersonPosition");
        thirdPersonPos.transform.SetParent(cameraHolder.transform);
        thirdPersonPos.transform.localPosition = new Vector3(0, 1.6f, -5f);
        
        // Create camera
        GameObject cameraObj = new GameObject("PlayerCamera");
        cameraObj.transform.SetParent(firstPersonPos.transform);
        cameraObj.transform.localPosition = Vector3.zero;
        Camera camera = cameraObj.AddComponent<Camera>();
        
        // Set up audio listener
        cameraObj.AddComponent<AudioListener>();
        
        return player;
    }
    
    /// <summary>
    /// Creates and attaches the humanoid character to the PlayerController
    /// </summary>
    private void SetupHumanoidCharacter()
    {
        if (playerInstance == null)
        {
            Debug.LogError("Cannot setup character - no PlayerController instance!");
            return;
        }
        
        // Create character generator
        GameObject generatorObj = new GameObject("CharacterGenerator");
        HumanoidCharacterGenerator generator = generatorObj.AddComponent<HumanoidCharacterGenerator>();
        
        // Set materials if provided
        if (characterMaterial != null)
        {
            // Set material via reflection
            var field = typeof(HumanoidCharacterGenerator).GetField("bodyMaterial", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(generator, characterMaterial);
        }
        
        // Generate the character
        characterInstance = generator.CreateHumanoidCharacter();
        
        // Parent to PlayerController
        characterInstance.transform.SetParent(playerInstance.transform);
        characterInstance.transform.localPosition = Vector3.zero;
        characterInstance.transform.localRotation = Quaternion.identity;
        
        // Add animation controller
        HumanoidAnimationController animController = characterInstance.AddComponent<HumanoidAnimationController>();
        
        // Create animator controller asset
        CreateAnimatorController(characterInstance.GetComponent<Animator>());
        
        // Clean up generator
        DestroyImmediate(generatorObj);
        
        Debug.Log("Humanoid character created and attached to PlayerController");
    }
    
    /// <summary>
    /// Creates a basic Animator Controller for the character
    /// </summary>
    private void CreateAnimatorController(Animator animator)
    {
        if (animator == null) return;
        
        // Create runtime animator controller
        RuntimeAnimatorController runtimeController = HumanoidAnimatorControllerBuilder.CreateHumanoidAnimatorController();
        animator.runtimeAnimatorController = runtimeController;
        
        // Log setup instructions for completing the animation system
        HumanoidAnimatorControllerBuilder.LogAnimationSetupInstructions();
        
        Debug.Log("Basic Animator Controller created - See console for complete setup instructions");
    }
    
    /// <summary>
    /// Creates test objects for pickup testing
    /// </summary>
    private void SetupTestObjects()
    {
        for (int i = 0; i < numberOfTestCubes; i++)
        {
            CreateTestCube(i);
        }
        
        Debug.Log($"Created {numberOfTestCubes} test objects for pickup testing");
    }
    
    /// <summary>
    /// Creates a single test cube for pickup
    /// </summary>
    private void CreateTestCube(int index)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = $"TestCube_{index}";
        
        // Position randomly around spawn center
        Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
        Vector3 spawnPos = spawnCenter + new Vector3(randomCircle.x, 1f, randomCircle.y);
        cube.transform.position = spawnPos;
        
        // Add random rotation and scale variation
        cube.transform.rotation = Random.rotation;
        float scale = Random.Range(0.5f, 1.5f);
        cube.transform.localScale = Vector3.one * scale;
        
        // Add SimplePickup component
        SimplePickup pickup = cube.AddComponent<SimplePickup>();
        
        // Ensure Rigidbody and Collider
        Rigidbody rb = cube.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = cube.AddComponent<Rigidbody>();
        }
        
        // Set material if provided
        if (pickupObjectMaterial != null)
        {
            MeshRenderer renderer = cube.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = pickupObjectMaterial;
            }
        }
        else
        {
            // Create colorful material for easy identification
            MeshRenderer renderer = cube.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material colorMat = new Material(Shader.Find("Standard"));
                colorMat.color = Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.8f, 1f);
                colorMat.name = $"TestCubeMaterial_{index}";
                renderer.material = colorMat;
            }
        }
    }
    
    /// <summary>
    /// Sets up TempParent for object holding
    /// </summary>
    private void SetupTempParent()
    {
        // Check if TempParent already exists
        if (TempParent.Instance != null)
        {
            Debug.Log("TempParent already exists");
            return;
        }
        
        // Create TempParent
        GameObject tempParentObj = new GameObject("TempParent");
        tempParentObj.AddComponent<TempParent>();
        
        // Position it relative to player camera for first-person holding
        if (playerInstance != null)
        {
            Camera playerCamera = playerInstance.GetComponentInChildren<Camera>();
            if (playerCamera != null)
            {
                tempParentObj.transform.SetParent(playerCamera.transform);
                tempParentObj.transform.localPosition = new Vector3(0.3f, -0.3f, 0.8f);
            }
        }
        
        Debug.Log("TempParent created for object holding");
    }
    
    /// <summary>
    /// Cleanup method for demo
    /// </summary>
    public void CleanupDemo()
    {
        // Remove all test cubes
        GameObject[] testCubes = GameObject.FindGameObjectsWithTag("Untagged");
        foreach (GameObject obj in testCubes)
        {
            if (obj.name.StartsWith("TestCube_"))
            {
                DestroyImmediate(obj);
            }
        }
        
        Debug.Log("Demo cleanup complete");
    }
    
    /// <summary>
    /// Public method to regenerate character (useful for testing)
    /// </summary>
    [ContextMenu("Regenerate Character")]
    public void RegenerateCharacter()
    {
        if (characterInstance != null)
        {
            DestroyImmediate(characterInstance);
        }
        
        SetupHumanoidCharacter();
    }
    
    /// <summary>
    /// Public method to regenerate test objects
    /// </summary>
    [ContextMenu("Regenerate Test Objects")]
    public void RegenerateTestObjects()
    {
        CleanupDemo();
        SetupTestObjects();
    }
}