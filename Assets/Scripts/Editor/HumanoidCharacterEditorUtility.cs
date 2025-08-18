using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor utility for creating humanoid characters in the Unity editor
/// Provides menu items and inspector tools for easy character creation
/// </summary>
public class HumanoidCharacterEditorUtility
{
    [MenuItem("VAXPROPP/Create Humanoid Character")]
    public static void CreateHumanoidCharacter()
    {
        // Create the demo setup
        GameObject demoSetup = new GameObject("HumanoidCharacterDemo");
        HumanoidCharacterDemo demo = demoSetup.AddComponent<HumanoidCharacterDemo>();
        
        // Setup the demo
        demo.SetupDemo();
        
        // Select the created objects
        Selection.activeGameObject = demoSetup;
        
        Debug.Log("Humanoid character system created! Check the scene for the demo setup.");
        Debug.Log("Press Play to test the character integration.");
    }
    
    [MenuItem("VAXPROPP/Create Character Only")]
    public static void CreateCharacterOnly()
    {
        // Find existing PlayerController
        PlayerController existingController = Object.FindObjectOfType<PlayerController>();
        
        if (existingController == null)
        {
            Debug.LogError("No PlayerController found in scene. Please create a PlayerController first or use 'Create Humanoid Character' instead.");
            return;
        }
        
        // Create character generator
        GameObject generatorObj = new GameObject("CharacterGenerator");
        HumanoidCharacterGenerator generator = generatorObj.AddComponent<HumanoidCharacterGenerator>();
        
        // Generate the character
        GameObject character = generator.CreateHumanoidCharacter();
        
        // Parent to PlayerController
        character.transform.SetParent(existingController.transform);
        character.transform.localPosition = Vector3.zero;
        character.transform.localRotation = Quaternion.identity;
        
        // Add animation controller
        character.AddComponent<HumanoidAnimationController>();
        
        // Clean up generator
        Object.DestroyImmediate(generatorObj);
        
        // Select the character
        Selection.activeGameObject = character;
        
        Debug.Log("Humanoid character created and attached to existing PlayerController!");
    }
    
    [MenuItem("VAXPROPP/Setup Animation Instructions")]
    public static void ShowAnimationInstructions()
    {
        HumanoidAnimatorControllerBuilder.LogAnimationSetupInstructions();
        Debug.Log("Animation setup instructions logged to console. Check the Console window for detailed steps.");
    }
    
    [MenuItem("GameObject/VAXPROPP/Add Humanoid Character", false, 10)]
    public static void AddHumanoidCharacterToSelected()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogError("Please select a GameObject first.");
            return;
        }
        
        // Create character generator
        GameObject generatorObj = new GameObject("CharacterGenerator");
        HumanoidCharacterGenerator generator = generatorObj.AddComponent<HumanoidCharacterGenerator>();
        
        // Generate the character
        GameObject character = generator.CreateHumanoidCharacter();
        
        // Parent to selected object
        character.transform.SetParent(selected.transform);
        character.transform.localPosition = Vector3.zero;
        character.transform.localRotation = Quaternion.identity;
        
        // Add animation controller
        character.AddComponent<HumanoidAnimationController>();
        
        // Clean up generator
        Object.DestroyImmediate(generatorObj);
        
        // Select the character
        Selection.activeGameObject = character;
        
        Debug.Log($"Humanoid character created and attached to {selected.name}!");
    }
}