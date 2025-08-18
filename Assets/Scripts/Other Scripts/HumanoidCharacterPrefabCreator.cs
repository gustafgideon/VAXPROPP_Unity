using UnityEngine;

/// <summary>
/// Creates a complete humanoid character prefab that can be easily attached to PlayerController
/// </summary>
[CreateAssetMenu(fileName = "HumanoidCharacterPrefab", menuName = "VAXPROPP/Create Humanoid Character Prefab")]
public class HumanoidCharacterPrefabCreator : ScriptableObject
{
    [Header("Character Settings")]
    public float characterHeight = 1.8f;
    public Material characterMaterial;
    
    [Header("Animation Settings")]
    public RuntimeAnimatorController animatorController;
    
    /// <summary>
    /// Creates the humanoid character prefab
    /// </summary>
    public GameObject CreateCharacterPrefab()
    {
        // Create the character generator
        GameObject tempGenerator = new GameObject("TempGenerator");
        HumanoidCharacterGenerator generator = tempGenerator.AddComponent<HumanoidCharacterGenerator>();
        
        // Set the character material if provided
        if (characterMaterial != null)
        {
            var field = typeof(HumanoidCharacterGenerator).GetField("bodyMaterial", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(generator, characterMaterial);
        }
        
        // Generate the character
        GameObject characterPrefab = generator.CreateHumanoidCharacter();
        characterPrefab.name = "HumanoidCharacterPrefab";
        
        // Add the animation controller component
        HumanoidAnimationController animController = characterPrefab.AddComponent<HumanoidAnimationController>();
        
        // Set up the animator with the provided controller
        Animator animator = characterPrefab.GetComponent<Animator>();
        if (animator != null && animatorController != null)
        {
            animator.runtimeAnimatorController = animatorController;
        }
        
        // Clean up the temporary generator
        DestroyImmediate(tempGenerator);
        
        Debug.Log("Humanoid character prefab created successfully!");
        Debug.Log("To use: Drag this prefab as a child of your PlayerController GameObject");
        
        return characterPrefab;
    }
}