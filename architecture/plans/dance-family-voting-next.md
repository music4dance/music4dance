# Dance Family Voting: Next Steps

**Type:** Plan
**Status:** Proposed. None of these have started (checked 2026-10-01).
**Last verified:** 2026-10-01

Follow-ons to family-aware dance voting, which is described in
[dance-family-voting](../songs/dance-family-voting.md).

## 1. Per-Family Vote Breakdown Display

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

## 2. User Preference: Default Dance Family

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

## 3. User-Specific Family Tag Search

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

## 4. Family-Filtered Search Results UI Enhancement

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

## 5. Vote Change Detection & Notification

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

## 6. Analytics & Insights

**Goal:** Surface interesting patterns about family voting

**Examples:**

- "85% of voters tagged this song as American Cha Cha"
- "This song works equally well for both families"
- "Popular for International (28 votes) but rare for American (3 votes)"

**Implementation:**

- Add computed properties to Song model
- Display insights on song detail pages
- Could influence search ranking
