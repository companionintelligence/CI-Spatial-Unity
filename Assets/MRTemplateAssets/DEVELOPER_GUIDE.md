# MR Template Assets - Developer Guide

## Overview

The MRTemplateAssets contains Unity scripts and components for building Mixed Reality applications targeting Meta Quest 3 and XREAL devices. These scripts implement common XR patterns and UI behaviors following Unity and OpenXR best practices.

## Architecture

### Assembly Definition
The scripts are organized in the `Unity.CI.MRTemplate` assembly with dependencies on:
- Unity XR Core Utils
- XR Interaction Toolkit 3.0.3
- AR Foundation
- TextMesh Pro
- Unity Input System

### Components

#### Material Fade Components

**FadeMaterial**
- Fades a GameObject's material alpha over time
- Used for smooth transitions between opaque and transparent states
- Properly caches Renderer component to avoid repeated GetComponent calls
- Example use: Fading skybox environments in MR experiences

**FadePlaneMaterial**
- Specialized fade component for plane materials with dual fade effects
- Controls both dot view radius and alpha values
- Used for AR plane visualization fade effects

#### Positioning Components

**PositionAtStart**
- Positions GameObject relative to a target transform after a startup delay
- Useful for initializing UI elements in XR space relative to the camera
- Supports rotation offset control with optional X-axis ignoring

**TurnToFace**
- Makes objects smoothly face a target (typically the camera)
- Supports axis-specific rotation ignoring
- Uses smooth interpolation for natural turning motion
- Static API for snapping all instances immediately

**ZoneScale**
- Dynamically scales objects based on distance from the camera
- Implements zone-based scaling with smooth transitions
- Helps maintain readable UI elements at varying distances in XR

#### XR Interaction Components

**XRBlaster**
- Custom XRGrabInteractable that aligns to face the interactor
- Determines orientation based on grab position (front/back)
- Useful for tool-like objects that should point toward the user

**LaunchProjectile**
- Spawns and launches projectiles with physics
- Applies forward force relative to a start point
- Used for throwing/shooting mechanics in XR

**PinchVizController**
- Visualizes pinch gestures using blend shape animation
- Reads from XRRayInteractor input values
- Provides visual feedback for hand tracking interactions

#### AR Components

**PlaneTrackingModeVisualizer**
- Visualizes AR plane tracking states with color coding
- None = Grey, Limited = Red, Tracking = Original color
- Helps debug AR plane detection

**GazeTooltips**
- Displays tooltips when gazing at AR surfaces
- Uses SphereCast for reliable detection
- Integrates with ARContactSpawnTrigger system

#### UI Components

**BooleanToggleVisualsController**
- Controls visual states of toggle switches
- Supports hover effects with Z-translation
- Implements IPointerEnterHandler and IPointerExitHandler

**XRPokeFollowAffordanceFill**
- Advanced poke interaction affordance with fill animation
- Smoothly follows poke position with configurable smoothing
- Supports distance clamping and return-to-initial-position

**VideoTimeScrubControl**
- Full video player control with scrubbing
- Displays current time and total duration
- Supports drag-to-scrub functionality
- Optional auto-hide after inactivity

**VideoPlayerRenderTexture**
- Creates and manages RenderTexture for video playback
- **Important**: Implements proper resource cleanup in OnDestroy to prevent memory leaks
- Dynamically assigns texture to target renderer

#### Game Logic Components

**GoalManager**
- Manages onboarding flow for MR experiences
- Queue-based goal system with completion tracking
- Integrates with AR plane detection and object spawning
- Supports coaching UI and tutorial flows

**SpawnedObjectsManager**
- Manages object spawning with UI controls
- Dropdown selection for spawn types
- Bulk destroy functionality

## Unity Best Practices Implemented

### Serialization
- Use `[SerializeField]` on private fields instead of public fields
- All serialized fields have descriptive tooltips
- Proper field naming with `m_` prefix for member variables

### Null Safety
- All GetComponent calls check for null results
- Debug.LogWarning/LogError used for graceful error handling
- Components disable themselves when missing required dependencies

### Performance
- Component references cached in Awake/Start to avoid repeated GetComponent calls
- Static shader property IDs for material property access
- Proper use of Time.unscaledDeltaTime for UI animations

### Memory Management
- Created resources (RenderTexture, Material) cleaned up in OnDestroy
- Coroutines properly stopped before starting new ones
- Event listeners removed in OnDisable

### Code Quality
- XML documentation on all public APIs
- Descriptive variable names
- Consistent code formatting
- Proper use of namespaces

## OpenXR Best Practices

### XR Interaction Toolkit 3.0.3
- Uses XRGrabInteractable, XRRayInteractor, and XRPokeFilter
- Implements IPokeStateDataProvider pattern correctly
- Properly subscribes/unsubscribes from interaction events

### Input Handling
- Uses Unity Input System for device-agnostic input
- Reads input values from XR interactors, not directly from devices
- Supports both controller and hand tracking inputs

### AR Foundation Integration
- Properly uses ARPlaneManager for plane detection
- Implements TrackingState visualization
- Uses ARContactSpawnTrigger for surface placement

### Performance Considerations
- Caches main camera reference
- Uses layer masks for optimized raycasting
- Implements smooth interpolation to reduce per-frame computation

## Common Patterns

### Component Initialization
```csharp
void Awake()
{
    // Cache required components
    m_Component = GetComponent<RequiredComponent>();
}

void Start()
{
    // Initialize with null checks
    if (m_Component == null)
    {
        Debug.LogWarning($"ComponentName: Missing required component on {name}", this);
        enabled = false;
        return;
    }
}
```

### Event Subscription
```csharp
void OnEnable()
{
    if (m_EventSource != null)
        m_EventSource.onEvent.AddListener(OnEventHandler);
}

void OnDisable()
{
    if (m_EventSource != null)
        m_EventSource.onEvent.RemoveListener(OnEventHandler);
}
```

### Resource Cleanup
```csharp
void OnDestroy()
{
    // Clean up created resources
    if (m_CreatedResource != null)
    {
        Destroy(m_CreatedResource);
    }
}
```

## Testing

Currently, no automated tests exist for these components. When adding tests:
- Use Unity Test Framework (already included in packages)
- Create PlayMode tests for XR interaction components
- Create EditMode tests for utility functions
- Mock XR dependencies where possible

## Known Issues and TODOs

### Ready Player Me Integration (from main README)
- Head movement issue when new sentence starts
- Eye pop on some avatars
- Body pose pop on start

## Contributing

When modifying these scripts:
1. Maintain XML documentation on all public APIs
2. Add null safety checks for all component references
3. Cache component references in Awake/Start
4. Clean up resources in OnDestroy
5. Use SerializeField instead of public fields
6. Add descriptive tooltips to serialized fields
7. Test on both Quest 3 and XREAL devices when possible

## See Also

- [Unity XR Interaction Toolkit Documentation](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.0/manual/index.html)
- [OpenXR Specification](https://www.khronos.org/openxr/)
- [Meta XR SDK Documentation](https://developer.oculus.com/documentation/unity/)
- [AR Foundation Documentation](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@5.1/manual/index.html)
