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

- Use nullable reference types (`string?`, `object?`)
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

### File Organization

- **Backend**: Controllers → Models → Services pattern
- **Frontend**: `pages/` for route components, `components/` for reusable UI
- **Tests**: Co-located with source (`__tests__/` folders)
- **Models**: Shared between frontend/backend when possible

## Testing Strategy

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
  (tag) => tag.category === "Style"
);
const styleValue = styleTags[0]?.value;

// WRONG: Never manually parse tag strings
const parts = tagString.split(":"); // ❌ Don't do this
const value = parts[0]; // ❌ Don't do this

// Correct: Use DanceQueryItem.fromValue to parse dance queries
const queryItem = DanceQueryItem.fromValue("CHA|+International:Style");
const styleTag = queryItem.tagQuery?.tagList.tags.find(
  (t) => t.category === "Style"
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
