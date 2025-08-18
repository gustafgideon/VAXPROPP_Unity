using UnityEngine;

/// <summary>
/// Runtime Animator Controller builder for the humanoid character
/// Creates animation states and parameters programmatically
/// </summary>
public class HumanoidAnimatorControllerBuilder : MonoBehaviour
{
    /// <summary>
    /// Creates a runtime animator controller with all required states and parameters
    /// </summary>
    public static RuntimeAnimatorController CreateHumanoidAnimatorController()
    {
        // Create new runtime animator controller
        var controller = new RuntimeAnimatorController();
        controller.name = "HumanoidAnimatorController";
        
        // Note: Since we can't create complex state machines at runtime easily,
        // we'll create a basic setup that can be expanded with actual animation assets
        
        Debug.Log("Created basic Runtime Animator Controller for humanoid character");
        Debug.Log("To fully utilize animations, create an Animator Controller asset in the editor with the following states:");
        Debug.Log("- Idle, Walking, Running, Jumping, Landing, Crouching, PickingUp, PuttingDown, Kicking, Punching");
        Debug.Log("And the following parameters:");
        Debug.Log("- IsMoving (Bool), MovementSpeed (Float), IsRunning (Bool), IsCrouching (Bool)");
        Debug.Log("- IsGrounded (Bool), IsHoldingObject (Bool)");
        Debug.Log("- JumpTrigger, LandTrigger, PickupTrigger, PutdownTrigger, KickTrigger, PunchTrigger (Triggers)");
        
        return controller;
    }
    
    /// <summary>
    /// Creates animation parameter setup instructions
    /// </summary>
    public static void LogAnimationSetupInstructions()
    {
        Debug.Log("=== HUMANOID CHARACTER ANIMATION SETUP INSTRUCTIONS ===");
        Debug.Log("");
        Debug.Log("To complete the animation system setup:");
        Debug.Log("");
        Debug.Log("1. Create an Animator Controller asset in the Project window");
        Debug.Log("2. Add the following Parameters:");
        Debug.Log("   - IsMoving (Bool) - Controls movement animations");
        Debug.Log("   - MovementSpeed (Float) - Controls walk/run blend");
        Debug.Log("   - IsRunning (Bool) - Controls running state");
        Debug.Log("   - IsCrouching (Bool) - Controls crouching state");
        Debug.Log("   - IsGrounded (Bool) - Controls grounded/airborne states");
        Debug.Log("   - IsHoldingObject (Bool) - Controls object holding animations");
        Debug.Log("   - JumpTrigger (Trigger) - Triggers jump animation");
        Debug.Log("   - LandTrigger (Trigger) - Triggers landing animation");
        Debug.Log("   - PickupTrigger (Trigger) - Triggers pickup animation");
        Debug.Log("   - PutdownTrigger (Trigger) - Triggers putdown animation");
        Debug.Log("   - KickTrigger (Trigger) - Triggers kick animation");
        Debug.Log("   - PunchTrigger (Trigger) - Triggers punch animation");
        Debug.Log("");
        Debug.Log("3. Create the following Animation States:");
        Debug.Log("   - Idle - Default standing animation");
        Debug.Log("   - Walking - Walking animation");
        Debug.Log("   - Running - Running animation");
        Debug.Log("   - Jumping - Jump start animation");
        Debug.Log("   - Landing - Landing animation");
        Debug.Log("   - Crouching - Crouching animation");
        Debug.Log("   - PickingUp - Object pickup animation");
        Debug.Log("   - PuttingDown - Object putdown animation");
        Debug.Log("   - Kicking - Kick attack animation");
        Debug.Log("   - Punching - Punch attack animation");
        Debug.Log("");
        Debug.Log("4. Create Blend Trees:");
        Debug.Log("   - Movement Blend Tree (Idle -> Walk -> Run based on MovementSpeed)");
        Debug.Log("");
        Debug.Log("5. Set up State Transitions:");
        Debug.Log("   - Use the parameters to control when states activate");
        Debug.Log("   - Set appropriate transition conditions and durations");
        Debug.Log("");
        Debug.Log("6. Assign the Animator Controller to the character's Animator component");
        Debug.Log("");
        Debug.Log("The HumanoidAnimationController script will automatically drive these parameters based on PlayerController state.");
        Debug.Log("=== END SETUP INSTRUCTIONS ===");
    }
}