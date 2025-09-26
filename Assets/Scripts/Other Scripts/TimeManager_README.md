# TimeManager Configuration Guide

## Overview
The TimeManager script now supports configurable day/night cycle timing through the Unity Inspector.

## New Features

### Time Speed Multiplier
- **Field**: `Time Speed Multiplier`
- **Default**: 1.0
- **Purpose**: Controls how fast the day/night cycle progresses
- **Examples**:
  - `1.0` = Normal speed (1 real second = 1 game minute)
  - `5.0` = 5x faster (1 real second = 5 game minutes)  
  - `0.5` = Half speed (2 real seconds = 1 game minute)

### Configurable Time Phase Hours
All time phases are now configurable through these Inspector fields:

- **Sunrise Hour**: When sunrise begins (default: 6)
- **Day Hour**: When full day begins (default: 8)
- **Sunset Hour**: When sunset begins (default: 18)
- **Night Hour**: When full night begins (default: 22)

## Usage Instructions

1. **Select the GameObject** with the TimeManager component
2. **In the Inspector**, you'll see organized sections:
   - Time Speed Settings
   - Time Phase Hours
   - Skybox Textures
   - Light Gradients
   - Global Light

3. **Adjust values** as needed:
   - Modify `Time Speed Multiplier` to change cycle speed
   - Modify hour values to change when each phase occurs

## Examples

### Fast Day/Night Cycle for Testing
```
Time Speed Multiplier: 10.0
Sunrise Hour: 6
Day Hour: 8  
Sunset Hour: 18
Night Hour: 22
```
This creates a 10x faster cycle with standard timing.

### Custom Phase Timing
```
Time Speed Multiplier: 2.0
Sunrise Hour: 5
Day Hour: 6
Sunset Hour: 20
Night Hour: 23
```
This creates longer day periods with earlier sunrise and later sunset.

## Backward Compatibility
- All default values match the original implementation
- Existing scenes will work unchanged
- No code changes required for existing projects

## Testing
Use the TimeManagerTester component to validate configuration:
1. Add TimeManagerTester to the same GameObject as TimeManager
2. Enable testing in the Inspector
3. Observe debug output in the Console