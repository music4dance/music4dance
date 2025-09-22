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
