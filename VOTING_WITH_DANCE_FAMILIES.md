# Voting with Dance Style Families

## Overview

This document describes the implementation of dance style family voting on music4dance.net. The system allows users to specify dance style families (International, American, Country, etc.) when voting on multi-style dances, while maintaining dance-level vote counts.

## Problem Statement

Many competitive ballroom dances exist in multiple style families:

- **Cha Cha**: International Latin, American Rhythm
- **Jive**: International Latin, American Rhythm, Country
- **Waltz**: International Standard, American Smooth
- **Foxtrot**: International Standard, American Smooth

Users needed a way to indicate which style family they were voting for, while respecting these constraints:

1. **Vote counts are dance-level only** - A vote for "Jive (American)" counts the same as "Jive (International)" toward the total Jive vote count
2. **Style tags are per-user** - Style tags affect personal search results and filtering, not global vote counts
3. **Minimal friction** - Style selection should be quick and intuitive
4. **Existing behavior preserved** - Single-style dances work without extra UI clutter
5. **Context awareness** - Auto-select family when obvious (single family, search context)

---

## Implemented Solution

### Architecture Overview

**Vote Count Display:**

- Vote counts remain dance-level (e.g., "Jive: 42 votes")
- `DanceRatingDelta` stores vote deltas per dance (not per style)
- Maximum vote comparison is dance-level for ranking

**Style Tag Storage:**

- Format: `Tag+:${danceId}=${family1}:Style|${family2}:Style` (e.g., `Tag+:CHA=American:Style|International:Style`)
- Multiple families can be selected in a single vote
- Stored in song's edit history via `SongProperty` with user identifier
- Indexed for search: aggregated across all users for global searches
- Example: Search "CHA|+International:Style" finds all songs voted Cha Cha with International family tag by any user
- Per-user filtering available: users can search their own votes/tags specifically

**Data Models:**

```typescript
// Vote with optional family tags
class DanceRatingVote {
  danceId: string;
  direction: VoteDirection; // Up or Down
  familyTags?: string[]; // e.g., ["American", "International"]
}

// Property stored in song history
SongProperty.fromDanceFamilyTags(danceId, familyTags);
// Produces: "Tag+:CHA=American:Style|International:Style"
```

### Component Structure

**1. DanceVote.vue** - Inline voting on song pages

- Displays vote button with current rating
- For multi-style dances, shows `FamilyChoiceModal` when voting
- Auto-selects family when:
  - Dance has single style (e.g., Waltz → always International Standard)
  - Search context provides family filter (e.g., searching "American:Style")
- Passes `filterFamilyTag` from `SongFilter.familyTag` getter

**2. DanceVoteItem.vue** - Voting in DanceChooser modal

- Similar behavior to DanceVote
- Used when adding dances to songs via modal
- Receives `filterFamilyTag` prop from parent context

**3. FamilyChoiceModal.vue** - Multi-select family picker

- Shows checkboxes for all available families for a dance
- Allows selecting multiple families in one vote
- "Select All" and "Clear" shortcuts
- Emits array of selected families
- Only shown for multi-style dances when context doesn't auto-select

**4. SongFilter.familyTag** - Context-aware family extraction

- Extracts family tag from search filter (e.g., "CHA|+International:Style")
- Uses `TagList.Adds` to strip `+` qualifier prefix
- Filters for `category === "Style"` tags
- Validates family is in dance's `styleFamilies` list
- Returns `undefined` if no valid family tag in context

### Auto-Selection Logic

```typescript
// Priority order for family selection:
if (hasSingleStyle) {
  // 1. Single style dance → auto-select the only family
  return [dance.styleFamilies[0]];
} else if (filterFamilyTag) {
  // 2. Search context → use family from filter
  return [filterFamilyTag];
} else if (hasMultipleStyles) {
  // 3. Multiple styles, no context → show modal
  showFamilyChoiceModal = true;
} else {
  // 4. No family needed → vote without tag
  return undefined;
}
```

### Vote Processing Flow

**User clicks vote button:**

1. `DanceVote.handleVote(direction)` called
2. Auto-selection logic determines family tags
3. If family determined → `emitVote(direction, familyTags)`
4. If multi-style, no context → show `FamilyChoiceModal`
5. Modal emits selected families → `onFamiliesSelected(families)`
6. `emitVote(direction, families)` with user selections

**Vote emission:**

```typescript
emit("dance-vote", new DanceRatingVote(danceId, direction, familyTags));
```

**SongEditor processes vote:**

```typescript
public danceVote(vote: DanceRatingVote): void {
  if (vote.direction === VoteDirection.Up) {
    this.upVote(vote.danceId, vote.familyTags);
  } else {
    this.downVote(vote.danceId, vote.familyTags);
  }
}

private upVote(danceId: string, familyTags?: string[]): void {
  // Add dance rating delta (+1)
  this.setVoteProperties(danceId, true, false, familyTags);
}

private setVoteProperties(
  danceId: string,
  positive: boolean | undefined,
  negative: boolean | undefined,
  familyTags?: string[]
): void {
  // Add DanceRating property (vote count)
  this.addPropertyFromObject(SongProperty.fromDanceRating(danceId, delta));

  // Add Dance tag (Tag+:danceId:Dance)
  this.addPropertyFromObject(SongProperty.fromAddedTag(Tag.fromDanceId(danceId)));

  // Add family style tags if provided (Tag+:danceId=family1:Style|family2:Style)
  if (familyTags && familyTags.length > 0 && positive === true) {
    this.addPropertyFromObject(SongProperty.fromDanceFamilyTags(danceId, familyTags));
  }
}
```

**Result in song edit history:**

```
DanceRating=CHA+1
Tag+=Cha Cha:Dance
Tag+:CHA=American:Style|International:Style
```

### Key Technical Implementations

**1. SongProperty Factory Methods**

Using helper classes eliminates manual string construction:

```typescript
// Create dance rating property
SongProperty.fromDanceRating(danceId, delta);
// → "DanceRating=CHA+1"

// Create added tag property
SongProperty.fromAddedTag(Tag.fromDanceId(danceId));
// → "Tag+=Cha Cha:Dance"

// Create family tags property (multiple families)
SongProperty.fromDanceFamilyTags(danceId, ["American", "International"]);
// → "Tag+:CHA=American:Style|International:Style"

// Parsing properties
SongProperty.FromString("Tag+:CHA=American:Style|International:Style");
// → { name: "Tag+", value: "CHA=American:Style|International:Style" }
```

**2. TagList.Adds - Stripping Qualifiers**

The `TagList.Adds` getter extracts qualified tags and removes the `+` prefix:

```typescript
// Input: "+American:Style|+International:Style|-Country:Style"
tagList.Adds;
// → [Tag("American:Style"), Tag("International:Style")]
// Note: "Country" excluded (has - qualifier), and + prefixes stripped

// Used in SongFilter.familyTag:
const styleTags = tagQuery.tagList.Adds.filter(
  (tag) => tag.category === "Style"
);
const styleValue = styleTags[0]?.value; // "American" not "+American"
```

**3. DanceDatabase.getStyleFamilies**

Existing method provides family validation:

```typescript
const styleFamilies = database.getStyleFamilies("CHA");
// → ["American", "International"]

// Used to validate filter-extracted families:
if (styleFamilies.includes(styleValue)) {
  return styleValue; // Valid family for this dance
}
```

**4. DanceQueryBase.database Getter**

Provides access to DanceDatabase from query objects:

```typescript
// In DanceQueryBase:
public get database(): DanceDatabase {
  return safeDanceDatabase();
}

// Enables clean access in SongFilter:
const styleFamilies = this.danceQuery.database.getStyleFamilies(danceId);
```

### User Experience Scenarios

**Scenario 1: Single-style dance (International Waltz)**

- User clicks vote button
- Family auto-selected: "International"
- Vote recorded with family tag automatically
- No modal shown, seamless experience

**Scenario 2: Multi-style dance (Cha Cha), no context**

- User clicks vote button
- FamilyChoiceModal appears
- User selects "American" and "International"
- Vote recorded with both family tags

**Scenario 3: Filtered search ("International:Style")**

- User searches "International Cha Cha"
- Song list shows Cha Cha songs
- `SongFilter.familyTag` extracts "International"
- User clicks vote → "International" auto-selected
- No modal needed, context-aware

**Scenario 4: Changing vote style**

- User previously voted "American"
- Changes mind, votes again with "International"
- Old family tag removed, new one added
- Vote count unchanged (dance-level)

---

## Testing

### Unit Tests Created

**TagList.test.ts** - 46 tests

- ✅ Parsing tags with qualifiers (`+`, `-`)
- ✅ `Adds` getter strips `+` prefix correctly
- ✅ `Removes` getter strips `-` prefix correctly
- ✅ `getByCategory` filters by category
- ✅ Integration test: Adds + filter for Style category

**SongProperty.test.ts** - 47 tests

- ✅ Factory method: `fromDanceFamilyTags` with single/multiple families
- ✅ Factory method: `fromDanceRating` with positive/negative deltas
- ✅ Factory method: `fromAddedTag` / `fromRemovedTag`
- ✅ `FromString` parsing with multiple `=` signs in value

**SongFilter.test.ts** - 17 tests

- ✅ Filter parsing and serialization
- ✅ Description generation for complex filters
- ✅ `familyTag` getter (tested via filter descriptions)

### Manual Testing Completed

- ✅ Single-style dance voting (Waltz)
- ✅ Multi-style dance voting with modal (Cha Cha)
- ✅ Filtered search context auto-selection
- ✅ Multiple family selection in one vote
- ✅ Vote count remains dance-level
- ✅ Edit history format verification

---

## Data Format Examples

### Single Family Vote

**User action:** Upvote Cha Cha, select "American"

**Edit history:**

```
DanceRating=CHA+1
Tag+=Cha Cha:Dance
Tag+:CHA=American:Style
```

### Multiple Family Vote

**User action:** Upvote Jive, select "American" and "International"

**Edit history:**

```
DanceRating=JIV+1
Tag+=Jive:Dance
Tag+:JIV=American:Style|International:Style
```

### Vote Without Family Tag

**User action:** Upvote Salsa (single-style dance)

**Edit history:**

```
DanceRating=SLS+1
Tag+=Salsa:Dance
```

(No family tag needed - single style)

### Search Filter with Family

**Filter string:** `v2-index-CHA|+International:Style-.-.-.-.--.-.`

**Parsed:**

- Dance: `CHA|+International:Style` (DanceQueryItem format)
- Extracted family tag: `"International"` (via `SongFilter.familyTag`)
- Used for auto-selection when voting

---

## Next Steps

### 1. Per-Family Vote Breakdown Display

**Goal:** Show how users voted by style family on song pages

**Concept:**

```
Cha Cha (Total: 42 votes)
┌─────────────────────────────────────────┐
│ American         [28 votes] [18 ▲▼]    │ ← Your vote
│ International    [14 votes] [14 ▲▼]    │
│ Both Families    [5 votes]  [5 ▲▼]     │
│ Unspecified      [3 votes]  [3 ▲▼]     │
└─────────────────────────────────────────┘
```

**Implementation considerations:**

- Parse all style tags from song history across all users
- Aggregate counts by family combination
- Handle "Both Families" (users who selected multiple)
- Show "Unspecified" for votes without family tags
- Highlight user's current selection
- Allow changing family without re-voting (update tag only)

**Data structure:**

```typescript
interface StyleVoteBreakdown {
  danceId: string;
  totalVotes: number;
  styleBreakdown: Array<{
    families: string[]; // e.g., ["American"] or ["American", "International"]
    count: number;
    userVoted: boolean;
  }>;
}
```

**Benefits:**

- Users see community voting patterns
- Encourages style family tagging
- Educational (learn which families are popular for a song)
- Validates that song works for specific style

**Challenges:**

- Parsing tags from all users (not just current user)
- Counting family combinations (American, International, Both)
- UI space (could be collapsed by default)
- Performance (caching breakdown calculations)

---

### 2. User Preference: Default Dance Family

**Goal:** Allow users to set default style family preferences for each dance

**Use cases:**

1. **Voting:** When voting on multi-style dance, pre-select user's preferred family
2. **Search:** Filter search results to preferred families by default
3. **Adding dances:** Auto-tag dances with preferred family when adding to songs
4. **Consistency:** Reflect user's actual dance style (e.g., "I always dance American Rhythm")

**User settings UI:**

```
Dance Preferences
┌─────────────────────────────────────────────────────────┐
│ Cha Cha         [American ▼]   [International]  [Both] │
│ Jive            [American ▼]   [International]  [Both] │
│ Waltz           [Int'l Std ▼]  [Am. Smooth]     [Both] │
│ Foxtrot         [Int'l Std ▼]  [Am. Smooth]     [Both] │
│ Rumba           [American ▼]   [International]  [Both] │
│                                                         │
│ [Apply to all searches]  [Reset to defaults]           │
└─────────────────────────────────────────────────────────┘
```

**Implementation:**

```typescript
interface UserDancePreferences {
  userId: string;
  preferences: {
    [danceId: string]: string[]; // e.g., { "CHA": ["American"] }
  };
  applyToSearch: boolean; // Auto-filter searches by preferences
}

// Storage: User profile or localStorage
// API: GET/PUT /api/user/{userId}/dance-preferences
```

**Integration points:**

1. **FamilyChoiceModal:**

   ```typescript
   // Pre-select user's preferred families
   const userPrefs = getUserDancePreferences(userId);
   const defaultSelection = userPrefs[danceId] ?? [];
   ```

2. **SongFilter auto-filtering:**

   ```typescript
   // If applyToSearch enabled, add family filter to query
   if (userPrefs.applyToSearch && !filter.hasStyleFilter) {
     const preferredFamilies = userPrefs[danceId];
     filter.addStyleFilter(preferredFamilies);
   }
   ```

3. **SongEditor:**
   ```typescript
   // When adding dance without explicit family selection
   private addDanceWithDefaults(danceId: string): void {
     const userPrefs = getUserDancePreferences(this.user);
     const families = userPrefs[danceId];
     this.addDanceWithFamilies(danceId, families);
   }
   ```

**Benefits:**

- Reduces clicks for frequent voters
- Respects user's actual dance style practice
- Optional (can always override in modal)
- Improves search relevance
- Maintains per-song override capability

**Challenges:**

- UI/UX for settings page
- Storage location (server vs localStorage)
- Migration for existing users (default to "Both"?)
- Handling when user's preference changes
- Balancing defaults vs explicit selection

**Settings page considerations:**

- Could be in user profile page
- Could be quick-access dropdown in nav menu
- Should show explanation of what preference does
- Preview how it affects voting/search
- Easy reset to "no preference"

---

### 3. User-Specific Family Tag Search

**Goal:** Enable users to search for songs they personally tagged with specific families

**Current capability:**

- ✅ Search all songs with Cha Cha + International family tag (any user): `CHA|+International:Style`
- ✅ Search songs the user voted for Cha Cha: user-specific vote filter
- ❌ Search songs the user voted AND tagged as International Cha Cha: not yet supported

**Desired queries:**

- "Show me Cha Cha songs I voted for and tagged as International"
- "My American Rumba votes only"
- "Songs where I specified both families"

**Implementation:**

```typescript
// Extend UserQuery to include family tags:
interface UserQuery {
  userId?: string;
  voteType?: "liked" | "disliked" | "edited";
  familyTags?: string[]; // NEW: filter by user's own family tags
}

// Query syntax:
// user:me+CHA|+International:Style
// → Songs where current user voted Cha Cha AND tagged it International

// Filter construction:
const filter = new SongFilter();
filter.dances = "CHA|+International:Style";
filter.user = "me"; // Existing: user's votes
// Need: Combine user filter with family tag filter
```

**Benefits:**

- Users can curate their personal style-specific playlists
- Find songs they've already identified as working for a specific family
- Build competition prep lists ("All my International Standard songs")
- Track style preferences over time

---

### 4. Family-Filtered Search Results UI Enhancement

**Goal:** Better UI for searching songs tagged with certain families (already works, needs better UX)

**Current:** Filter string `CHA|+American:Style` works but requires knowing syntax

**UI enhancement:**

```
Search Results: Cha Cha
┌─────────────────────────────────────────┐
│ Filter by style:                        │
│ [All] [American] [International] [Both] │
│ [My tags only] ← NEW                    │
└─────────────────────────────────────────┘
```

---

### 5. Vote Change Detection & Notification

**Goal:** Alert users if they change their family tag without changing their vote

**Scenario:**

- User previously voted Cha Cha with "American"
- Now voting again with "International"
- Is this an intentional change or accidental re-vote?

**Implementation:**

```typescript
// In upVote/downVote:
const currentVote = this.song.danceVote(danceId);
const currentFamilies = this.song.getFamilyTags(danceId);

if (currentVote && familyTags !== currentFamilies) {
  // Show toast: "Changed style family from American to International"
  // Or confirm: "Update your Cha Cha vote from American to International?"
}
```

---

### 6. Analytics & Insights

**Goal:** Surface interesting patterns about family voting

**Examples:**

- "85% of voters tagged this song as American Cha Cha"
- "This song works equally well for both families"
- "Popular for International (28 votes) but rare for American (3 votes)"

**Implementation:**

- Add computed properties to Song model
- Display insights on song detail pages
- Could influence search ranking

---

## Technical Debt & Future Cleanup

### Debug Console Logs

**Current state:** Extensive `console.log` statements for debugging

**Action needed:**

- Remove or wrap in development-only guards
- Convert to proper logging framework
- Keep critical error logs only

```typescript
// Current:
console.log("Style Families for dance", danceId, styleFamilies);

// Recommended:
if (import.meta.env.DEV) {
  console.debug("Style families:", { danceId, styleFamilies });
}
```

### Filter by Style Button Group

**Current state:** Functional but feels "kludgy" (per original note)

**Improvement ideas:**

- Better visual design (chips instead of buttons?)
- Clear active filter indication
- "Clear filters" button
- Persist filter state in URL
- Show count of filtered results

### Modal State Management

**Current state:** Each component manages its own modal state

**Consideration:** Centralized modal service or Pinia store for modal state

---

## Summary

✅ **Completed:**

- Multi-family vote selection via modal
- Context-aware auto-selection (single family, search filter)
- Proper tag format storage and parsing
- Factory methods eliminate manual string construction
- Comprehensive unit tests (110 total)
- Filter extraction using TagList.Adds

🎯 **Ready for production:**

- User can vote with family specification
- Tags are properly formatted and stored
- Search context provides smart defaults
- Edit history is correct and parseable

🚀 **Future enhancements:**

- Per-family vote breakdown display
- User preference system for default families
- Enhanced search filtering by family
- Vote change detection
- Community insights and analytics
