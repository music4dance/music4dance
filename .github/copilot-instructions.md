# GitHub Copilot Instructions for music4dance.net

## Project Overview

music4dance.net is a sophisticated web application designed to help dancers find appropriate music for competitive ballroom dancing and social partner dancing. The platform catalogs music with specific focus on dance styles, tempo ranges, and competitive dance categories.

**Core Mission**: Match music to dance styles based on meter, tempo, and competitive ballroom dance requirements established by World Dance Council and National Dance Council of America.

## Architecture

### Backend (.NET)

- **Framework**: ASP.NET Core (net9.0)
- **Language**: C# with nullable reference types enabled
- **Structure**:
  - `DanceLib/` - Core domain models and business logic
  - `m4d/` - Main web application (controllers, views, configuration)
  - `m4dModels/` - Data models and Entity Framework context
  - `SelfCrawler/` - Web crawling tests (run manually, excluded from routine testing)

### Frontend (Vue.js)

- **Framework**: Vue 3 with TypeScript
- **Build Tool**: Vite
- **Testing**: Vitest with Vue Test Utils
- **Styling**: Bootstrap 5 with custom SCSS
- **Package Manager**: Yarn (not npm)

## Domain Knowledge

### Dance Categories & Competition Structure

**Major Competition Groups:**

- **Ballroom**: International Standard, International Latin, American Smooth, American Rhythm
- **Social Dancing**: Swing, Salsa, Tango, Country, etc.
- **Performance**: Broadway, Contemporary, Jazz, Ballet, Tap (different tempo constraints)

**International Standard**: Waltz, Tango, Viennese Waltz, Foxtrot, Quickstep
**International Latin**: Cha Cha, Samba, Rumba, Paso Doble, Jive
**American Smooth**: Waltz, Tango, Foxtrot, Viennese Waltz
**American Rhythm**: Cha Cha, Rumba, East Coast Swing, Bolero, Mambo

### Tempo System

**Critical Concepts:**

- **MPM (Measures Per Minute)**: Primary tempo measurement for dance
- **BPM (Beats Per Minute)**: Standard musical tempo
- **BPS (Beats Per Second)**: Alternative measurement
- **Meter**: Time signature (e.g., 4/4, 3/4, 2/4)

**Tempo Relationships:**

- Different dances have specific tempo ranges
- Organizations (NDCA, DanceSport) may have different tempo requirements
- Same music can work for multiple related dances at different tempos
- Example: Rumba family (Bolero 96-104 BPM, International Rumba 100-108 BPM, American Rumba 120-144 BPM)

**Competition Order**: Dances are performed in specific order in competitions (competitionOrder property)

## Coding Standards & Conventions

### C# (.NET) Standards

- **DO NOT use nullable reference types** (`string?`, `object?`) - This project does not have nullable reference types enabled
  - Use regular types: `string`, `object`, `List<string>`, etc.
  - Check for null explicitly using `if (value != null)` or null-coalescing operators
  - Avoid CS8632 warnings by not using `?` annotations on reference types
- Prefer records for immutable data structures
- Use `readonly` fields where appropriate
- Follow dependency injection patterns
- Entity Framework with code-first approach
- Use `JsonConstructor` for serialization when needed
- Immutable classes for core domain objects (Tempo, TempoRange, etc.)

### TypeScript/Vue Standards

- **Composition API**: Use `<script setup lang="ts">` syntax
- **Type Safety**: Explicit typing, avoid `any`
- **Props Definition**: Use `defineProps<>()` with TypeScript interfaces
- **Component Structure**: Single File Components (.vue)
- **Testing**: Vitest with comprehensive component testing
- **Naming**: PascalCase for components, camelCase for properties
- **Line Endings**: ALL files in `m4d/ClientApp/**` MUST use LF (Unix) line endings, not CRLF
  - This is enforced by `.gitattributes` for Vue.js/Node.js convention
  - When creating new files in ClientApp, ensure LF line endings
- **Functional Programming**: Prefer functional/method chaining style over imperative loops
  - Use `map()`, `filter()`, `reduce()`, `sort()` for array operations
  - Example: `items.map(x => x.value).filter(v => v > 0).sort()` instead of for-loops
  - Use spread operator `[...new Set(array)]` for deduplication when array is small
- **Unit Testing**: Always add unit tests for new methods containing logic
  - Test happy path and edge cases (empty arrays, single items, duplicates)
  - Test error conditions (invalid IDs, undefined values)
  - Co-locate tests in `__tests__/` folders next to source files
- **Icons**: Use unplugin-icons for icon components
  - Icons auto-import as Vue components: `<IBi{IconName} />`
  - Example: `<IBiCheckCircleFill />`, `<IBiCircle />`, `<IBiPencil />`
  - Add CSS classes for styling: `<IBiCheckCircleFill class="text-success me-2" />`
  - **NEVER** use `<i class="bi bi-icon-name">` (old Bootstrap Icons pattern)
  - Icons from `@iconify-json/bi` package (Bootstrap Icons)
  - No explicit imports needed - unplugin-icons handles auto-import
- **Bootstrap Components**: ALWAYS prefer bootstrap-vue-next components over custom implementations or native Bootstrap 5
  - Use `<BAccordion>`, `<BAlert>`, `<BButton>`, `<BCard>`, etc. from bootstrap-vue-next
  - Avoid manual Bootstrap CSS/JS patterns when equivalent component exists
  - Provides better Vue integration, reactivity, and type safety
  - Docs: https://bootstrap-vue-next.github.io/bootstrap-vue-next/

### File Organization

- **Backend**: Controllers → Models → Services pattern
- **Frontend**: `pages/` for route components, `components/` for reusable UI
- **Tests**: Co-located with source (`__tests__/` folders)
- **Models**: Shared between frontend/backend when possible

## Testing Strategy

### Comprehensive Testing Documentation

**Primary Reference:** See `architecture/testing-patterns.md` for complete testing guide including:

- Server-side integration testing patterns (DanceMusicTester)
- Client-side testing patterns (Vitest/Vue)
- Helper methods and test infrastructure
- Common pitfalls and solutions
- Testing checklists

### Server Tests

- **Include**: All test projects EXCEPT SelfCrawler
- **SelfCrawler**: Web crawling/integration tests, run manually only
- **Unit Tests**: Focus on domain logic (Tempo, TempoRange, Dance models)
- **Filter**: Use `--filter "FullyQualifiedName!~SelfCrawler"` for routine testing

### Client Tests

- **Framework**: Vitest with Vue Test Utils
- **Run Mode**: Use `--run` flag to execute once (not watch mode)
- **Coverage**: Component rendering, business logic, model validation
- **Test Data**: Use realistic dance/tempo data in tests

### Task Configuration

- **Default Build**: "Build All" (server + client)
- **Default Test**: "Test All" (server tests excluding SelfCrawler + client tests + linting)
- **Manual SelfCrawler**: Separate "Server: Test SelfCrawler" task

## Development Workflow

### Build Process

1. Server: `dotnet build` (may encounter file locks if app is running)
2. Client: `yarn install` → `yarn build` (includes type checking)
3. Linting: `yarn lint` (ESLint with auto-fix)
4. Type Checking: `yarn type-check` (Vue TSC)

### Common Patterns

**Tempo Calculations**:

```csharp
// Creating tempo objects
var tempo = new Tempo(120, new TempoType(TempoKind.Bpm));
var tempoRange = new TempoRange(min: 96.0m, max: 104.0m);
```

**Vue Component Structure**:

```vue
<script setup lang="ts">
import type { DanceType, TempoRange } from "@/models";

interface Props {
  dance: DanceType;
  tempoRange?: TempoRange;
}

const props = defineProps<Props>();
</script>
```

**Dance Category Lookups**:

```csharp
var category = CompetitionCategory.GetCategory("american-rhythm");
var ballroomDances = CompetitionGroup.Get(CompetitionCategory.Ballroom);
```

**Filter Construction** (TypeScript):

```typescript
// ALWAYS use class library methods - NEVER construct filter strings manually
import { DanceQueryItem } from "@/models/DanceQueryItem";
import { Tag } from "@/models/Tag";

// Correct: Use DanceQueryItem to build dance filter strings
const queryItem = new DanceQueryItem({
  id: danceId,
  threshold: 1,
  tags: styleTag ? Tag.fromParts(styleTag, "Style").toString() : undefined,
});
filter.dances = queryItem.toString(); // Produces: "CHA|+International:Style"

// Correct: Use Tag.fromParts for tag construction
const tag = Tag.fromParts("International", "Style"); // Produces proper tag format

// WRONG: Never manually construct filter strings
filter.dances = `${danceId}|+${styleTag}:Style`; // ❌ Don't do this
const tag = `${styleTag}:Style`; // ❌ Don't do this
```

**Why use class library:**

- Classes handle proper serialization formats (e.g., `DanceQueryItem.toString()`)
- Encapsulation ensures format changes are centralized
- Type safety prevents format errors
- Parsing and serialization stay in sync

**String Parsing** (General Rule):

```typescript
// ALWAYS use helper classes for parsing - NEVER manually parse strings
import { TagQuery } from "@/models/TagQuery";
import { DanceQueryItem } from "@/models/DanceQueryItem";

// Correct: Use TagQuery to extract tag information
const tagQuery = item.tagQuery;
const styleTags = tagQuery.tagList.tags.filter(
  (tag) => tag.category === "Style",
);
const styleValue = styleTags[0]?.value;

// WRONG: Never manually parse tag strings
const parts = tagString.split(":"); // ❌ Don't do this
const value = parts[0]; // ❌ Don't do this

// Correct: Use DanceQueryItem.fromValue to parse dance queries
const queryItem = DanceQueryItem.fromValue("CHA|+International:Style");
const styleTag = queryItem.tagQuery?.tagList.tags.find(
  (t) => t.category === "Style",
)?.value;

// WRONG: Never manually parse query strings
const parts = queryString.split("|"); // ❌ Don't do this
const tagPart = parts[1]?.split(":"); // ❌ Don't do this
```

**Why avoid manual parsing:**

- Helper classes handle edge cases (special characters, optional fields, prefixes)
- Type safety ensures correct property access
- Changes to format only require updating one class
- Reduces bugs from inconsistent parsing logic

## Testing Strategy

### Integration Testing Best Practices

**Primary Documentation:** `architecture/testing-patterns.md`

**Creating Songs for Tests**:

Songs should be created using the serialization format, NOT by setting properties directly:

```csharp
// ✅ CORRECT: Use Song.Create with serialized properties
var songData = @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=My Song	Artist=Artist Name	Tempo=180.0	Tag+=Salsa:Dance	DanceRating=SLS+1";
var song = await Song.Create(songData, dms);

// ❌ WRONG: Don't set properties directly (incomplete initialization)
var song = new Song
{
    SongId = Guid.NewGuid(),
    Title = "My Song",
    Artist = "Artist Name",
    Tempo = 180m
};
song.DanceRatings.Add(new DanceRating { DanceId = "SLS", Weight = 1 });
```

**Serialized Song Format**:

- Tab-delimited key=value pairs
- `.Create=` prefix indicates song creation
- `User=` and `Time=` for audit trail
- `Tag+=` for song-level tags (format: `value:Category` or `value1|value2:Category`)
- `Tag+:DANCEID=` for dance-specific tags
- `DanceRating=DANCEID+weight` for dance ratings

**Tag Levels**:

```csharp
// Song-level tags (apply to entire song)
Tag+=4/4:Tempo|Salsa:Dance    // Meter at song level

// Dance-specific tags (apply to specific dance rating)
Tag+:SLS=Traditional:Style    // Style tag for Salsa dance rating only
```

**Common Tag Categories**:

- `Tempo` - Meter information (e.g., "4/4", "3/4", "6/8")
- `Dance` - Dance type tags
- `Style` - Dance style (e.g., "Traditional", "International", "American")
- `Music` - Music genre
- `Other` - General tags

**Using DanceMusicTester**:

```csharp
// Basic service creation
var dms = await DanceMusicTester.CreateServiceWithUsers("TestDb");

// Service with TestSongIndex (for capturing EditSong calls)
// DanceMusicTester creates and attaches TestSongIndex automatically
var service = await DanceMusicTester.CreateService("TestDb", useTestSongIndex: true);
var testIndex = (TestSongIndex)service.SongIndex;  // Get the TestSongIndex from service
await DanceMusicTester.AddUser(service, "dwgray", false);
```

**Testing SongIndex Modifications**:

To verify that code correctly modifies songs (via `SongIndex.EditSong`):

1. Make `SongIndex.EditSong` virtual
2. Create `TestSongIndex` that overrides and captures calls (using late-binding pattern)
3. Pass `useTestSongIndex: true` to `DanceMusicTester.CreateService` (automatic creation & attachment)
4. Get TestSongIndex from `service.SongIndex` and verify captured parameters

```csharp
// TestSongIndex uses late-binding to avoid circular dependency
// DanceMusicTester creates it and calls AttachToService automatically
public class TestSongIndex : SongIndex
{
    private DanceMusicCoreService? _actualService;
    public List<EditSongCall> EditCalls { get; } = new();

    public TestSongIndex() : base()
    {
    }

    public void AttachToService(DanceMusicCoreService service)
    {
        _actualService = service;
    }

    public override DanceMusicCoreService DanceMusicService =>
        _actualService ?? throw new InvalidOperationException("TestSongIndex not attached");

    public override async Task<bool> EditSong(ApplicationUser user, Song song, Song edit, ...)
    {
        EditCalls.Add(new EditSongCall(user, song, edit, tags?.ToList()));
        return await base.EditSong(user, song, edit, tags);
    }
}

// In tests, DanceMusicTester handles TestSongIndex creation and attachment
var service = await DanceMusicTester.CreateService("TestDb", useTestSongIndex: true);
var testIndex = (TestSongIndex)service.SongIndex;
await DanceMusicTester.AddUser(service, "dwgray", false);

// Verify the captured data
Assert.AreEqual(1, testIndex.EditCalls.Count);
var call = testIndex.EditCalls[0];
Assert.AreEqual("tempo-bot", call.User.UserName);
Assert.AreEqual(160m, call.Edit.Tempo);
```

**Integration Test Structure**:

- Use `[ClassInitialize]` to load dances once: `await DanceMusicTester.LoadDances();`
- Use `[AssemblyInitialize]` to setup shared infrastructure (e.g., `ApplicationLogging`)
- Create unique database names per test to avoid conflicts
- Use proper song serialization format for realistic test data
- Verify both return values AND side effects (EditSong calls, tag additions)

### Testing Patterns: When to Use Moq vs TestSongIndex

The codebase uses two distinct testing patterns for different purposes:

**Mock Pattern (Moq) - For Unit Tests:**

Use Moq when you want complete isolation and don't need real implementation behavior:

```csharp
// ✅ USE MOQ FOR: Unit tests with complete isolation
var mockSongIndex = new Mock<SongIndex>();
mockSongIndex.Setup(m => m.FindSong(It.IsAny<Guid>())).ReturnsAsync(someSong);
mockSongIndex.Setup(m => m.UpdateIndex(It.IsAny<List<string>>())).ReturnsAsync(true);

// Good for:
// - Testing controllers/services in isolation
// - Forcing specific return values or exceptions
// - Fast, focused unit tests
// - Default pattern in DanceMusicTester for simple tests
```

**Spy Pattern (TestSongIndex) - For Integration Tests:**

Use TestSongIndex when you need to verify real method implementations and capture actual parameters:

```csharp
// ✅ USE TESTSONGINDEX FOR: Integration tests with real behavior
var testIndex = new TestSongIndex();
var service = await DanceMusicTester.CreateService("TestDb", customSongIndex: testIndex);

// After test execution
Assert.AreEqual(1, testIndex.EditCalls.Count);
var call = testIndex.EditCalls[0];
Assert.AreEqual("tempo-bot", call.User.UserName);
Assert.AreEqual(160m, call.Edit.Tempo);

// Good for:
// - Testing end-to-end workflows (like tempo validation)
// - Verifying real side effects (song actually gets updated)
// - Inspecting complex parameters passed to methods
// - Integration tests where you need both real behavior AND verification
```

**Decision Guide:**

| Scenario                                              | Pattern           | Reason                                                    |
| ----------------------------------------------------- | ----------------- | --------------------------------------------------------- |
| Testing a controller that uses `DanceMusicService`    | **Moq**           | Need isolation, don't care about SongIndex implementation |
| Testing `MusicServiceManager.ValidateAndCorrectTempo` | **TestSongIndex** | Need to verify actual EditSong behavior and parameters    |
| Need to force an exception from SongIndex             | **Moq**           | Mock can force any return value/exception                 |
| Need to verify exact parameters passed to `EditSong`  | **TestSongIndex** | Spy captures real parameters for inspection               |
| Most basic tests in `DanceMusicTester`                | **Moq**           | Default for fast, isolated tests                          |
| Testing real song updates with tags                   | **TestSongIndex** | Need real behavior + parameter verification               |

**Key Difference:**

- **Moq = Mock Pattern**: Replace behavior entirely (no real code runs)
- **TestSongIndex = Spy Pattern**: Real code runs, but you can observe it

Both patterns are valuable and serve different testing needs. Choose based on whether you need isolation (Moq) or real behavior verification (TestSongIndex).

### Testing API Controllers (Integration Tests)

**Pattern:** Use DanceMusicTester with real dependencies (in-memory database)

**Example:** `m4d.Tests/APIControllers/UsageLogApiControllerTests.cs`

```csharp
[TestClass]
public class MyApiControllerIntegrationTests
{
    // Helper to create test configuration
    private static IConfiguration CreateTestConfiguration()
    {
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Setting:Key"] = "value"
        });
        return configBuilder.Build();
    }

    // Helper to create controller with dependencies
    private static (MyController controller, TestBackgroundTaskQueue queue)
        CreateController(DanceMusicService dms, IConfiguration? config = null)
    {
        config ??= CreateTestConfiguration();
        var taskQueue = new TestBackgroundTaskQueue();

        // Access internal properties via reflection if needed
        var internalField = typeof(DanceMusicCoreService)
            .GetField("<PropertyName>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        if (internalField == null)
        {
            throw new InvalidOperationException("Reflection failed");
        }

        var service = (IService)internalField.GetValue(dms)!;

        var controller = new MyController(
            dms.Context,
            dms.UserManager,
            dms.SearchService,
            service,
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

1. ✅ Use unique database names per test
2. ✅ Use reflection for internal dependencies (document why)
3. ✅ Use `NullLogger<T>.Instance` for logging
4. ✅ Set up `ControllerContext` with `HttpContext`
5. ✅ Test both success and error cases
6. ✅ Verify background task enqueueing
7. ✅ Handle nullable reference types correctly (`string?`, `!` operator)

### Client-Side Testing (Vitest + Vue)

**Pattern:** Mock browser APIs, use synchronous assertions

**Example:** `m4d/ClientApp/src/composables/__tests__/useUsageTracking.test.ts`

```typescript
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";

describe("useMyComposable", () => {
  beforeEach(() => {
    // Mock browser APIs
    localStorage.clear();
    global.navigator.sendBeacon = vi.fn(() => true);

    // Mock user agent to NOT be a bot
    Object.defineProperty(navigator, "userAgent", {
      value: "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
      configurable: true,
    });

    Object.defineProperty(navigator, "webdriver", {
      value: false,
      configurable: true,
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("should do something", () => {
    // Arrange
    const composable = useMyComposable({ option: "value" });

    // Act
    composable.doSomething();

    // Assert (synchronous - no await needed for sendBeacon)
    expect(composable.state).toBe("expected");
    expect(global.navigator.sendBeacon).toHaveBeenCalledTimes(1);
  });
});
```

**Key Points:**

1. ✅ Mock user agent to avoid bot detection
2. ✅ Use `configurable: true` for property mocks
3. ✅ Restore mocks in `afterEach`
4. ✅ Synchronous assertions for sendBeacon (no async/await)
5. ✅ Test both happy path and edge cases

### Testing Checklists

**For New API Controllers:**

- [ ] Valid requests (200/202)
- [ ] Invalid requests (400)
- [ ] Authentication (anonymous vs authenticated)
- [ ] Request validation (null, empty, boundaries)
- [ ] Background task enqueueing
- [ ] Error handling

**For New Composables:**

- [ ] Initial state
- [ ] State changes
- [ ] API calls (mock sendBeacon/fetch)
- [ ] localStorage usage
- [ ] Error handling
- [ ] Cleanup/unmount

**Full testing guide:** `architecture/testing-patterns.md`

---

## Error Handling & Debugging

### Common Issues

- **File Locks**: .NET build may fail if development server is running
- **Tempo Validation**: Ensure tempo values are positive decimals < 250
- **Dance ID Consistency**: Use canonical names (lowercase, hyphens)
- **Vitest Watch Mode**: Use `--run` for CI/automated scenarios

### Performance Considerations

- Tempo calculations are frequently performed - ensure efficiency
- Database queries should consider dance hierarchies and tempo ranges
- Frontend should handle large lists of songs/dances efficiently

## Deployment Notes

- **Azure Pipelines**: Multiple pipelines for different deployment scenarios
- **Static Assets**: Client builds to static files served by .NET backend
- **Environment**: ASP.NET Core with development/production configurations
- **SSL**: Use HTTPS in production for dance/music data security

## Business Context

This is a specialized domain where precision matters:

- Competition dancers need exact tempo ranges
- Music must match specific dance requirements
- Different organizations have varying standards
- Social dancers have more flexibility than competitive dancers

When suggesting code changes or new features, consider the impact on both competitive and social dancing communities, and ensure tempo/dance relationships remain accurate.

