# MRTemplate Tests

This directory contains automated tests for the MRTemplate components.

## Test Organization

### Editor Tests
Located in `Editor/` directory, these tests run in Edit mode without entering Play mode:
- Fast execution
- Good for testing component initialization
- Testing RequireComponent attributes
- Validating serialization

### Runtime Tests  
Located in `Runtime/` directory, these tests run in Play mode:
- Test runtime behavior
- Test coroutines and frame-by-frame logic
- Test component lifecycle (Awake, Start, Update, OnDestroy)
- Test XR interactions (requires XR setup)

## Running Tests

### Unity Test Runner
1. Open Unity Test Runner: `Window > General > Test Runner`
2. Select `EditMode` or `PlayMode` tab
3. Click `Run All` or select specific tests

### Command Line
```bash
# Run all tests
Unity.exe -runTests -batchmode -projectPath . -testResults ./test-results.xml

# Run only EditMode tests
Unity.exe -runTests -batchmode -projectPath . -testPlatform EditMode -testResults ./test-results-edit.xml

# Run only PlayMode tests
Unity.exe -runTests -batchmode -projectPath . -testPlatform PlayMode -testResults ./test-results-play.xml
```

## Current Test Coverage

### Implemented Tests
- ✅ Component initialization tests
- ✅ RequireComponent validation
- ✅ Resource cleanup verification
- ✅ Null safety validation

### Planned Tests
- ⬜ XR interaction behavior tests (requires XR simulator)
- ⬜ AR plane detection tests (requires AR Foundation setup)
- ⬜ Material fade animation tests
- ⬜ Video player functionality tests
- ⬜ Goal manager flow tests

## Writing New Tests

### Edit Mode Test Template
```csharp
using NUnit.Framework;
using UnityEngine;

namespace Unity.CI.MRTemplate.Tests
{
    public class MyComponentTests
    {
        GameObject m_TestGameObject;

        [SetUp]
        public void Setup()
        {
            m_TestGameObject = new GameObject("TestObject");
        }

        [TearDown]
        public void Teardown()
        {
            if (m_TestGameObject != null)
                Object.DestroyImmediate(m_TestGameObject);
        }

        [Test]
        public void MyComponent_DoesExpectedBehavior()
        {
            var component = m_TestGameObject.AddComponent<MyComponent>();
            Assert.IsNotNull(component);
            // Add more assertions
        }
    }
}
```

### Play Mode Test Template
```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.CI.MRTemplate.Tests
{
    public class MyComponentPlayModeTests
    {
        GameObject m_TestGameObject;

        [SetUp]
        public void Setup()
        {
            m_TestGameObject = new GameObject("TestObject");
        }

        [TearDown]
        public void Teardown()
        {
            if (m_TestGameObject != null)
                Object.Destroy(m_TestGameObject);
        }

        [UnityTest]
        public IEnumerator MyComponent_RuntimeBehavior()
        {
            var component = m_TestGameObject.AddComponent<MyComponent>();
            
            yield return null; // Wait one frame
            
            Assert.IsTrue(component.IsInitialized);
        }
    }
}
```

## Best Practices

### Test Organization
- One test class per component
- Group related tests in nested classes
- Use descriptive test names: `Component_Scenario_ExpectedBehavior`

### Setup and Teardown
- Always clean up created GameObjects
- Use `DestroyImmediate` in EditMode tests
- Use `Destroy` in PlayMode tests
- Reset static state if needed

### Assertions
- Test one concept per test method
- Use specific assertion messages
- Prefer `Assert.That()` for complex assertions

### Performance
- Keep tests fast and focused
- Avoid unnecessary waits in UnityTest
- Mock external dependencies when possible

### XR Testing
- Use XR Device Simulator for interaction tests
- Test with and without XR hardware
- Verify null handling when XR components are missing

## CI/CD Integration

These tests can be integrated into continuous integration pipelines:
- GitHub Actions
- GitLab CI
- Jenkins
- Unity Cloud Build

Example workflow includes:
1. Run EditMode tests
2. Run PlayMode tests
3. Generate coverage reports
4. Fail build on test failures

## Dependencies

Tests depend on:
- Unity Test Framework (com.unity.test-framework)
- NUnit Framework
- MRTemplate assembly (Unity.CI.MRTemplate)

## Known Limitations

- XR interaction tests require XR Device Simulator or actual hardware
- AR tests require AR Foundation setup in Play mode
- Some platform-specific features may not be testable in Editor
- Video playback tests may require specific codecs

## Contributing

When adding new components:
1. Write corresponding EditMode tests
2. Add PlayMode tests for runtime behavior
3. Test null safety and error handling
4. Verify resource cleanup in OnDestroy
5. Update this README with new test coverage

## References

- [Unity Test Framework Documentation](https://docs.unity3d.com/Packages/com.unity.test-framework@1.1/manual/index.html)
- [NUnit Documentation](https://docs.nunit.org/)
- [Unity Testing Best Practices](https://unity.com/how-to/unity-test-framework-tutorial)
