using UnityEngine;

/// <summary>
/// Procedurally creates a basic humanoid character using Unity primitives
/// This provides a simple but properly rigged character for animation
/// </summary>
[System.Serializable]
public class HumanoidCharacterGenerator : MonoBehaviour
{
    [Header("Character Proportions")]
    [SerializeField] private float characterHeight = 1.8f;
    [SerializeField] private float headSize = 0.12f;
    [SerializeField] private float torsoWidth = 0.25f;
    [SerializeField] private float torsoHeight = 0.6f;
    [SerializeField] private float armLength = 0.65f;
    [SerializeField] private float legLength = 0.9f;
    [SerializeField] private float limbThickness = 0.08f;
    
    [Header("Materials")]
    [SerializeField] private Material bodyMaterial;
    [SerializeField] private Material headMaterial;
    [SerializeField] private Material limbMaterial;
    
    private GameObject characterRoot;
    private Transform[] bones;
    
    /// <summary>
    /// Creates the humanoid character with proper bone structure
    /// </summary>
    public GameObject CreateHumanoidCharacter()
    {
        characterRoot = new GameObject("HumanoidCharacter");
        characterRoot.transform.position = transform.position;
        
        CreateSimpleCharacterMesh();
        CreateBoneStructure();
        SetupAnimator();
        
        return characterRoot;
    }
    
    /// <summary>
    /// Creates a simple visual representation using Unity primitives
    /// </summary>
    private void CreateSimpleCharacterMesh()
    {
        // Create character visual representation using primitives
        
        // Head
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(characterRoot.transform);
        head.transform.localPosition = Vector3.up * characterHeight * 0.92f;
        head.transform.localScale = Vector3.one * headSize * 2f;
        
        // Torso
        GameObject torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        torso.name = "Torso";
        torso.transform.SetParent(characterRoot.transform);
        torso.transform.localPosition = Vector3.up * characterHeight * 0.65f;
        torso.transform.localScale = new Vector3(torsoWidth * 2f, torsoHeight * 0.5f, torsoWidth * 1.5f);
        
        // Left Arm
        GameObject leftArm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        leftArm.name = "LeftArm";
        leftArm.transform.SetParent(characterRoot.transform);
        leftArm.transform.localPosition = Vector3.up * characterHeight * 0.6f + Vector3.left * 0.35f;
        leftArm.transform.localRotation = Quaternion.Euler(0, 0, 90);
        leftArm.transform.localScale = new Vector3(limbThickness * 2f, armLength * 0.5f, limbThickness * 2f);
        
        // Right Arm
        GameObject rightArm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        rightArm.name = "RightArm";
        rightArm.transform.SetParent(characterRoot.transform);
        rightArm.transform.localPosition = Vector3.up * characterHeight * 0.6f + Vector3.right * 0.35f;
        rightArm.transform.localRotation = Quaternion.Euler(0, 0, -90);
        rightArm.transform.localScale = new Vector3(limbThickness * 2f, armLength * 0.5f, limbThickness * 2f);
        
        // Left Leg
        GameObject leftLeg = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        leftLeg.name = "LeftLeg";
        leftLeg.transform.SetParent(characterRoot.transform);
        leftLeg.transform.localPosition = Vector3.up * characterHeight * 0.25f + Vector3.left * 0.1f;
        leftLeg.transform.localScale = new Vector3(limbThickness * 2f, legLength * 0.5f, limbThickness * 2f);
        
        // Right Leg
        GameObject rightLeg = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        rightLeg.name = "RightLeg";
        rightLeg.transform.SetParent(characterRoot.transform);
        rightLeg.transform.localPosition = Vector3.up * characterHeight * 0.25f + Vector3.right * 0.1f;
        rightLeg.transform.localScale = new Vector3(limbThickness * 2f, legLength * 0.5f, limbThickness * 2f);
        
        // Apply materials if available
        ApplyMaterials(head, torso, leftArm, rightArm, leftLeg, rightLeg);
    }
    
    private void ApplyMaterials(params GameObject[] parts)
    {
        Material defaultMat = bodyMaterial != null ? bodyMaterial : CreateDefaultMaterial();
        
        foreach (GameObject part in parts)
        {
            MeshRenderer renderer = part.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = defaultMat;
            }
        }
    }
    
    private Material CreateDefaultMaterial()
    {
        Material defaultMat = new Material(Shader.Find("Standard"));
        defaultMat.color = new Color(0.8f, 0.7f, 0.6f); // Skin-like color
        defaultMat.name = "DefaultHumanoidMaterial";
        return defaultMat;
    }
    
    private void CreateBoneStructure()
    {
        // Create bone hierarchy following Unity humanoid standard
        var rootBone = CreateBone("Root", Vector3.zero);
        var hips = CreateBone("Hips", Vector3.up * characterHeight * 0.5f, rootBone);
        var spine = CreateBone("Spine", Vector3.up * characterHeight * 0.55f, hips);
        var chest = CreateBone("Chest", Vector3.up * characterHeight * 0.7f, spine);
        var neck = CreateBone("Neck", Vector3.up * characterHeight * 0.85f, chest);
        var head = CreateBone("Head", Vector3.up * characterHeight * 0.9f, neck);
        
        // Left arm
        var leftShoulder = CreateBone("LeftShoulder", Vector3.up * characterHeight * 0.8f + Vector3.left * 0.1f, chest);
        var leftUpperArm = CreateBone("LeftUpperArm", Vector3.up * characterHeight * 0.75f + Vector3.left * 0.2f, leftShoulder);
        var leftLowerArm = CreateBone("LeftLowerArm", Vector3.up * characterHeight * 0.6f + Vector3.left * 0.35f, leftUpperArm);
        var leftHand = CreateBone("LeftHand", Vector3.up * characterHeight * 0.45f + Vector3.left * 0.5f, leftLowerArm);
        
        // Right arm
        var rightShoulder = CreateBone("RightShoulder", Vector3.up * characterHeight * 0.8f + Vector3.right * 0.1f, chest);
        var rightUpperArm = CreateBone("RightUpperArm", Vector3.up * characterHeight * 0.75f + Vector3.right * 0.2f, rightShoulder);
        var rightLowerArm = CreateBone("RightLowerArm", Vector3.up * characterHeight * 0.6f + Vector3.right * 0.35f, rightUpperArm);
        var rightHand = CreateBone("RightHand", Vector3.up * characterHeight * 0.45f + Vector3.right * 0.5f, rightLowerArm);
        
        // Left leg
        var leftUpperLeg = CreateBone("LeftUpperLeg", Vector3.up * characterHeight * 0.45f + Vector3.left * 0.1f, hips);
        var leftLowerLeg = CreateBone("LeftLowerLeg", Vector3.up * characterHeight * 0.25f + Vector3.left * 0.1f, leftUpperLeg);
        var leftFoot = CreateBone("LeftFoot", Vector3.up * characterHeight * 0.05f + Vector3.left * 0.1f, leftLowerLeg);
        
        // Right leg
        var rightUpperLeg = CreateBone("RightUpperLeg", Vector3.up * characterHeight * 0.45f + Vector3.right * 0.1f, hips);
        var rightLowerLeg = CreateBone("RightLowerLeg", Vector3.up * characterHeight * 0.25f + Vector3.right * 0.1f, rightUpperLeg);
        var rightFoot = CreateBone("RightFoot", Vector3.up * characterHeight * 0.05f + Vector3.right * 0.1f, rightLowerLeg);
        
        // Store bone references
        bones = new Transform[]
        {
            rootBone, hips, spine, chest, neck, head,
            leftShoulder, leftUpperArm, leftLowerArm, leftHand,
            rightShoulder, rightUpperArm, rightLowerArm, rightHand,
            leftUpperLeg, leftLowerLeg, leftFoot,
            rightUpperLeg, rightLowerLeg, rightFoot
        };
    }
    
    private Transform CreateBone(string name, Vector3 position, Transform parent = null)
    {
        GameObject bone = new GameObject(name);
        bone.transform.position = position;
        
        if (parent != null)
        {
            bone.transform.SetParent(parent);
        }
        else
        {
            bone.transform.SetParent(characterRoot.transform);
        }
        
        return bone.transform;
    }
    
    private void SetupAnimator()
    {
        Animator animator = characterRoot.AddComponent<Animator>();
        
        // Create a basic humanoid avatar
        Avatar avatar = CreateHumanoidAvatar();
        animator.avatar = avatar;
        
        Debug.Log("Humanoid character created with basic avatar setup");
    }
    
    private Avatar CreateHumanoidAvatar()
    {
        // Create a basic avatar - this will be a generic avatar since we're using primitives
        Avatar avatar = AvatarBuilder.BuildGenericAvatar(characterRoot, "");
        avatar.name = "HumanoidCharacterAvatar";
        
        return avatar;
    }
}