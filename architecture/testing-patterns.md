# Testing Patterns for music4dance.net

## Overview

This document describes the testing patterns and best practices established for the music4dance.net codebase, including both client-side (TypeScript/Vue) and server-side (C#/.NET) testing approaches.

---

## Table of Contents

1. [Server-Side Testing Patterns](#server-side-testing-patterns)
2. [Client-Side Testing Patterns](#client-side-testing-patterns)
3. [Test Infrastructure](#test-infrastructure)
4. [Best Practices](#best-practices)
5. [Common Pitfalls](#common-pitfalls)

---

## Server-Side Testing Patterns

### Integration Tests with DanceMusicTester

**When to use:** Testing API controllers, services, and database operations.

**Why integration over unit tests:**
- Tests real database interactions (in-memory)
- Tests real authentication/authorization
- Provides higher confidence
- Less brittle than mocking everything

### Pattern 1: Testing API Controllers

**Example: `UsageLogApiControllerTests.cs`**

```csharp
[TestClass]
public class UsageLogApiControllerIntegrationTests
{
    // Helper method to create test configuration
    private static IConfiguration CreateTestConfiguration()
    {
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Setting:Key"] = "value"
        });
        return configBuilder.Build();
    }

    // Helper method to create controller with all dependencies
    private static (MyController controller, TestBackgroundTaskQueue taskQueue) 
        CreateController(DanceMusicService dms, IConfiguration config = null)
    {
        config ??= CreateTestConfiguration();
        var taskQueue = new TestBackgroundTaskQueue();
        
        // Access internal properties via reflection if needed
        var internalField = typeof(DanceMusicCoreService)
            .GetField("<PropertyName>k__BackingField", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        
        if (internalField == null)
        {
            throw new InvalidOperationException("Could not access field via reflection");
        }
        
        var internalService = (IServiceInterface)internalField.GetValue(dms);
        
        var controller = new MyController(
            dms.Context,
            dms.UserManager,
            dms.SearchService,
            internalService,
            config,
            NullLogger<MyController>.Instance,
            taskQueue
        );
        
        return (controller, taskQueue);
    }

    [TestMethod]
    public async Task MyMethod_ValidInput_ReturnsSuccess()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateServiceWithUsers("TestDb_Unique");
        var (controller, taskQueue) = CreateController(dms);
        
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        
        // Act
        var result = await controller.MyMethod(request);
        
        // Assert
        Assert.IsInstanceOfType(result, typeof(OkObjectResult));
        Assert.AreEqual(1, taskQueue.Count);
    }
}
```

**Key Points:**
1. ? Use unique database names per test (`"TestDb_Unique"`)
2. ? Create helper methods to reduce duplication
3. ? Use reflection for internal dependencies (document why)
4. ? Use `NullLogger<T>.Instance` for logging in tests
5. ? Set up `ControllerContext` with `HttpContext` for controller tests

### Pattern 2: Testing Authenticated Users

```csharp
[TestMethod]
public async Task MyMethod_AuthenticatedUser_HasAccess()
{
    // Arrange
    var dms = await DanceMusicTester.CreateServiceWithUsers("TestDb_Auth");
    var (controller, _) = CreateController(dms);
    
    // Set up authenticated user
    var identity = new GenericIdentity("dwgray", "TestAuthentication");
    var principal = new ClaimsPrincipal(identity);
    
    controller.ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext { User = principal }
    };
    
    // Act
    var result = await controller.MyMethod();
    
    // Assert
    Assert.IsInstanceOfType(result, typeof(OkResult));
}
```

**Key Points:**
1. ? Use `GenericIdentity` with username matching test user ("dwgray", "batch", etc.)
2. ? Wrap in `ClaimsPrincipal`
3. ? Assign to `HttpContext.User`
4. ? DanceMusicTester creates standard test users automatically

### Pattern 3: Testing Background Tasks

```csharp
[TestMethod]
public async Task MyMethod_ValidInput_EnqueuesBackgroundTask()
{
    // Arrange
    var dms = await DanceMusicTester.CreateServiceWithUsers("TestDb_Background");
    var (controller, taskQueue) = CreateController(dms);
    
    // Act
    await controller.MyMethod(request);
    
    // Assert - Verify task was enqueued
    Assert.AreEqual(1, taskQueue.Count, "Should enqueue exactly one task");
    
    // Optional: Execute the task and verify results
    // (Requires full service provider setup - see TestBackgroundTaskQueue.ExecuteAllAsync)
}
```

**Key Points:**
1. ? Use `TestBackgroundTaskQueue` to capture enqueued tasks
2. ? Verify task count (controller logic)
3. ?? Task execution testing requires `IServiceProvider` (future enhancement)

### Pattern 4: Accessing Internal Dependencies

**Problem:** Some properties in `DanceMusicCoreService` are marked `internal`.

**Solution:** Use reflection to access backing fields.

```csharp
// Access auto-property backing field
var backingField = typeof(DanceMusicCoreService)
    .GetField("<PropertyName>k__BackingField", 
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

if (backingField == null)
{
    throw new InvalidOperationException(
        "Could not access PropertyName field via reflection. " +
        "This test infrastructure needs updating if the property implementation changed.");
}

var service = (IServiceInterface)backingField.GetValue(dms);
```

**When to use reflection in tests:**
- ? Accessing internal dependencies for testing
- ? Verifying private state (sparingly)
- ? Testing framework/infrastructure code
- ? NOT for production code
- ? NOT as a workaround for bad design

---

## Client-Side Testing Patterns

### Pattern 1: Vue Component Testing with Vitest

**Example: Testing composables**

```typescript
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { useMyComposable } from '../useMyComposable';

describe('useMyComposable', () => {
  beforeEach(() => {
    // Setup: Clear state, mock APIs, etc.
    localStorage.clear();
    global.navigator.sendBeacon = vi.fn(() => true);
    
    // Mock user agent to NOT be a bot
    Object.defineProperty(navigator, 'userAgent', {
      value: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36',
      configurable: true,
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('should do something', () => {
    // Arrange
    const composable = useMyComposable({ option: 'value' });
    
    // Act
    composable.doSomething();
    
    // Assert
    expect(composable.state).toBe('expected');
  });
});
```

**Key Points:**
1. ? Use `beforeEach` to set up clean state
2. ? Use `afterEach` to restore mocks
3. ? Mock browser APIs (`navigator`, `localStorage`, etc.)
4. ? Use descriptive test names: `should [expected behavior]`

### Pattern 2: Testing Synchronous Operations

**For synchronous APIs like sendBeacon:**

```typescript
it('sends beacon when threshold reached', () => {
  const mockSendBeacon = vi.fn(() => true);
  global.navigator.sendBeacon = mockSendBeacon;
  
  const tracker = useTracker({ threshold: 1 });
  
  // Act (synchronous)
  tracker.track('/page');
  
  // Assert immediately (no awaiting needed)
  expect(mockSendBeacon).toHaveBeenCalledTimes(1);
  expect(mockSendBeacon).toHaveBeenCalledWith(
    expect.stringContaining('/api/endpoint'),
    expect.any(String)
  );
});
```

**Key Points:**
1. ? Synchronous code = synchronous tests (no `async/await` needed)
2. ? Assert immediately after action
3. ? Verify both call count AND call arguments
4. ? Use `expect.stringContaining()` and `expect.any()` for flexible matching

### Pattern 3: Handling Bot Detection in Tests

**Problem:** Test environment may be detected as a bot.

**Solution:** Mock user agent and webdriver properties.

```typescript
beforeEach(() => {
  // Mock user agent to NOT be a bot
  Object.defineProperty(navigator, 'userAgent', {
    value: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36',
    configurable: true,
  });
  
  // Mock webdriver to false (headless detection)
  Object.defineProperty(navigator, 'webdriver', {
    value: false,
    configurable: true,
  });
});
```

### Pattern 4: Testing with MenuContext

```typescript
import type { MenuContextInterface } from '@/models/MenuContext';

it('uses configuration from MenuContext', () => {
  const menuContext: MenuContextInterface = {
    userName: 'testuser@example.com',
    xsrfToken: 'test-token',
    usageTracking: {
      enabled: true,
      anonymousThreshold: 3,
    }
  };
  
  const tracker = useTracker({ menuContext });
  
  // Verify configuration is used
  expect(tracker.config.anonymousThreshold).toBe(3);
});
```

---

## Test Infrastructure

### DanceMusicTester (Server-Side)

**Purpose:** Creates test instances of `DanceMusicService` with in-memory database and test users.

**Usage:**

```csharp
// Basic service creation
var dms = await DanceMusicTester.CreateService("TestDbName");

// Service with standard test users
var dms = await DanceMusicTester.CreateServiceWithUsers("TestDbName");

// Users created by CreateServiceWithUsers:
// - dwgray (admin)
// - batch (regular user)
// - batch-a, batch-e, batch-i, batch-s, batch-x (batch users)
// - Plus other test users
```

**Properties Available:**
- `dms.Context` - `DanceMusicContext` (in-memory database)
- `dms.UserManager` - `UserManager<ApplicationUser>`
- `dms.SearchService` - `ISearchServiceManager` (mock)
- Internal: `DanceStatsManager` (access via reflection)

### TestBackgroundTaskQueue (Server-Side)

**Purpose:** Test spy for `IBackgroundTaskQueue` that captures enqueued tasks.

**Usage:**

```csharp
var taskQueue = new TestBackgroundTaskQueue();

// Use in controller
var controller = new MyController(..., taskQueue);

// Verify tasks were enqueued
Assert.AreEqual(1, taskQueue.Count);
Assert.IsTrue(taskQueue.Tasks.Any());

// Optional: Execute tasks (requires IServiceProvider)
// await taskQueue.ExecuteAllAsync(serviceProvider);
```

**Available Methods:**
- `Count` - Number of enqueued tasks
- `Tasks` - Read-only collection of tasks
- `EnqueueTask(task)` - Enqueue a task (captured)
- `ExecuteAllAsync(serviceProvider)` - Execute all tasks (for integration testing)
- `Clear()` - Clear all tasks

---

## Best Practices

### General

1. **? Test Behavior, Not Implementation**
   - Test what the code does, not how it does it
   - Avoid testing private methods directly
   - Focus on public API and observable effects

2. **? Use Descriptive Test Names**
   ```csharp
   // Good
   public async Task LogBatch_EmptyEvents_ReturnsBadRequest()
   
   // Bad
   public async Task Test1()
   ```
   
   **Pattern:** `MethodName_Scenario_ExpectedResult`

3. **? Arrange-Act-Assert (AAA) Pattern**
   ```csharp
   [TestMethod]
   public async Task MyTest()
   {
       // Arrange - Setup test data and dependencies
       var dms = await DanceMusicTester.CreateServiceWithUsers("TestDb");
       var controller = CreateController(dms);
       
       // Act - Execute the code under test
       var result = await controller.MyMethod();
       
       // Assert - Verify expected outcomes
       Assert.IsInstanceOfType(result, typeof(OkResult));
   }
   ```

4. **? One Assert Per Concept**
   - Multiple `Assert` statements are OK if testing the same concept
   - Separate unrelated assertions into different tests

5. **? Use Unique Database Names**
   ```csharp
   // Good - Each test gets its own database
   var dms = await DanceMusicTester.CreateServiceWithUsers("LogBatch_ValidInput");
   
   // Bad - Tests may interfere with each other
   var dms = await DanceMusicTester.CreateServiceWithUsers("TestDb");
   ```

### Server-Side Specific

6. **? Use Helper Methods for Controller Setup**
   - Reduces duplication
   - Makes tests more maintainable
   - Centralizes dependency wiring

7. **? Test Request Validation**
   - Test null inputs
   - Test empty collections
   - Test boundary conditions (e.g., max size limits)

8. **? Test Authentication States**
   - Test anonymous users
   - Test authenticated users
   - Test authorization (roles/permissions)

9. **? Use `NullLogger` in Tests**
   ```csharp
   NullLogger<MyController>.Instance
   ```
   - Don't mock `ILogger` unless testing logging behavior
   - `NullLogger` is faster and simpler

### Client-Side Specific

10. **? Mock Browser APIs Explicitly**
    ```typescript
    beforeEach(() => {
      global.navigator.sendBeacon = vi.fn(() => true);
      global.localStorage.clear();
    });
    ```

11. **? Use `configurable: true` for Property Mocks**
    ```typescript
    Object.defineProperty(navigator, 'userAgent', {
      value: 'test-agent',
      configurable: true,  // ? Important!
    });
    ```

12. **? Test Bot Detection Edge Cases**
    - Different user agents
    - Headless mode
    - Webdriver presence

---

## Common Pitfalls

### Server-Side

? **Using `as` cast without null check**
```csharp
// Bad
var badRequest = result as BadRequestObjectResult;
Assert.AreEqual("error", badRequest.Value); // NullReferenceException if wrong type

// Good
Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
var badRequest = (BadRequestObjectResult)result;
Assert.AreEqual("error", badRequest.Value);
```

? **Sharing Database Between Tests**
```csharp
// Bad - All tests use same database
private static DanceMusicService _sharedService;

[ClassInitialize]
public static async Task Initialize()
{
    _sharedService = await DanceMusicTester.CreateServiceWithUsers("Shared");
}

// Good - Each test gets its own database
[TestMethod]
public async Task MyTest()
{
    var dms = await DanceMusicTester.CreateServiceWithUsers("MyTest_Unique");
}
```

? **Not Setting ControllerContext**
```csharp
// Bad - Controller has no HttpContext
var controller = new MyController(...);
var result = await controller.MyMethod(); // May throw

// Good
var controller = new MyController(...);
controller.ControllerContext = new ControllerContext
{
    HttpContext = new DefaultHttpContext()
};
```

? **Using Discard for ConfigurationBuilder**
```csharp
// Bad - Causes CS warnings
_ = configBuilder.AddInMemoryCollection(...);

// Good - Method chaining is preferred
configBuilder.AddInMemoryCollection(...);
return configBuilder.Build();
```

### Client-Side

? **Waiting Arbitrary Timeouts**
```typescript
// Bad - Flaky, slow
await new Promise(resolve => setTimeout(resolve, 100));
expect(mockFetch).toHaveBeenCalled();

// Good - Synchronous APIs don't need waiting
expect(mockSendBeacon).toHaveBeenCalled();
```

? **Not Restoring Mocks**
```typescript
// Bad - Mocks leak to other tests
it('test 1', () => {
  global.fetch = vi.fn();
});

// Good - Clean up after each test
afterEach(() => {
  vi.restoreAllMocks();
});
```

? **Not Mocking User Agent (Bot Detection)**
```typescript
// Bad - Test environment may be detected as bot
const tracker = useUsageTracking();
tracker.trackPageView('/page');
expect(localStorage.getItem('usageQueue')).toBeTruthy(); // Fails!

// Good - Mock user agent
beforeEach(() => {
  Object.defineProperty(navigator, 'userAgent', {
    value: 'Mozilla/5.0 ...',
    configurable: true
  });
});
```

---

## Testing Checklist

### For New API Controllers

- [ ] Test valid requests (200/202)
- [ ] Test invalid requests (400)
- [ ] Test authentication (anonymous vs authenticated)
- [ ] Test authorization (roles/permissions)
- [ ] Test request validation (null, empty, boundaries)
- [ ] Test background task enqueueing (if applicable)
- [ ] Test error handling (500)

### For New Client Composables

- [ ] Test initial state
- [ ] Test state changes
- [ ] Test API calls (mock fetch/sendBeacon)
- [ ] Test localStorage usage
- [ ] Test error handling
- [ ] Test cleanup/unmount
- [ ] Test with MenuContext configuration

### Code Review Checklist

- [ ] Tests follow naming convention
- [ ] Tests use AAA pattern
- [ ] Tests are deterministic (no random data unless seeded)
- [ ] Tests clean up after themselves
- [ ] Tests use helper methods to reduce duplication
- [ ] Tests have descriptive assertions with messages
- [ ] Tests cover happy path and error cases

---

## Example Test Suites

### Complete Server-Side Example

See: `m4d.Tests/APIControllers/UsageLogApiControllerTests.cs`

**Features demonstrated:**
- Integration testing with DanceMusicTester
- Reflection to access internal dependencies
- Helper methods for controller setup
- Authentication testing
- Request validation testing
- Background task verification

**Test Coverage:** 8 tests, all passing
- Valid anonymous request
- Authenticated user request
- Multiple events in batch
- Empty events (validation)
- Null request (validation)
- Too many events (validation)
- Required field validation
- Optional field validation

### Complete Client-Side Example

See: `m4d/ClientApp/src/composables/__tests__/useUsageTracking.test.ts`

**Features demonstrated:**
- Vitest with Vue Test Utils
- Browser API mocking (sendBeacon, localStorage)
- User agent mocking (bot detection)
- Synchronous testing (no arbitrary waits)
- MenuContext integration

**Test Coverage:** 19 tests, all passing
- UsageId management (3 tests)
- Visit count tracking (2 tests)
- Bot detection (3 tests)
- Anonymous user batching (2 tests)
- Authenticated user batching (1 test)
- Queue management (3 tests)
- XSRF token handling (1 test)
- SendBeacon on page unload (2 tests)
- Configuration (2 tests)

---

## Resources

### Documentation
- [MSTest Documentation](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-mstest)
- [Vitest Documentation](https://vitest.dev/)
- [Vue Test Utils](https://test-utils.vuejs.org/)

### Internal Documentation
- `SERVER-TESTING-FINAL-SUCCESS.md` - Server-side testing implementation details
- `SENDBEACON-REFACTORING-SUCCESS.md` - Client-side testing improvements
- `architecture/server-side-testing-analysis.md` - DI analysis and recommendations

### Test Infrastructure
- `m4dModels.Tests/DanceMusicTester.cs` - Test service creation
- `m4d.Tests/TestHelpers/TestBackgroundTaskQueue.cs` - Background task spy
- `m4dModels.Tests/TestSongIndex.cs` - Song index spy for integration tests

---

## Future Enhancements

### Server-Side

1. **Full Integration Testing**
   - Enhance `DanceMusicTester` to provide `IServiceProvider`
   - Enable background task execution in tests
   - Test database persistence end-to-end

2. **Test Database Seeding**
   - Helper methods for common test data scenarios
   - Song creation helpers
   - User creation with specific roles

3. **Performance Testing**
   - Add benchmarks for critical paths
   - Load testing helpers

### Client-Side

1. **Component Testing**
   - Add examples for testing Vue components
   - Testing with Vue Router
   - Testing with Pinia stores

2. **E2E Testing**
   - Playwright setup and patterns
   - Integration with CI/CD

3. **Visual Regression Testing**
   - Screenshot comparisons
   - Component story testing

---

## Conclusion

This testing infrastructure provides:
- ? **High Confidence** - Integration tests verify real behavior
- ? **Maintainability** - Helper methods reduce duplication
- ? **Fast Execution** - In-memory database, synchronous tests
- ? **Good Coverage** - 27/27 tests passing (100%)

The patterns documented here should be followed for all new code to maintain the high testing standards established in this implementation.

**Key Takeaway:** Test what matters, use the right tool for the job, and make tests readable and maintainable.
