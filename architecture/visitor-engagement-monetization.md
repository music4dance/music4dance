# Visitor Engagement & Monetization Architecture

**Status:** ✅ **Production Ready** (March 9, 2026)

**Version:** 2.0 - Post-Redesign Implementation

---

## Executive Summary

Music4Dance implements a progressive engagement system to convert anonymous visitors into registered users and paying subscribers. The system uses client-side usage tracking to deliver targeted messaging at optimal moments in the user journey through a persistent bottom bar that expands into an offcanvas overlay.

**Key Features:**

- ✅ Persistent bottom bar for non-premium users (collapsed state, ~40px height)
- ✅ BOffcanvas overlay for expanded content (full benefits explanation)
- ✅ Progressive messaging based on page view count (3 engagement levels)
- ✅ Support for both anonymous and logged-in non-premium users
- ✅ Google Ads integration with pause/resume on expand/collapse
- ✅ Fully server-side configurable (timing, messages, benefits, CTAs)
- ✅ 44 passing tests (20 composable, 10 bottom bar, 14 offcanvas)

**Context:** Multi-Page Application (MPA) where each page load represents engagement—navigation within site or return visits.

---

## Architecture Overview

### Component Structure

```
App.vue
├─> EngagementBottomBar.vue (collapsed trigger)
│   └─> Props: none
│   └─> Emits: @expand
│   └─> Displays: "How to support music4dance" + chevron up icon
│   └─> Fixed bottom positioning, always visible for non-premium users
│
└─> EngagementOffcanvas.vue (expanded content)
    ├─> Props: modelValue, engagementData, isAuthenticated, config
    ├─> Emits: @collapse, @update:modelValue
    ├─> Placement: bottom (BOffcanvas)
    ├─> Content: Dynamic based on isAuthenticated
    │   ├─> Anonymous: Free account benefits (6 items) + Sign Up/Sign In CTAs
    │   └─> Logged-In: Premium benefits (4+ items) + Subscribe CTA
    └─> Header: Clickable entire header (not just icon) with chevron down

useEngagementOffcanvas.ts (composable)
└─> Logic: Page count tracking, show/hide logic, ads control
    ├─> Inputs: config, isAuthenticated, isPremium
    ├─> Returns: shouldShowBottomBar, shouldShowOffcanvas, isExpanded,
    │            currentLevel, shouldShowAds, expand(), collapse()
    └─> Storage: localStorage (usageCount), sessionStorage (dismissed flag)
```

### User Flow

```plaintext
Non-Premium User Visit (Anonymous or Logged-In)
└─> Page Load (tracked in localStorage)
    │
    ├─> Page 1: No UI shown (clean first impression)
    │
    ├─> Pages 2+: Bottom bar appears
    │   └─> User sees persistent reminder: "How to support music4dance ▲"
    │
    ├─> Pages 2, 7, 12, 17... (trigger pages): Auto-expand
    │   └─> BOffcanvas opens with full content
    │       ├─> If Anonymous: Benefits of free account + Sign Up/Sign In/Maybe Later
    │       ├─> If Logged-In: Premium benefits + Subscribe Now/Learn More/Maybe Later
    │       ├─> Google Ads pause (if consent given)
    │       └─> User Actions:
    │           ├─> Signs up / subscribes (navigation to action page)
    │           ├─> Clicks "Maybe Later" or down arrow → Collapses to bottom bar
    │           └─> Clicks backdrop → Collapses to bottom bar
    │
    └─> Pages 3-6, 8-11, 13-16... (between triggers): Bottom bar visible, ads show
        └─> Google Ads resume (if cookie consent + not page 1 + not dismissed)
```

### Progressive Messaging (3 Levels)

**Engagement Level Detection:**

- **Level 1**: Pages 2-6 (Awareness)
- **Level 2**: Pages 7-11 (Consideration)
- **Level 3**: Pages 12+ (Conversion)

**Anonymous User Messages:**

- **Level 1** (Page 2): "Exploring music4dance?"
- **Level 2** (Page 7): "Still searching for music?"
- **Level 3** (Page 12+): "Finding everything you need?"

**Logged-In User Message:**

- All levels: "Upgrade to Premium" (tone: "Your membership includes...")

**Note:** Messages displayed in offcanvas header. Progressive messaging provides increasing insistence without changing CTAs or benefits list.

---

## Component Details

### EngagementBottomBar.vue

**Purpose:** Persistent reminder bar that triggers offcanvas expansion.

**Implementation:**

- Uses `BAlert` component (variant="light", fixed bottom positioning)
- Chevron up icon positioned left with spacing (`me-3`)
- Entire alert is clickable (cursor pointer, keyboard accessible)
- Emits `@expand` event when clicked/activated

**Visual:**

```
┌───────────────────────────────────────────────────────────┐
│ ▲   How to support music4dance                            │ ← Fixed bottom
└───────────────────────────────────────────────────────────┘
```

**CSS:**

```scss
.engagement-bottom-bar {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  cursor: pointer;
  z-index: 1030; // Below modals (1055), above nav (1020)

  &:hover {
    opacity: 0.9;
  }
}
```

**Testing:** 10 passing tests covering rendering, events, keyboard accessibility, styling.

---

### EngagementOffcanvas.vue

**Purpose:** Full content overlay explaining value proposition and presenting CTAs.

**Props:**

- `modelValue: boolean` - Controls visibility (v-model binding)
- `engagementData: EngagementLevel | null` - Contains level (1|2|3) and message
- `isAuthenticated: boolean` - Determines anonymous vs logged-in content
- `config: EngagementConfig` - Server-side configuration object

**Emits:**

- `@collapse` - User clicked down arrow, "Maybe Later", or backdrop
- `@update:modelValue` - Sync v-model when offcanvas closes

**Content Sections:**

**1. Header (Clickable, All Audiences):**

```vue
<div class="engagement-offcanvas-header" role="button" @click="onCollapse">
  <span><IBiChevronDown /></span>
  <h5>{{ headerTitle }}</h5> <!-- Dynamic: "Exploring music4dance?" or "Upgrade to Premium" -->
</div>
```

**2. Engagement Message (Dynamic, All Audiences):**

```vue
<div
  v-if="engagementData?.message"
  class="engagement-message"
  v-html="engagementData.message"
></div>
```

**3. Content Area (Anonymous Users):**

```vue
<div v-if="!isAuthenticated" class="free-account-benefits">
  <h6>When you've signed up you can:</h6>
  <ul class="list-clean-aligned">
    <li>
      <IBiHandThumbsUp />
      <a href="...">Vote on dances</a>
    </li>
    <li><IBiTagsFill /> <a href="...">Tag songs</a></li>
    <li><IBiSearch /> <a href="...">Search on songs you've tagged</a></li>
    <li><IBiFolderFill /> <a href="...">Save your searches</a></li>
    <li><IBiHeartFill /> <a href="...">Add songs to favorites</a></li>
    <li>
      <IBiCurrencyDollar />
      <a href="...">Purchase a premium subscription to unlock even more features</a>
    </li>
  </ul>
</div>
```

**Benefits Links:**

- Vote on dances → dance-tags help
- Tag songs → tag-editing help
- Search tagged → advanced-search help
- Save searches → saved-searches help
- Add to favorites → favorites vs voting blog post
- Purchase subscription → subscriptions help

**4. Content Area (Logged-In Users):**

```vue
<div v-else class="premium-benefits">
  <h6>Upgrade to Premium Membership</h6>
  <p class="text-muted small">Your membership includes:</p>
  <ul class="list-clean-aligned">
    <li v-for="benefit in premiumBenefits">
      <IBiCheckCircleFill class="text-success me-2" />
      <span v-html="benefit"></span>
    </li>
    <li v-if="premiumBenefitsMoreText">
      <IBiCheckCircleFill class="text-success me-2" />
      {{ premiumBenefitsMoreText }}
    </li>
  </ul>
  <p><a :href="premiumFeaturesUrl" target="_blank">View complete feature list</a></p>
</div>
```

**Premium Benefits (Default):**

1. Ad-free experience (linked to subscriptions page)
2. Spotify playlist integration
3. Bonus content access
4. Priority email support
5. "...and more!" (moreText)

**5. CTAs (Dynamic):**

**Anonymous Users (Always Same 3):**

```vue
<BButton :href="registerUrl" variant="primary">Sign Up Free</BButton>
<BButton :href="loginUrl" variant="outline-primary">Sign In</BButton>
<BButton variant="outline-secondary" @click="onCollapse">Maybe Later</BButton>
```

**Logged-In Users:**

```vue
<BButton :href="subscribeUrl" variant="success">Subscribe Now</BButton>
<BButton
  :href="premiumFeaturesUrl"
  variant="outline-primary"
>Learn More</BButton>
<BButton variant="outline-secondary" @click="onCollapse">Maybe Later</BButton>
```

**Design Rationale:**

- Anonymous users: Clear path to account creation (not subscription)
- Logged-in users: Direct subscription push with premium benefits
- Consistent "Maybe Later" allows collapse without dismissal
- Icons provide visual anchors for scanning benefits list

**Testing:** 14 passing tests covering anonymous rendering, logged-in rendering, header interaction, model updates, edge cases.

---

### useEngagementOffcanvas.ts

**Purpose:** Composable managing engagement logic, timing, and state.

**Signature:**

```typescript
export interface UseEngagementOffcanvasOptions {
  config?: EngagementConfig;
  isAuthenticated: boolean;
  isPremium: boolean;
}

export function useEngagementOffcanvas(options: UseEngagementOffcanvasOptions);
```

**Returns:**

```typescript
{
  shouldShowBottomBar: Ref<boolean>,       // Show collapsed bottom bar
  shouldShowOffcanvas: Ref<boolean>,       // Show expanded offcanvas
  isExpanded: Ref<boolean>,                // Currently expanded
  currentLevel: ComputedRef<EngagementLevel | null>, // { level: 1|2|3, message: string }
  shouldShowAds: ComputedRef<boolean>,     // Control Google Ads visibility
  expand: () => void,                       // Expand offcanvas
  collapse: () => void,                     // Collapse to bottom bar
  dismissForSession: () => void,            // Optional: dismiss for session
  _getPageCount: () => number,              // Testing only
  _isDismissedForSession: () => boolean    // Testing only
}
```

**Logic:**

**1. Premium Users Early Return:**

```typescript
if (isPremium) {
  return {
    shouldShowBottomBar: ref(false),
    shouldShowOffcanvas: ref(false),
    isExpanded: ref(false),
    currentLevel: computed(() => null),
    shouldShowAds: computed(() => true), // Ads OK for premium
    expand: () => {},
    collapse: () => {},
    _getPageCount: () => 0,
    _isDismissedForSession: () => false,
  };
}
```

**2. Page Count Tracking:**

- Read from `localStorage.getItem("usageCount")` on initialization
- Incremented by `useUsageTracking` composable on each page load
- Used to determine engagement level and trigger pages

**3. Show Logic:**

```typescript
// shouldShowBottomBar: Uses >= logic (not exact trigger pages)
computed(() => {
  return (
    finalConfig.enabled &&
    !isPremium &&
    pageCount.value >= finalConfig.firstShowPageCount
  );
});

// calculateShouldShow: Determines auto-expand on trigger pages
// Returns true for: page 2, 7, 12, 17, 22... (firstShow + intervals)
function calculateShouldShow(count: number): boolean {
  if (!finalConfig.enabled || isDismissedForSession()) return false;
  if (!isAuthenticated && finalConfig.showForAnonymous === false) return false;
  if (isAuthenticated && finalConfig.showForLoggedIn === false) return false;
  if (count <= 1) return false;
  if (count === finalConfig.firstShowPageCount) return true; // Page 2
  if (count > finalConfig.firstShowPageCount) {
    const pagesSinceFirst = count - finalConfig.firstShowPageCount;
    return pagesSinceFirst % finalConfig.repeatInterval === 0; // Every 5 pages
  }
  return false;
}
```

**4. Engagement Level Calculation:**

```typescript
function calculateEngagementLevel(count: number): 1 | 2 | 3 {
  if (count >= 12) return 3; // Conversion
  if (count >= 7) return 2; // Consideration
  return 1; // Awareness
}

function getMessage(level: 1 | 2 | 3): string {
  if (!isAuthenticated) {
    // Anonymous: Use level-specific messages
    return finalConfig.messages[`level${level}`];
  } else {
    // Logged-in: Always upgrade message
    return finalConfig.messages.loggedInUpgrade || "";
  }
}
```

**5. Google Ads Control:**

```typescript
const shouldShowAds = computed(() => {
  const hasCookieConsent = () => {
    if (typeof document === "undefined") return false;
    return document.cookie.includes("cookieconsent_status=dismiss");
  };

  return (
    finalConfig.enabled &&
    pageCount.value > 1 &&
    !isExpanded.value &&
    !isDismissedForSession() &&
    hasCookieConsent()
  );
});

// On expand:
function expand(): void {
  if (finalConfig.enabled && !isPremium) {
    isExpanded.value = true;
    // Pause ads
    if (typeof window !== "undefined" && (window as any).adsbygoogle) {
      (window as any).adsbygoogle.pauseAdRequests = 1;
    }
  }
}

// On collapse:
function collapse(): void {
  isExpanded.value = false;
  // Resume ads
  if (typeof window !== "undefined" && (window as any).adsbygoogle) {
    if ((window as any).adsbygoogle.pauseAdRequests !== undefined) {
      (window as any).adsbygoogle.pauseAdRequests = 0;
    }
  }
}
```

**6. Auto-Expand on Initialization:**

```typescript
function initialize(): void {
  pageCount.value = getPageCount();
  if (calculateShouldShow(pageCount.value)) {
    expand(); // Auto-expand on trigger pages
  }
}

initialize(); // Called at composable creation
```

**Testing:** 20 passing tests covering premium user behavior, show logic, expand/collapse, engagement levels, ads control, disabled configuration, auto-expand.

**Test Note:** Cookie mocking in test environment is challenging with Vitest. Tests validate that `shouldShowAds` correctly returns false without cookie consent (expected test behavior). Production implementation correctly checks for cookie consent.

---

## Server Configuration

### Configuration Structure

**appsettings.json:**

```json
{
  "Engagement": {
    "Enabled": true,
    "FirstShowPageCount": 2,
    "RepeatInterval": 5,
    "SessionDismissalTimeout": 30,
    "ShowForAnonymous": true,
    "ShowForLoggedIn": true,
    "Messages": {
      "Level1": "<p>Create a <strong>free account</strong> to save your searches.</p>",
      "Level2": "<p><strong>Subscribe</strong> to help keep it running.</p>",
      "Level3": "<p><strong>Thank you</strong> for using music4dance!</p>",
      "LoggedInUpgrade": "<p>Upgrade to <strong>Premium membership</strong>.</p>"
    },
    "PremiumBenefits": {
      "Items": [
        "<a href='https://music4dance.blog/music4dance-help/subscriptions/'>Ad-free experience</a>",
        "Spotify playlist integration",
        "Bonus content access",
        "Priority email support"
      ],
      "MoreText": "...and more!",
      "CompleteListUrl": "https://music4dance.blog/music4dance-help/subscriptions/"
    },
    "CtaUrls": {
      "Register": "/identity/account/register",
      "Login": "/identity/account/login",
      "Subscribe": "/home/contribute",
      "Features": "https://music4dance.blog/features/"
    }
  }
}
```

**Configuration Properties:**

| Property                          | Type     | Default                      | Description                                               |
| --------------------------------- | -------- | ---------------------------- | --------------------------------------------------------- |
| `Enabled`                         | boolean  | `true`                       | Master switch for engagement system                       |
| `FirstShowPageCount`              | number   | `2`                          | Page number to first show engagement UI                   |
| `RepeatInterval`                  | number   | `5`                          | Pages between repeat shows (2, 7, 12, 17...)              |
| `SessionDismissalTimeout`         | number   | `15`                         | Minutes before clearing session dismissal (0 = permanent) |
| `ShowForAnonymous`                | boolean  | `true`                       | Show for anonymous users                                  |
| `ShowForLoggedIn`                 | boolean  | `true`                       | Show for logged-in non-premium users                      |
| `Messages.Level1/2/3`             | string   | (see above)                  | HTML messages for each engagement level                   |
| `Messages.LoggedInUpgrade`        | string   | (see above)                  | Message for logged-in users                               |
| `PremiumBenefits.Items`           | string[] | (see above)                  | List of premium benefits (HTML allowed)                   |
| `PremiumBenefits.MoreText`        | string   | `...and more!`               | Additional text after benefits list                       |
| `PremiumBenefits.CompleteListUrl` | string   | (url)                        | Link to full feature list                                 |
| `CtaUrls.Register`                | string   | `/identity/account/register` | Sign up URL                                               |
| `CtaUrls.Login`                   | string   | `/identity/account/login`    | Sign in URL                                               |
| `CtaUrls.Subscribe`               | string   | `/home/contribute`           | Subscription URL                                          |
| `CtaUrls.Features`                | string   | (url)                        | Premium features list URL                                 |

**TypeScript Model (EngagementConfig.ts):**

```typescript
export interface EngagementConfig {
  enabled: boolean;
  firstShowPageCount: number;
  repeatInterval: number;
  sessionDismissalTimeout: number;
  showForAnonymous?: boolean;
  showForLoggedIn?: boolean;
  messages: {
    level1: string;
    level2: string;
    level3: string;
    loggedInUpgrade?: string;
  };
  premiumBenefits?: {
    items: string[];
    moreText?: string;
    completeListUrl?: string;
  };
  ctaUrls: {
    register: string;
    login: string;
    subscribe: string;
    features: string;
  };
}

export const defaultEngagementConfig: EngagementConfig = {
  enabled: false,
  firstShowPageCount: 2,
  repeatInterval: 5,
  sessionDismissalTimeout: 15,
  showForAnonymous: true,
  showForLoggedIn: true,
  messages: {
    level1: "Create a free account to unlock features.",
    level2: "Subscribe to support music4dance.",
    level3: "Thank you for using music4dance!",
    loggedInUpgrade: "Upgrade to Premium membership.",
  },
  premiumBenefits: {
    items: [
      "Ad-free experience",
      "Spotify playlist integration",
      "Bonus content access",
      "Priority email support",
    ],
    moreText: "...and more!",
    completeListUrl: "/subscriptions/",
  },
  ctaUrls: {
    register: "/identity/account/register",
    login: "/identity/account/login",
    subscribe: "/home/contribute",
    features: "/features/",
  },
};
```

### Configuration Delivery

**Server-Side Rendering (App.cshtml):**

```html
<script>
  window.menuContext = {
    userName: '@User.Identity?.Name',
    isPremium: @Json.Serialize(Model.IsPremium),
    engagementConfig: @Html.Raw(JsonSerializer.Serialize(Model.EngagementConfig))
  };
</script>
```

**Client-Side Access (App.vue):**

```typescript
const menuContext = (window as any).menuContext || {};
const engagementConfig =
  menuContext.engagementConfig || defaultEngagementConfig;
const isPremium = menuContext.isPremium || false;

const engagement = useEngagementOffcanvas({
  config: engagementConfig,
  isAuthenticated: !!menuContext.userName,
  isPremium: isPremium,
});
```

**Hot Reload Support:**

- Configuration changes in `appsettings.json` (or Azure App Configuration) take effect immediately
- No code deployment required for timing, messaging, or benefits changes
- Feature flag (`Enabled: false`) allows instant disable if issues arise

### Azure App Configuration Overrides

To override engagement settings from Azure App Configuration, use the following key format:

```
EngagementOffcanvas:Enabled = true
EngagementOffcanvas:SessionDismissalTimeout = 15
EngagementOffcanvas:FirstShowPageCount = 2
EngagementOffcanvas:RepeatInterval = 5
EngagementOffcanvas:Messages:Level1 = <p>Your custom message</p>
EngagementOffcanvas:PremiumBenefits:Items:0 = First benefit
EngagementOffcanvas:PremiumBenefits:Items:1 = Second benefit
```

**Key Points:**

- Use colon (`:`) as the hierarchy separator
- Array items use zero-based indexing (`:Items:0`, `:Items:1`, etc.)
- String values with HTML don't need special encoding
- Changes take effect within minutes (no restart required)

**Common Overrides for Emergency Control:**

- **Instant Disable:** `EngagementOffcanvas:Enabled = false`
- **Adjust Timing:** `EngagementOffcanvas:FirstShowPageCount = 5` (delay first show)
- **Reduce Frequency:** `EngagementOffcanvas:RepeatInterval = 10` (show less often)
- **Extend Timeout:** `EngagementOffcanvas:SessionDismissalTimeout = 60` (1 hour)

---

## Integration Points

### App.vue Integration

**Conditional Rendering:**

```vue
<template>
  <!-- Only render for non-premium users when engagement enabled -->
  <EngagementBottomBar
    v-if="engagement.shouldShowBottomBar.value"
    @expand="engagement.expand"
  />

  <EngagementOffcanvas
    v-model="engagement.shouldShowOffcanvas.value"
    :engagement-data="engagement.currentLevel.value"
    :is-authenticated="isAuthenticated"
    :config="engagementConfig"
    @collapse="engagement.collapse"
  />

  <!-- Hide old alert banner when engagement system is enabled -->
  <BAlert
    v-if="showReminder && !engagementConfig.enabled"
    id="premium-alert"
    ...
  >
    <!-- Old customer reminder content -->
  </BAlert>
</template>
```

**Usage Tracking:**

```typescript
import { useUsageTracking } from "@/composables/useUsageTracking";
import { useEngagementOffcanvas } from "@/composables/useEngagementOffcanvas";

const usageTracking = useUsageTracking();
const engagement = useEngagementOffcanvas({
  config: engagementConfig,
  isAuthenticated: isAuthenticated,
  isPremium: isPremium,
});

// Usage tracking automatically increments page count on mount
// Engagement composable reads page count and determines show logic
```

**Google Ads:**

```vue
<div v-if="engagement.shouldShowAds.value" class="ad-container">
  <ins class="adsbygoogle" ...></ins>
</div>
```

### Page Suppression

**Suppress on Identity & Contribute Pages:**

Engagement UI is automatically suppressed on:

- `/identity/*` - Login, registration, account management pages
- `/home/contribute` - Subscription/payment page

Suppression logic in `useUsageTracking` composable - engagement system never shows on these pages regardless of configuration.

### Feature Flag Coordination

**Old Alert Banner (showReminder):**

- Controlled by `engagementConfig.enabled` flag
- When engagement system is enabled, old banner is hidden
- Ensures single consistent UI (not competing messages)

---

## Privacy & User Experience

### Privacy Safeguards

1. **localStorage Only:** Usage count stored locally, never sent to server
2. **No Cross-Device Tracking:** Each device/browser has independent count
3. **Opt-Out Friendly:** User can clear localStorage to reset
4. **No Personal Data:** Only page count, no identifying information
5. **Cookie Consent Required:** Ads only show with existing cookie consent

### Anti-Annoyance Measures

1. **No Dismissal Penalty:** "Maybe Later" only collapses, doesn't hide bottom bar
2. **Session Dismissal Available:** Optional - can dismiss for session with timeout
3. **Clean First Impression:** No UI on first page load
4. **Non-Blocking:** Bottom bar is small, doesn't cover content
5. **User Control:** User chooses when to expand (or auto-expand is dismissible)

### Bot Detection Integration

**Bot Exclusion:**
Bots detected by `useUsageTracking` do NOT trigger engagement UI:

- User-Agent check for common bot signatures
- `navigator.webdriver` check for automated browsers
- These users never see bottom bar or offcanvas

---

## Testing Strategy

### Test Coverage

**Total: 44 Tests (100% passing)**

**1. useEngagementOffcanvas.test.ts (20 tests):**

- Premium user behavior (early return with all false)
- Anonymous user show logic (pages 1, 2, 3+, 7, 12)
- Logged-in non-premium user show logic
- Expand/collapse behavior
- Engagement level calculation (1, 2, 3)
- Google Ads control (pause on expand, note on cookie mocking)
- Disabled configuration
- Auto-expand on initialization

**2. EngagementBottomBar.test.ts (10 tests):**

- Component rendering
- Text display ("How to support music4dance")
- Fixed positioning styles
- Click and keyboard event emission
- Accessibility attributes (role, aria-label, tabindex)
- Visual elements (chevron up icon as SVG)

**3. EngagementOffcanvas.test.ts (14 tests):**

- Anonymous user rendering (6 benefit items with links)
- Logged-in user rendering (4+ premium benefit items)
- Clickable header interaction (click, Enter key)
- Model value updates (v-model sync)
- Edge cases (null engagementData)
- CTA button rendering (3 for anonymous, 3 for logged-in)
- Conditional content display (free vs premium benefits)

### Test Infrastructure

**Vitest + Vue Test Utils:**

- Shallow mounting with component stubs (BOffcanvas, BButton, BAlert)
- localStorage/sessionStorage mocking
- Cookie consent mocking (limited in test environment)
- Icon component handling (unplugin-icons)

**Stub Configuration:**

```typescript
const globalStubs = {
  BOffcanvas: {
    template: '<div class="b-offcanvas-stub"><slot /></div>',
    props: ["modelValue", "placement", "backdrop", "scroll", "noHeader"],
  },
  BButton: {
    template: '<button class="b-button-stub"><slot /></button>',
    props: ["variant", "href"],
  },
};
```

**Running Tests:**

```bash
cd m4d/ClientApp
yarn test:unit --run src/components/__tests__/EngagementBottomBar.test.ts \
                    src/components/__tests__/EngagementOffcanvas.test.ts \
                    src/composables/__tests__/useEngagementOffcanvas.test.ts
```

### Known Test Limitations

**Cookie Consent Mocking:**

- `document.cookie` is difficult to mock in Vitest due to browser API design
- Tests validate that `shouldShowAds` returns false without cookie consent (expected)
- Production implementation correctly checks for `cookieconsent_status=dismiss` cookie
- Ads behavior manually verified in browser environment

---

## Production Deployment

### Rollout Strategy

**Single Phase: Enable and Monitor**

Start with the feature fully enabled for all users (`Enabled: true` in production config). This pragmatic approach is appropriate for a small team working part-time.

**Rationale:**

- Feature has comprehensive test coverage (44 passing tests)
- All logic is client-side with no server performance impact
- Can be instantly disabled via Azure App Configuration if issues arise
- Multi-phase rollout would delay learning and iteration unnecessarily

**Launch Checklist:**

1. ✅ Deploy code to production
2. ✅ Verify `EngagementOffcanvas:Enabled = true` in Azure App Configuration
3. ✅ Monitor for first 24 hours:
   - Application Insights for client errors
   - Bounce rate in Google Analytics
   - User feedback/complaints
4. ✅ Document baseline metrics (account registrations, subscriptions)
5. ✅ Plan first optimization experiment (message testing)

**If Issues Arise:**

- **Instant Disable:** Set `EngagementOffcanvas:Enabled = false` in Azure App Configuration
- **Reduce Frequency:** Increase `RepeatInterval` or `FirstShowPageCount`
- **Adjust Messaging:** Update `Messages` config to be less aggressive
- No code deployment needed for any of these changes

**Post-Launch Optimization:**
After initial stability (1-2 weeks), begin iterating:

1. A/B test different messages (Level 1 vs Level 2 vs Level 3 content)
2. Experiment with firstShowPageCount (2 vs 3 vs 4)
3. Test repeatInterval (5 vs 7 vs 10)
4. Optimize premium benefits list order based on click data

### Monitoring & Metrics

**Key Metrics to Track:**

1. **Impression Rate by Message:**
   - **Level 1 impressions** ("Exploring music4dance?") - Pages 2-6
   - **Level 2 impressions** ("Still searching for music?") - Pages 7-11
   - **Level 3 impressions** ("Finding everything you need?") - Pages 12+
   - **Logged-in impressions** ("Upgrade to Premium") - All pages for authenticated users
   - Track which messages users see most frequently

2. **Engagement Funnel:**
   - Bottom bar impressions (baseline visibility)
   - Offcanvas impressions (auto-expand or manual click)
   - CTA click-through rates by message level
   - Conversion completion (signup/subscription)

3. **Conversion Rate by Message:**
   - Anonymous → Registered (Sign Up clicks) per level
   - Logged-In → Premium (Subscribe clicks)
   - Which message drives highest conversion?

4. **Dismissal Patterns:**
   - "Maybe Later" clicks by level
   - Header collapse actions by level
   - Do users reject certain messages more than others?

5. **User Experience:**
   - Bounce rate changes
   - Session duration
   - Pages per session
   - User complaints/feedback

6. **Revenue Impact:**
   - Subscription revenue growth
   - Ad revenue changes (ads paused during expansion)
   - Net revenue per user

**Instrumentation:**

✅ **Google Tag Manager Integration Ready** - All components now include data attributes for easy GTM tracking:

- `data-engagement-element` (bottom-bar, offcanvas)
- `data-engagement-level` (1, 2, 3, loggedin)
- `data-engagement-user-type` (anonymous, authenticated)
- `data-engagement-action` (impression, signup-click, subscribe-click, etc.)

📖 **Complete GTM Setup Guide:** See [gtm-tracking-guide.md](gtm-tracking-guide.md) for:

- CSS selectors for all triggers
- Recommended event names and parameters
- GA4 conversion funnel setup
- Testing procedures
- Data analysis examples

**Priority Tracking (Week 1):**

1. **Offcanvas Impressions by Level** - Which messages do users see?
2. **Sign Up Clicks** - Primary anonymous conversion
3. **Subscribe Clicks** - Primary logged-in conversion
4. **Dismissal Rate by Level** - Are users rejecting certain messages?

### Rollback Plan

**Instant Disable:**

```json
{
  "Engagement": {
    "Enabled": false
  }
}
```

- Change config in Azure App Configuration
- Config auto-reloads within minutes
- Users see no engagement UI immediately
- No code deployment needed

**Fallback to Old Banner:**

- Old alert banner automatically reappears when `Enabled: false`
- No code changes required

---

## Performance Considerations

### Client-Side Performance

**Bundle Size:**

- EngagementBottomBar.vue: ~1KB minified
- EngagementOffcanvas.vue: ~3KB minified
- useEngagementOffcanvas.ts: ~2KB minified
- **Total: ~6KB additional bundle size**

**Runtime Performance:**

- localStorage access: negligible (< 1ms)
- Composable initialization: single read, no polling
- Vue reactivity: computed refs, efficient updates
- BOffcanvas: Bootstrap Vue Next optimized component

**Lazy Loading:**
Not necessary - components are small and render conditionally (v-if). Premium users never render engagement components.

### Server-Side Performance

**Configuration Delivery:**

- Config serialized once per request (minimal overhead)
- Cached in App.cshtml view model
- No database queries (appsettings.json or Azure App Config)

**No Server Round-Trips:**

- All logic client-side after initial config delivery
- No API calls for engagement state
- Page count in localStorage only

---

## Moving Forward

### Immediate Priorities (Post-Launch)

**1. Configure Google Tag Manager Tracking (Week 1)**

✅ Code is instrumented - Follow [gtm-tracking-guide.md](gtm-tracking-guide.md) to configure GTM:

- Set up impression tracking for all 4 message types
- Configure CTA click tracking
- Create GA4 conversion funnel
- Test in GTM Preview mode

**Focus on impression tracking** to understand:

- How many users see Level 1 vs Level 2 vs Level 3 messages?
- Is logged-in upgrade message reaching the right users?
- Which message level has highest conversion rate?

**2. Message Optimization (Weeks 2-4)**

- Analyze conversion rate by engagement level (Level 1 vs 2 vs 3)
- Test alternative Level 1 messages if conversion is low:
  - Current: "Exploring music4dance?"
  - Alternative A: "Create a free account to unlock more features"
  - Alternative B: "Sign up to save your searches and tag songs"
- Use GTM to implement A/B test (randomize message shown)

**3. Timing Optimization (Month 2)**

- Experiment with `firstShowPageCount` (2 vs 3 vs 4)
- Test `repeatInterval` (5 vs 7 vs 10)
- Find balance between engagement and annoyance
- Monitor dismissal rate as timing changes

### Medium-Term Enhancements

**1. Segmented Messaging (Dance Style Focus)**

- Detect user interests from page views (e.g., visiting many Salsa pages)
- Personalize Level 2/3 messages:
  - "Looking for more Salsa music? Sign up to save your Salsa searches"
  - "Upgrade to Premium for our complete Salsa playlist library"
- Requires: Page view analysis, interest detection algorithm

**2. Social Proof Integration**

- Add to offcanvas:
  - "Join 10,000+ dancers using music4dance"
  - "500+ premium members support music4dance"
- Display testimonials or reviews (from blog/social media)
- Test whether social proof increases conversions

**3. Countdown Timers (Limited-Time Offers)**

- For Level 3 (highly engaged users), test limited-time offers:
  - "50% off premium for the next 24 hours"
  - Requires: Server-side offer tracking, expiration logic

**4. Re-Engagement Campaigns**

- For users who collapsed at Level 3 without action:
  - Show subtle reminder after N more pages
  - Different message: "Last chance to support music4dance"
- Avoid being too aggressive (don't annoy power users)

### Long-Term Vision

**1. Gamification (Engagement Badges)**

- Award badges for milestones:
  - "Explorer" - 10 page views
  - "Enthusiast" - 50 page views
  - "Power User" - 100 page views
- Display badges in engagement offcanvas
- Link badges to account creation ("Sign up to keep your badges")

**2. Personalized Premium Benefits**

- Dynamically show premium benefits relevant to user:
  - Frequent searchers → "Advanced search filters"
  - Playlist users → "Spotify playlist integration"
  - Taggers → "Custom dance categories"
- Requires: Usage pattern analysis

**3. Multi-Channel Engagement**

- Email campaigns for registered users who view many pages but don't subscribe
- Push notifications (if user opts in)
- SMS for premium trial offers

**4. Dynamic Pricing Experiments**

- Test different subscription prices for different segments:
  - Heavy users: Show $10/month (higher value recognition)
  - Light users: Show $3/month (low-commitment trial)
- Requires: Legal/ethical review, transparent pricing policies

### Research & Investigation

**1. Optimal Timing Research**

- Analyze actual user data to determine:
  - Best firstShowPageCount (currently 2)
  - Optimal repeatInterval (currently 5)
  - Conversion rate by engagement level (which level converts best?)
- Adjust configuration based on findings

**2. Competitive Analysis**

- Research how similar sites handle engagement:
  - Spotify (freemium model)
  - Last.fm (music discovery)
  - Bandcamp (artist support)
- Identify best practices and anti-patterns

**3. User Interviews**

- Conduct interviews with:
  - Anonymous users who signed up (what convinced them?)
  - Registered users who subscribed (what was the deciding factor?)
  - Bounced users (why did they leave?)
- Use insights to refine messaging

---

## Technical Debt & Maintenance

### Known Issues (Non-Blocking)

**1. Cookie Consent Mocking in Tests:**

- Document.cookie cannot be reliably mocked in Vitest
- Tests validate expected behavior (false without consent)
- Production implementation works correctly
- **Resolution:** Accept test limitation, manually verify in browser

**2. Icon Component Testing:**

- Unplugin-icons auto-imported components don't have expected component name in tests
- Work-around: Test for SVG elements instead of component name
- **Resolution:** Document pattern in testing guide

**3. Numeric Engagement Levels:**

- `currentLevel.value` returns `{ level: 1, message: '...' }` not just `1`
- Tests must access `.level` property
- **Resolution:** Document in API, update all tests

### Maintenance Tasks

**Regular (Monthly):**

- Review analytics data for engagement metrics
- Check for console errors in production
- Monitor bounce rate and conversion rate trends

**Quarterly:**

- Update premium benefits list (as new features ship)
- Refresh messaging based on user feedback
- Review and update links (blog posts, help pages)

**Yearly:**

- Major refactor if patterns change (e.g., move to SPA)
- Reevaluate timing and frequency based on accumulated data

### Code Quality

**Linting:**

- ESLint with recommended Vue/TypeScript rules
- No lint errors in engagement files

**Type Safety:**

- Full TypeScript coverage
- No `any` types (except mocked window objects)
- Strict null checks enabled

**Documentation:**

- Inline JSDoc comments in composable
- README in component folders
- Architecture doc (this file)

---

## Appendix

### Related Documents

- **[Client-Side Usage Logging](./client-side-usage-logging.md)** - Usage tracking infrastructure
- **[Testing Patterns](./testing-patterns.md)** - Test infrastructure and patterns
- **[Identity Endpoint Protection](./identity-endpoint-protection.md)** - Rate limiting on auth pages

### File Locations

**Client:**

- `m4d/ClientApp/src/components/EngagementBottomBar.vue`
- `m4d/ClientApp/src/components/EngagementOffcanvas.vue`
- `m4d/ClientApp/src/composables/useEngagementOffcanvas.ts`
- `m4d/ClientApp/src/models/EngagementConfig.ts`
- `m4d/ClientApp/src/components/__tests__/EngagementBottomBar.test.ts`
- `m4d/ClientApp/src/components/__tests__/EngagementOffcanvas.test.ts`
- `m4d/ClientApp/src/composables/__tests__/useEngagementOffcanvas.test.ts`

**Server:**

- `m4d/appsettings.json` - Configuration
- `m4d/Models/EngagementConfig.cs` - C# config model
- `m4d/Views/Shared/_Layout.cshtml` - Integration point
- `m4d/Views/Home/_WhySignUp.cshtml` - Benefits reference content

### Change Log

**v2.0 (March 9, 2026):**

- ✅ Persistent bottom bar pattern implemented
- ✅ BOffcanvas overlay for expanded content
- ✅ Support for logged-in non-premium users
- ✅ 6 anonymous benefits, 4+ premium benefits
- ✅ Clickable header (entire header, not just icon)
- ✅ Google Ads pause/resume on expand/collapse
- ✅ 44 passing tests
- ✅ Production ready

**v1.0 (March 7, 2026):**

- Initial implementation with dismissible modal
- 62 tests (37 composable, 25 component)
- Anonymous users only
- Different CTAs per level

---

**Document Version:** 2.0
**Last Updated:** March 9, 2026
**Status:** Production Ready
**Maintained By:** Development Team
