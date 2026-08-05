The goal is to test `HectonCelestialEngine` in `PlayMode`. We need a test file `HectonCelestialEnginePlayModeTests.cs`.
Since it acts as a global singleton `InitializeRuntimeAuthority`, we should test the lifecycle.

Tests to add:
1. `Awake_RegistersGlobalRuntimeAuthority()`
2. `DuplicateEngine_DisablesPresentation()`
3. `TryApplyRuntimeTimeOfDay01_InvalidTime_ReturnsFalse()`
4. `SlowTick_UpdatesGlobalWeather()` (requires setup)

Wait, `HectonCelestialEngine` uses Unity shaders and render settings. `PlayMode` tests shouldn't invoke Editor-only things, and `HectonCelestialEngine` has things like `GlobalRegistry.RegisterCelestialEngineRuntime(this)`.

Let's check `HectonCelestialEngineEditTests.cs` for an idea of how tests deal with it. EditTests create a dummy GameObject, add `HectonCelestialEngine`, and check some values.

Let's make `HectonCelestialEnginePlayModeTests.cs` and ensure the `SetUp` builds an engine, maybe some mocks for dependencies if they are interfaces.
But `HectonCelestialEngine` is a monolithic MonoBehaviour.

Wait, PlayMode tests often run in an actual scene or empty scene.

Let's look at `Assets/_Project/Scripts/HectonCelestialEngine.cs` for its dependencies.
`[SerializeField] private HectonAtmosphereManager _atmosphereManager;`
`[SerializeField] private Light sunLight;`

Let's create a test suite:
`Assets/_Project/Tests/PlayMode/HectonCelestialEnginePlayModeTests.cs`

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hecton8.Core.Global;

public class HectonCelestialEnginePlayModeTests
{
    private GameObject _gameObject;
    private HectonCelestialEngine _engine;

    [SetUp]
    public void SetUp()
    {
        _gameObject = new GameObject("CelestialEngineTest");
        _engine = _gameObject.AddComponent<HectonCelestialEngine>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_gameObject);
    }

    [UnityTest]
    public IEnumerator Engine_RegistersWithGlobalRegistryOnEnable()
    {
        // ...
    }
}
```
Let's see if `GlobalRegistry.ActiveCelestialEngine` exists.
