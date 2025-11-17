# Unity and OpenXR Best Practices

This document outlines the coding standards and best practices followed in this project for Unity and OpenXR development.

## Unity Best Practices

### Code Organization

#### Assembly Definitions
- Create assembly definitions (`.asmdef`) for logical code groupings
- Reduces compilation times by enabling incremental compilation
- Improves code encapsulation and dependency management
- Example: `Unity.CI.MRTemplate.asmdef` for MR template scripts

#### Namespaces
- Use namespaces to avoid naming conflicts
- Follow pattern: `Unity.CompanyName.ProjectName` or `CompanyName.ProjectName`
- Example: `Unity.XRTemplate`, `Unity.XR.Content.UI.Layout`

### Serialization and Inspector

#### Field Access Modifiers
```csharp
// ✅ Good - Private with SerializeField
[SerializeField]
[Tooltip("Description of what this field does")]
private GameObject m_Target;

// ❌ Bad - Public field
public GameObject Target;
```

#### Field Naming
- Use `m_` prefix for member variables
- Use `k_` prefix for constants
- Use `s_` prefix for static variables
- Use PascalCase for public properties
- Use camelCase with prefix for private fields

```csharp
// Constants
const float k_MaxSpeed = 10f;
const string k_ShaderName = "Unlit/Texture";

// Static fields
static readonly int s_ShaderPropertyId = Shader.PropertyToID("_Color");

// Member fields
[SerializeField]
private float m_Speed;

// Properties
public float Speed => m_Speed;
```

### Performance Optimization

#### Component Caching
```csharp
// ✅ Good - Cache in Awake/Start
private Renderer m_Renderer;

void Awake()
{
    m_Renderer = GetComponent<Renderer>();
}

void Update()
{
    if (m_Renderer != null)
        m_Renderer.material.color = Color.red;
}

// ❌ Bad - GetComponent every frame
void Update()
{
    GetComponent<Renderer>().material.color = Color.red;
}
```

#### Camera.main Caching
```csharp
// ✅ Good - Cache camera reference
private Transform m_CameraTransform;

void Start()
{
    var mainCamera = Camera.main;
    if (mainCamera != null)
        m_CameraTransform = mainCamera.transform;
}

// ❌ Bad - Camera.main every frame (uses FindGameObjectWithTag internally)
void Update()
{
    transform.LookAt(Camera.main.transform);
}
```

#### Shader Property IDs
```csharp
// ✅ Good - Cache shader property IDs
static readonly int k_AlphaProperty = Shader.PropertyToID("_Alpha");

void UpdateAlpha(float alpha)
{
    material.SetFloat(k_AlphaProperty, alpha);
}

// ❌ Bad - String lookup every call
void UpdateAlpha(float alpha)
{
    material.SetFloat("_Alpha", alpha);
}
```

### Memory Management

#### Resource Cleanup
```csharp
// ✅ Good - Cleanup created resources
private RenderTexture m_RenderTexture;
private Material m_Material;

void OnDestroy()
{
    if (m_RenderTexture != null)
    {
        m_RenderTexture.Release();
        Destroy(m_RenderTexture);
    }
    
    if (m_Material != null)
    {
        Destroy(m_Material);
    }
}
```

#### Coroutine Management
```csharp
// ✅ Good - Stop before starting new coroutine
private Coroutine m_FadeCoroutine;

public void StartFade()
{
    if (m_FadeCoroutine != null)
        StopCoroutine(m_FadeCoroutine);
    
    m_FadeCoroutine = StartCoroutine(FadeRoutine());
}
```

### Null Safety

#### Required Component Checks
```csharp
void Start()
{
    m_Renderer = GetComponent<Renderer>();
    if (m_Renderer == null)
    {
        Debug.LogWarning($"ComponentName: No Renderer found on {name}", this);
        enabled = false;
        return;
    }
}
```

#### Safe Component Access
```csharp
// ✅ Good - Check before use
if (m_Component != null)
{
    m_Component.DoSomething();
}

// ✅ Better - Use TryGetComponent for first-time access
if (TryGetComponent(out Rigidbody rb))
{
    rb.AddForce(force);
}
```

### Event Handling

#### Subscribe/Unsubscribe Pattern
```csharp
void OnEnable()
{
    if (m_Button != null)
        m_Button.onClick.AddListener(OnButtonClick);
    
    if (m_Interactable != null)
        m_Interactable.selectEntered.AddListener(OnSelectEntered);
}

void OnDisable()
{
    if (m_Button != null)
        m_Button.onClick.RemoveListener(OnButtonClick);
    
    if (m_Interactable != null)
        m_Interactable.selectEntered.RemoveListener(OnSelectEntered);
}
```

### Documentation

#### XML Documentation
```csharp
/// <summary>
/// Fades the material's alpha value over time.
/// </summary>
/// <param name="visible">True to fade to invisible, false to fade to visible.</param>
/// <returns>Coroutine that can be yielded on.</returns>
public IEnumerator Fade(bool visible)
{
    // Implementation
}
```

#### Tooltips
```csharp
[SerializeField]
[Tooltip("The target transform to follow. Defaults to main camera if not set.")]
private Transform m_Target;
```

## OpenXR and XR Interaction Toolkit Best Practices

### XR Interaction Toolkit 3.0.3

#### Interactable Components
```csharp
// ✅ Good - Extend base XR interactables
public class CustomInteractable : XRGrabInteractable
{
    protected override void OnEnable()
    {
        base.OnEnable();
        selectEntered.AddListener(OnGrabbed);
    }
    
    protected override void OnDisable()
    {
        selectEntered.RemoveListener(OnGrabbed);
        base.OnDisable();
    }
}
```

#### Interactor Usage
```csharp
// ✅ Good - Use IXRSelectInteractor interface
IXRSelectInteractor m_Interactor;

void OnSelectEntered(SelectEnterEventArgs args)
{
    m_Interactor = args.interactorObject;
}

// Access attach transform
var attachTransform = m_Interactor.GetAttachTransform(this);
```

### Input System Integration

#### Reading Input from XR
```csharp
// ✅ Good - Read from interactor input
XRRayInteractor m_RayInteractor;

void Update()
{
    if (m_RayInteractor != null)
    {
        float selectValue = m_RayInteractor.selectInput.ReadValue();
        float uiPressValue = m_RayInteractor.uiPressInput.ReadValue();
    }
}

// ❌ Bad - Don't read directly from input devices in XR context
// Use the interactor's input abstraction instead
```

### AR Foundation Integration

#### Plane Detection
```csharp
[SerializeField]
private ARPlaneManager m_PlaneManager;

void Start()
{
    if (m_PlaneManager != null)
    {
        m_PlaneManager.planesChanged += OnPlanesChanged;
    }
}

void OnPlanesChanged(ARPlanesChangedEventArgs args)
{
    foreach (var plane in args.added)
    {
        // Handle new planes
    }
}
```

#### Tracking State Handling
```csharp
void Update()
{
    switch (m_ARPlane.trackingState)
    {
        case TrackingState.None:
            // Handle not tracking
            break;
        case TrackingState.Limited:
            // Handle limited tracking
            break;
        case TrackingState.Tracking:
            // Handle good tracking
            break;
    }
}
```

### Performance Considerations for XR

#### Frame Rate is Critical
- Target 72 FPS for Quest 2, 90 FPS for Quest 3
- Use Unity Profiler regularly
- Minimize garbage collection in Update/LateUpdate
- Use object pooling for frequently instantiated objects

#### Rendering Optimization
```csharp
// Use layer masks for raycasting
[SerializeField]
private LayerMask m_InteractionMask;

void CheckForInteraction()
{
    if (Physics.Raycast(ray, out hit, maxDistance, m_InteractionMask))
    {
        // Process hit
    }
}
```

#### Smooth Interpolation
```csharp
// ✅ Good - Use smooth interpolation for natural movement
void Update()
{
    float ease = 1f - Mathf.Exp(-m_SmoothSpeed * Time.deltaTime);
    transform.position = Vector3.Lerp(transform.position, targetPosition, ease);
}

// For rotation
transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, ease);
```

## Platform-Specific Considerations

### Meta Quest
- Use OpenXR backend (Meta XR SDK 1.0.4+)
- Enable hand tracking in XR Plugin Management
- Test both controller and hand tracking modes
- Optimize for mobile GPU (Adreno)

### XREAL Devices
- Supports both 3DOF and 6DOF modes
- Test passthrough quality and lighting
- Consider battery life in design

## Testing

### Test Framework Usage
```csharp
// EditMode test example
[Test]
public void ComponentInitializesCorrectly()
{
    var go = new GameObject();
    var component = go.AddComponent<MyComponent>();
    
    Assert.IsNotNull(component);
    Assert.AreEqual(expectedValue, component.Value);
}

// PlayMode test for XR
[UnityTest]
public IEnumerator XRInteractionWorks()
{
    // Setup XR scene
    yield return new WaitForSeconds(1);
    
    // Perform interaction
    // Assert expected behavior
}
```

## Common Anti-Patterns to Avoid

### ❌ Don't
- Use `FindObjectOfType` in Update loops
- Create materials/textures without cleanup
- Use public fields for everything
- Ignore null checks
- Use magic numbers instead of constants
- Access `transform` or `gameObject` directly in tight loops
- Allocate memory in Update/LateUpdate unnecessarily

### ✅ Do
- Cache component references
- Clean up resources in OnDestroy
- Use SerializeField on private fields
- Add null safety checks
- Define constants for magic numbers
- Cache transform/gameObject references
- Use object pooling for repeated instantiation
- Profile regularly and optimize hotspots

## Version Information

- Unity Version: 2022.3.60f1 LTS
- XR Interaction Toolkit: 3.0.3
- OpenXR Plugin: 1.14.2
- Meta XR SDK: 1.0.4
- AR Foundation: 5.1.6

## References

- [Unity Manual](https://docs.unity3d.com/Manual/index.html)
- [XR Interaction Toolkit Manual](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.0/manual/index.html)
- [OpenXR Specification](https://www.khronos.org/openxr/)
- [Unity Performance Optimization](https://docs.unity3d.com/Manual/BestPracticeUnderstandingPerformanceInUnity.html)
- [Meta Quest Development](https://developer.oculus.com/documentation/unity/)
