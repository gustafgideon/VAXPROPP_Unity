# Humanoid Character Avatar System for Unity 6

This system provides a complete humanoid character avatar that integrates seamlessly with the existing PlayerController.cs, adding visual representation and animation support without duplicating functionality.

## Features

- **Procedural Humanoid Character**: Basic humanoid mesh created from Unity primitives with proper rigging
- **Animation Integration**: Bridge component that connects PlayerController state to character animations
- **9 Animation States**: Walking, Running, Jumping, Landing, Crouching, Picking up, Putting down, Kicking, Punching
- **Seamless Integration**: Works with existing movement, interaction, and pickup systems
- **First/Third Person Support**: Compatible with PlayerController's view switching
- **Demo Scene**: Complete test environment with pickup objects

## Components

### 1. HumanoidAnimationController.cs
- Bridges PlayerController state to Animator parameters
- Uses reflection to access PlayerController private fields without tight coupling
- Handles animation triggers for actions (jump, attack, pickup/putdown)
- Manages smooth transitions between movement states

### 2. HumanoidCharacterGenerator.cs
- Procedurally creates a basic humanoid character using Unity primitives
- Sets up proper bone structure following Unity humanoid standards
- Creates skinned mesh with bone weights for animation
- Generates materials and basic character appearance

### 3. HumanoidCharacterDemo.cs
- Automated demo setup script
- Creates PlayerController if none exists
- Generates character and integrates with player
- Spawns test objects for pickup testing
- Sets up TempParent system for object holding

### 4. HumanoidAnimatorControllerBuilder.cs
- Helper class for creating runtime animator controllers
- Provides detailed setup instructions for animation assets
- Documents required animation parameters and states

## Quick Setup

1. **Automatic Demo Setup**:
   - Open the `HumanoidCharacterDemo` scene
   - Play the scene - everything will be set up automatically
   - Use WASD to move, Shift to run, Ctrl to crouch, Space to jump
   - Press F to pickup/drop objects, Mouse click to attack
   - Press V to switch between first/third person view

2. **Manual Integration**:
   - Add `HumanoidCharacterDemo` component to any GameObject
   - Call `SetupDemo()` to integrate with existing PlayerController
   - Or manually add the character components to your PlayerController

## Animation Setup (Advanced)

For full animation support, create an Animator Controller asset with:

### Parameters:
- `IsMoving` (Bool) - Controls movement animations
- `MovementSpeed` (Float) - Controls walk/run blend (0=idle, 0.5=walk, 1=run)
- `IsRunning` (Bool) - Running state
- `IsCrouching` (Bool) - Crouching state
- `IsGrounded` (Bool) - Grounded/airborne states
- `IsHoldingObject` (Bool) - Object holding animations
- `JumpTrigger` (Trigger) - Jump animation
- `LandTrigger` (Trigger) - Landing animation
- `PickupTrigger` (Trigger) - Pickup animation
- `PutdownTrigger` (Trigger) - Putdown animation
- `KickTrigger` (Trigger) - Kick attack animation
- `PunchTrigger` (Trigger) - Punch attack animation

### Animation States:
- Idle, Walking, Running (connected via blend tree)
- Jumping, Landing
- Crouching
- PickingUp, PuttingDown
- Kicking, Punching

## Integration with Existing PlayerController

The system integrates with PlayerController through:

1. **Movement State**: Reads `moveInput`, `isRunning`, `isCrouching`, `isGrounded`
2. **Interaction State**: Monitors `isHoldingObject` for pickup animations
3. **Action Triggers**: PlayerController calls animation triggers for jump/attack
4. **TempParent System**: Uses existing object holding system
5. **View Compatibility**: Works with first/third person camera switching

## File Structure

```
Assets/Scripts/Other Scripts/
├── HumanoidAnimationController.cs       # Animation bridge component
├── HumanoidCharacterGenerator.cs        # Procedural character creation
├── HumanoidCharacterDemo.cs             # Demo setup and integration
├── HumanoidAnimatorControllerBuilder.cs # Animation controller helper
└── PlayerController.cs                  # Enhanced with animation triggers

Assets/Scenes/
└── HumanoidCharacterDemo.unity          # Demo scene
```

## Requirements

- Unity 6.0+
- Input System package
- PlayerController.cs with movement and interaction systems
- TempParent.cs for object holding

## Demo Controls

- **Movement**: WASD keys
- **Run**: Hold Shift while moving
- **Crouch**: Hold Ctrl
- **Jump**: Spacebar
- **Attack**: Left mouse button (alternates between kick/punch)
- **Pickup/Drop**: F key
- **Switch View**: V key (first/third person)
- **Zoom**: Mouse scroll wheel (third person only)

## Customization

1. **Character Appearance**: Modify materials in HumanoidCharacterGenerator
2. **Character Proportions**: Adjust size parameters in HumanoidCharacterGenerator
3. **Animation Timing**: Modify transition speeds in HumanoidAnimationController
4. **Test Environment**: Change spawn settings in HumanoidCharacterDemo

## Performance Notes

- Uses reflection to access PlayerController state for loose coupling
- Procedural character is optimized for basic use
- Animation parameter updates are cached using StringToHash for performance
- Minimal overhead when integrated with existing PlayerController

## Troubleshooting

1. **Character not appearing**: Check that HumanoidCharacterDemo.autoSetupOnStart is enabled
2. **Animations not working**: Follow animation setup instructions in console
3. **Pickup not working**: Ensure TempParent is created and SimplePickup components exist
4. **Performance issues**: Check that only one HumanoidAnimationController exists per character

## Future Enhancements

- Replace procedural character with artist-created model
- Add more complex animation blend trees
- Implement animation events for precise timing
- Add facial animations and expressions
- Include inverse kinematics for feet placement