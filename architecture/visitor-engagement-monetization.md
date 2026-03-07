# Visitor Engagement & Monetization Architecture

## Overview

Music4Dance needs a strategic approach to convert anonymous visitors into registered users and ultimately paying subscribers. This document outlines a progressive engagement system that uses client-side usage tracking to deliver targeted messaging at optimal moments in the user journey.

**Context:** This is a **Multi-Page Application (MPA)**, where each page load represents user engagement—whether through navigation within the site or returning from an external source. We track cumulative page loads as a proxy for engagement level, without distinguishing navigation from return visits.

**Status:** 📋 **Planning Phase**

**Dependencies:**

- ✅ Client-side usage tracking (production ready)
- ✅ localStorage-based page load counting
- ✅ Bot detection infrastructure
- ✅ bootstrap-vue-next BOffcanvas component (existing)
- 🔨 Engagement offcanvas component (to be built)
- 🔨 Google Ads integration logic (to be built)

**Related Documents:**

- [Client-Side Usage Logging](./client-side-usage-logging.md) - Tracking infrastructure
- [Testing Patterns](./testing-patterns.md) - Test infrastructure

---

## 1. Problem Statement

### Current Challenges

1. **Ad Blockers**: Significant portion of users block Google Ads, reducing revenue
2. **Browser Restrictions**: Private browsing modes block ads by default
3. **Low Conversion Rates**: Anonymous visitors don't understand value proposition
4. **Account Creation Friction**: No clear nudge toward registration
5. **Timing**: Current alerts shown to wrong audience or at wrong time

### Success Metrics

- **Primary**: Increase subscription conversion rate by 20%
- **Secondary**: Increase account registrations by 30%
- **Tertiary**: Maintain or improve user satisfaction (< 5% bounce rate increase)

---

## 2. User Journey & Engagement Strategy

### 2.1 Multi-Page Application Context

**Important:** Music4Dance is an MPA where each page load could represent:

- **Navigation**: User clicking through dance pages, searching for songs, exploring features
- **Return Visit**: User coming back hours or days later via bookmark, search, or direct link

**Our Approach:** We treat **all page loads as engagement signals** without attempting to distinguish between navigation and return visits. This simplifies implementation while still providing valuable engagement data:

- More page loads = more engagement (regardless of how they occurred)
- Users navigating through multiple pages in one session are engaged
- Users returning multiple times are also engaged
- Both behaviors signal value and justify engagement messaging

**Future Enhancement:** Could use `document.referrer` or timestamp analysis to distinguish navigation vs. return, but that adds complexity without clear benefit at this stage.

### 2.2 Progressive Engagement Model

**Core Principle**: Meet users where they are in their journey with appropriate messaging.

```
Page Load 1 (First Impression)
├─ Goal: No friction, let them explore
├─ Show: Nothing (clean experience)
└─ Ads: None

Page Load 2 (Initial Engagement)
├─ Goal: Introduce value proposition
├─ Show: Engagement Offcanvas (Level 1)
│   ├─ Message: "Exploring music4dance? Here's what we offer"
│   ├─ CTA: "Create Free Account" (primary)
│   └─ CTA: "Learn More" (secondary)
└─ Ads: None

Page Loads 3-6 (Evaluation Phase)
├─ Goal: Soft monetization, let them explore more
├─ Show: Nothing
└─ Ads: Google Ads (soft sell)

Page Load 7 (Sustained Engagement)
├─ Goal: Convert to registered user or subscriber
├─ Show: Engagement Offcanvas (Level 2)
│   ├─ Message: "You're finding what you need! Help keep m4d running"
│   ├─ CTA: "Subscribe ($5/month)" (primary)
│   └─ CTA: "Create Free Account" (secondary)
└─ Ads: None

Page Loads 8-11 (Continued Engagement)
├─ Goal: Soft monetization
├─ Show: Nothing
└─ Ads: Google Ads

Page Loads 12, 17, 22... (Every 5 pages)
├─ Goal: Direct conversion ask
├─ Show: Engagement Offcanvas (Level 3)
│   ├─ Message: Stronger subscription pitch
│   ├─ Feature highlights
│   └─ Social proof
└─ Ads: None
```

### 2.3 Engagement Levels

**Level 1: Awareness (Page Load 2)**

- **Audience**: Users on their second page (navigation or return)
- **Goal**: Introduce platform, encourage account creation
- **Tone**: Friendly, informative, session-agnostic
- **Message**:
  > "**Exploring music4dance?** We're the best resource for matching music to dance styles. Create a **free account** to save your searches, build playlists, and unlock more features."
- **CTAs**:
  - Primary: "Create Free Account"
  - Secondary: "See Features"
  - Tertiary: "Maybe Later" (dismiss)
- **Why This Works**: Doesn't assume return visit, works whether they're navigating or came back

**Level 2: Consideration (Page Load 7)**

- **Audience**: Engaged users (7 pages across any number of sessions)
- **Goal**: Explain value of contribution
- **Tone**: Appreciative, value-focused
- **Message**:
  > "**You're finding what you need!** We're glad music4dance is helping you discover great dance music. The site is free, but costs real money to run. **Create an account** to unlock extra features, or **subscribe** to support the service and get premium perks."
- **CTAs**:
  - Primary: "Subscribe ($5/month)"
  - Secondary: "Create Free Account"
  - Tertiary: "See Premium Features"
  - Quaternary: "Dismiss"
- **Why This Works**: Acknowledges engagement without assuming frequency

**Level 3: Conversion (Page Load 12+)**

- **Audience**: Highly engaged anonymous users (12+ pages)
- **Goal**: Direct conversion to paid subscriber
- **Tone**: Direct, value proposition focused
- **Message**:
  > "**You've loaded {count} pages** on music4dance – clearly it's valuable to you! Help us keep the lights on and the database growing. Premium subscribers get:<br/>
  > • Advanced search filters<br/>
  > • Custom playlists with Spotify integration<br/>
  > • Priority support<br/>
  > • No ads, ever<br/>
  > **Just $5/month** supports real dancers helping real dancers."
- **CTAs**:
  - Primary: "Subscribe Now"
  - Secondary: "See All Premium Features"
  - Tertiary: "Create Free Account"
  - Quaternary: "Dismiss"
- **Why This Works**: Uses concrete "page loads" metric, emphasizes value received

### 2.4 Ad Display Strategy

**Rules:**

1. **Page Load 1**: No ads (clean first impression)
2. **Page Loads 2, 7, 12, 17...**: No ads (offcanvas showing)
3. **All other page loads**: Google Ads displayed
4. **Logged-in users**: No ads, show support banner (current behavior)
5. **Premium subscribers**: No ads, no banners

**Rationale:**

- First page: Remove all friction, let users experience core value
- Offcanvas pages: Don't compete with engagement message
- Between offcanvas: Monetize with ads, less aggressive than offcanvas
- Logged-in: Current strategy works (banner nudges toward subscription)

---

## 3. Technical Architecture

### 3.1 System Components

```
┌──────────────────────────────────────────┐
│         Browser (Client)                 │
│                                          │
│  ┌────────────────────────────────┐    │
│  │  useUsageTracking              │    │
│  │  - Page count in localStorage  │    │
│  │  - Bot detection               │    │
│  └────────┬───────────────────────┘    │
│           │                              │
│  ┌────────▼───────────────────────┐    │
│  │  useEngagementOffcanvas (NEW) │    │
│  │  - Calculate if show offcanvas │    │
│  │  - Determine engagement level  │    │
│  │  - Session dismissal tracking  │    │
│  │  - Google Ads control          │    │
│  └────────┬───────────────────────┘    │
│           │                              │
│  ┌────────▼───────────────────────┐    │
│  │  EngagementOffcanvas.vue (NEW) │    │
│  │  - BOffcanvas (placement=bottom)│   │
│  │  - Multi-level messaging       │    │
│  │  - CTA buttons                 │    │
│  │  - Bootstrap animations        │    │
│  └────────────────────────────────┘    │
│                                          │
│  ┌────────────────────────────────┐    │
│  │  GoogleAdsController (NEW)     │    │
│  │  - Show/hide based on page     │    │
│  │  - Respect offcanvas timing    │    │
│  └────────────────────────────────┘    │
└──────────────────────────────────────────┘
            │
            │ Configuration loads from server
            │
┌───────────▼───────────────────────────────┐
│      Server (.NET)                        │
│                                           │
│  ┌─────────────────────────────────┐    │
│  │  _head.cshtml                   │    │
│  │  - Populate menuContext with    │    │
│  │    engagement config            │    │
│  │  - Feature flags                │    │
│  └─────────────────────────────────┘    │
│                                           │
│  ┌─────────────────────────────────┐    │
│  │  appsettings.json               │    │
│  │  - EngagementModal config       │    │
│  │  - Feature flags                │    │
│  │  - A/B test variants            │    │
│  └─────────────────────────────────┘    │
└───────────────────────────────────────────┘
```

### 3.2 New Components

#### 3.2.1 Composable: `useEngagementOffcanvas.ts`

**Location:** `m4d/ClientApp/src/composables/useEngagementOffcanvas.ts`

**Purpose:** Encapsulate logic for when and what to show users based on page load count

**Key Logic:**

```typescript
interface EngagementConfig {
  enabled: boolean;
  firstShowPageCount: number; // Default: 2 (show on 2nd page load)
  repeatInterval: number; // Default: 5 (show every 5 pages after first)
  sessionDismissalTimeout: number; // Default: 3600000 (1 hour)
  messages: {
    level1: string;
    level2: string;
    level3: string;
  };
  ctaUrls: {
    register: string;
    subscribe: string;
    features: string;
  };
}

export function useEngagementOffcanvas(config: EngagementConfig) {
  const pageCount = ref(getPageCount()); // From useUsageTracking
  const sessionDismissed = ref(isSessionDismissed());

  // Calculate if offcanvas should show
  const shouldShowOffcanvas = computed(() => {
    if (!config.enabled) return false;
    if (sessionDismissed.value) return false;
    if (pageCount.value < config.firstShowPageCount) return false;

    // Page 2, then every 5 pages (7, 12, 17, 22...)
    if (pageCount.value === config.firstShowPageCount) return true;
    if (pageCount.value > config.firstShowPageCount) {
      const pagesSinceFirst = pageCount.value - config.firstShowPageCount;
      return pagesSinceFirst % config.repeatInterval === 0;
    }

    return false;
  });

  // Determine engagement level based on page count
  const engagementLevel = computed(() => {
    if (pageCount.value <= 6) return 1;
    if (pageCount.value <= 11) return 2;
    return 3;
  });

  // Should show Google Ads (inverse of offcanvas, plus page 1 exclusion)
  const shouldShowAds = computed(() => {
    if (pageCount.value === 1) return false; // Clean first impression
    if (shouldShowOffcanvas.value) return false; // Offcanvas takes precedence
    return true;
  });

  // Dismiss for session
  function dismissOffcanvas() {
    const dismissalKey = "engagement-offcanvas-dismissed";
    sessionStorage.setItem(dismissalKey, Date.now().toString());
    sessionDismissed.value = true;
  }

  return {
    shouldShowOffcanvas,
    engagementLevel,
    shouldShowAds,
    dismissOffcanvas,
    pageCount,
  };
}

// Helper to get page count from useUsageTracking localStorage
function getPageCount(): number {
  try {
    const count = localStorage.getItem("usageCount");
    return count ? parseInt(count, 10) : 0;
  } catch {
    return 0; // localStorage unavailable
  }
}

// Helper to check if dismissed this session
function isSessionDismissed(): boolean {
  try {
    const dismissalKey = "engagement-offcanvas-dismissed";
    const dismissed = sessionStorage.getItem(dismissalKey);
    if (!dismissed) return false;

    const timestamp = parseInt(dismissed, 10);
    if (isNaN(timestamp)) return false;

    // Check if dismissal is still valid (within timeout)
    const now = Date.now();
    const timeout = 3600000; // 1 hour default
    return now - timestamp < timeout;
  } catch {
    return false; // sessionStorage unavailable
  }
}
```

**Testing Strategy:**

- Mock localStorage and sessionStorage
- Test all page count scenarios (1, 2, 3-6, 7, 8-11, 12+)
- Test dismissal behavior
- Test session timeout
- Test disabled config
- Test integration with existing useUsageTracking composable

#### 3.2.2 Component: `EngagementOffcanvas.vue`

**Location:** `m4d/ClientApp/src/components/EngagementOffcanvas.vue`

**Design:** Leverages **bootstrap-vue-next's BOffcanvas** component for consistency with existing UI patterns

**Key Features:**

- **Component**: `BOffcanvas` with `placement="bottom"`
- **Style**: Slide-up from bottom (less intrusive than center overlays)
- **Animation**: Bootstrap's built-in transitions
- **Dismissal**: Click backdrop, click X, or click "Dismiss" button
- **Responsive**: Mobile-friendly, doesn't cover navigation
- **Accessibility**: Bootstrap's built-in ARIA support

**Why BOffcanvas:**

- ✅ Already in our component library (bootstrap-vue-next)
- ✅ Built-in backdrop and animations
- ✅ Accessibility features included
- ✅ Consistent with existing UI patterns
- ✅ Less custom code to maintain
- ✅ Placement="bottom" gives slide-up behavior

**Props:**

```typescript
interface Props {
  show: boolean;
  level: 1 | 2 | 3;
  pageCount: number;
  config: EngagementConfig;
}
```

**Component Structure:**

```vue
<script setup lang="ts">
import { computed } from "vue";
import { EngagementConfig } from "@/models/EngagementConfig";

interface Props {
  show: boolean;
  level: 1 | 2 | 3;
  pageCount: number;
  config: EngagementConfig;
}

const props = defineProps<Props>();
const emit = defineEmits<{
  dismiss: [];
}>();

// Dynamic content based on engagement level
const message = computed(() => {
  if (props.level === 1) {
    return props.config.messages.level1;
  } else if (props.level === 2) {
    return props.config.messages.level2;
  } else {
    // Replace {pageCount} placeholder in level 3 message
    return props.config.messages.level3.replace(
      "{pageCount}",
      props.pageCount.toString(),
    );
  }
});

function handleDismiss() {
  emit("dismiss");
}
</script>

<template>
  <BOffcanvas
    v-model="show"
    placement="bottom"
    backdrop
    scroll="false"
    class="engagement-offcanvas"
    @hidden="handleDismiss"
  >
    <!-- Header with close button -->
    <template #header>
      <div class="d-flex justify-content-end w-100">
        <BButton
          variant="link"
          class="btn-close"
          aria-label="Close"
          @click="handleDismiss"
        />
      </div>
    </template>

    <!-- Dynamic content based on level -->
    <div class="offcanvas-body">
      <div class="content-wrapper">
        <!-- Render HTML message (configured in appsettings.json) -->
        <div v-html="message" />
      </div>

      <!-- Action buttons (footer area) -->
      <div class="cta-buttons mt-4">
        <template v-if="level === 1">
          <BButton
            variant="primary"
            size="lg"
            :href="config.ctaUrls.register"
            class="w-100 mb-2"
          >
            Create Free Account
          </BButton>
          <BButton
            variant="outline-primary"
            :href="config.ctaUrls.features"
            class="w-100 mb-2"
          >
            See Features
          </BButton>
          <BButton variant="link" class="w-100" @click="handleDismiss">
            Maybe Later
          </BButton>
        </template>

        <template v-else-if="level === 2">
          <BButton
            variant="success"
            size="lg"
            :href="config.ctaUrls.subscribe"
            class="w-100 mb-2"
          >
            Subscribe ($5/month)
          </BButton>
          <BButton
            variant="primary"
            :href="config.ctaUrls.register"
            class="w-100 mb-2"
          >
            Create Free Account
          </BButton>
          <BButton
            variant="outline-secondary"
            :href="config.ctaUrls.features"
            class="w-100 mb-2"
          >
            See Premium Features
          </BButton>
          <BButton variant="link" class="w-100" @click="handleDismiss">
            Dismiss
          </BButton>
        </template>

        <template v-else>
          <!-- Level 3: Direct conversion focus -->
          <BButton
            variant="success"
            size="lg"
            :href="config.ctaUrls.subscribe"
            class="w-100 mb-2"
          >
            <strong>Subscribe Now ($5/month)</strong>
          </BButton>
          <BButton
            variant="outline-primary"
            :href="config.ctaUrls.features"
            class="w-100 mb-2"
          >
            See All Premium Features
          </BButton>
          <BButton
            variant="outline-secondary"
            :href="config.ctaUrls.register"
            class="w-100 mb-2"
          >
            Create Free Account
          </BButton>
          <BButton variant="link" class="w-100" @click="handleDismiss">
            Dismiss
          </BButton>
        </template>
      </div>
    </div>
  </BOffcanvas>
</template>

<style scoped lang="scss">
.engagement-offcanvas {
  max-height: 80vh;

  // Center content on larger screens
  @media (min-width: 768px) {
    max-width: 600px;
    margin: 0 auto;
    border-radius: 16px 16px 0 0;
  }
}

.content-wrapper {
  h4 {
    margin-bottom: 1rem;
    color: var(--bs-primary);
  }

  p {
    margin-bottom: 0.75rem;
    line-height: 1.6;
  }

  ul {
    padding-left: 1.5rem;
    margin-bottom: 1rem;

    li {
      margin-bottom: 0.5rem;
    }
  }
}

.cta-buttons {
  border-top: 1px solid var(--bs-border-color);
  padding-top: 1rem;
}
</style>
```

**Bootstrap-Vue-Next Integration:**

- Uses `BOffcanvas` component (https://bootstrap-vue-next.github.io/bootstrap-vue-next/docs/components/offcanvas.html)
- `placement="bottom"` creates slide-up behavior
- Built-in `backdrop` prop for backdrop overlay
- Built-in `@hidden` event for dismissal tracking
- Uses `BButton` for all CTAs (consistent styling)
- Responsive by default (Bootstrap's mobile-first design)

#### 3.2.3 Component: `GoogleAdsController.vue`

**Location:** `m4d/ClientApp/src/components/GoogleAdsController.vue`

**Purpose:** Wrap Google Ads and control visibility based on engagement rules

**Usage:**

```vue
<GoogleAdsController :should-show="shouldShowAds">
  <!-- Google Ads script/elements -->
</GoogleAdsController>
```

### 3.3 Integration Points

#### 3.3.1 MainMenu.vue Changes

**Add engagement offcanvas alongside existing alerts:**

```vue
<script setup>
import { useEngagementOffcanvas } from "@/composables/useEngagementOffcanvas";
import EngagementOffcanvas from "@/components/EngagementOffcanvas.vue";

// Initialize engagement system (only for anonymous users)
const engagement = !props.context.userName
  ? useEngagementOffcanvas({
      enabled: props.context.engagementConfig?.enabled ?? false,
      firstShowPageCount:
        props.context.engagementConfig?.firstShowPageCount ?? 2,
      repeatInterval: props.context.engagementConfig?.repeatInterval ?? 5,
      sessionDismissalTimeout:
        props.context.engagementConfig?.sessionDismissalTimeout ?? 3600000,
      messages: props.context.engagementConfig?.messages,
      ctaUrls: props.context.engagementConfig?.ctaUrls,
    })
  : null;
</script>

<template>
  <!-- Existing alerts (isTest, showMarketing, showExpiration, showReminder) -->

  <!-- New engagement offcanvas (anonymous users only) -->
  <EngagementOffcanvas
    v-if="engagement"
    :show="engagement.shouldShowOffcanvas.value"
    :level="engagement.engagementLevel.value"
    :page-count="engagement.pageCount.value"
    :config="context.engagementConfig"
    @dismiss="engagement.dismissOffcanvas"
  />

  <!-- Google Ads (controlled by engagement) -->
  <GoogleAdsController
    v-if="!context.userName"
    :should-show="engagement?.shouldShowAds.value ?? false"
  >
    <!-- Ad slots -->
  </GoogleAdsController>
</template>
```

#### 3.3.2 Server Configuration (`_head.cshtml`)

**Add engagement configuration to MenuContext:**

```csharp
@{
    var menuContext = new {
        // ... existing properties

        // Only send to anonymous users
        engagementConfig = !Context.User.Identity.IsAuthenticated ? new {
            enabled = Configuration.GetValue("EngagementOffcanvas:Enabled", true),
            firstShowPageCount = Configuration.GetValue("EngagementOffcanvas:FirstShowPageCount", 2),
            repeatInterval = Configuration.GetValue("EngagementOffcanvas:RepeatInterval", 5),
            sessionDismissalTimeout = Configuration.GetValue("EngagementOffcanvas:SessionDismissalTimeout", 3600000),
            messages = new {
                level1 = Configuration["EngagementOffcanvas:Messages:Level1"],
                level2 = Configuration["EngagementOffcanvas:Messages:Level2"],
                level3 = Configuration["EngagementOffcanvas:Messages:Level3"]
            },
            ctaUrls = new {
                register = "/identity/account/register",
                subscribe = "/home/contribute",
                features = "https://music4dance.blog/music4dance-help/subscriptions/"
            }
        } : null
    };
}
```

#### 3.3.3 Configuration (`appsettings.json`)

```json
{
  "EngagementOffcanvas": {
    "Enabled": true,
    "FirstShowPageCount": 2,
    "RepeatInterval": 5,
    "SessionDismissalTimeout": 3600000,
    "Messages": {
      "Level1": "<h4>Exploring music4dance?</h4><p>We're the best resource for matching music to dance styles.</p><p>Create a <strong>free account</strong> to save your searches, build playlists, and unlock more features.</p>",
      "Level2": "<h4>You're finding what you need!</h4><p>We're glad music4dance is helping you discover great dance music. The site is free, but costs real money to run.</p><p><strong>Create an account</strong> to unlock extra features, or <strong>subscribe</strong> to support the service and get premium perks.</p>",
      "Level3": "<h4>You've loaded <strong>{pageCount}</strong> pages on music4dance – clearly it's valuable to you!</h4><p>Help us keep the lights on and the database growing. Premium subscribers get:</p><ul><li>Advanced search filters</li><li>Custom playlists with Spotify integration</li><li>Priority support</li><li>No ads, ever</li></ul><p><strong>Just $5/month</strong> supports real dancers helping real dancers.</p>"
    }
  },
  "FeatureManagement": {
    "EngagementOffcanvas": true
  }
}
```

**Note:** Messages use HTML formatting and are rendered via `v-html` in the component. The `{pageCount}` placeholder in Level 3 is dynamically replaced by the actual page count.

---

## 4. User Experience Considerations

### 4.1 Design Principles

1. **Respect User Intent**: Never block core functionality
2. **Progressive Disclosure**: Start gentle, increase directness with engagement
3. **Easy Dismissal**: Single click to dismiss, no dark patterns
4. **Session Respect**: Once dismissed, don't show again for 1 hour
5. **Mobile-First**: Works beautifully on all screen sizes
6. **Accessibility**: Keyboard navigation, screen reader support

### 4.2 Messaging Framework

**Do's:**

- ✅ Emphasize value received ("You've used m4d X times")
- ✅ Show specific benefits ("Save searches", "Spotify integration")
- ✅ Acknowledge that core service is free
- ✅ Frame subscription as supporting community
- ✅ Use social proof ("Join X dancers who support m4d")

**Don'ts:**

- ❌ Guilt-trip users
- ❌ Block access to content
- ❌ Make dismissal hard to find
- ❌ Show more than once per session (after dismissal)
- ❌ Be vague about pricing

### 4.3 Mobile Experience

**Slide-up Design Benefits:**

- Takes up only bottom portion of screen
- Doesn't obscure navigation
- Natural swiping motion to dismiss
- Thumb-friendly button placement
- Less aggressive than center overlays

### 4.4 Accessibility

**Requirements:**

- ARIA labels on all interactive elements
- Focus trap (Tab cycles through offcanvas)
- Escape key to dismiss
- Screen reader announces offcanvas content
- Color contrast meets WCAG AA standards
- Touch targets minimum 44x44px

---

## 5. Analytics & Measurement

### 5.1 Events to Track

**Offcanvas Interactions:**

```typescript
{
  event: 'engagement_offcanvas_shown',
  page_count: number,
  engagement_level: 1 | 2 | 3,
  user_authenticated: boolean
}

{
  event: 'engagement_offcanvas_dismissed',
  page_count: number,
  engagement_level: 1 | 2 | 3,
  dismiss_method: 'backdrop' | 'button' | 'close_x'
}

{
  event: 'engagement_offcanvas_cta_clicked',
  page_count: number,
  engagement_level: 1 | 2 | 3,
  cta_type: 'register' | 'subscribe' | 'features',
  cta_position: 'primary' | 'secondary' | 'tertiary'
}
```

**Ad Impressions:**

```typescript
{
  event: 'google_ads_shown',
  page_count: number,
  user_authenticated: boolean
}
```

**Conversions:**

```typescript
{
  event: 'user_registered',
  page_count_at_registration: number,
  last_offcanvas_level_seen: 1 | 2 | 3 | null,
  days_since_first_visit: number
}

{
  event: 'subscription_purchased',
  page_count_at_purchase: number,
  user_journey: 'anonymous_direct' | 'anonymous_to_registered' | 'registered_user',
  last_offcanvas_level_seen: 1 | 2 | 3 | null
}
```

### 5.2 Key Metrics Dashboard

**Funnel Metrics:**

1. **Anonymous → Registered Conversion Rate**
   - Baseline: Current rate
   - Target: +30% improvement
   - Track by engagement level last seen

2. **Registered → Subscriber Conversion Rate**
   - Track impact of engagement on later purchases
   - Cohort analysis by first engagement level

3. **Offcanvas Effectiveness**
   - Show rate per level
   - Dismiss rate per level
   - CTA click-through rate per button
   - Time to conversion after engagement

4. **Ad Revenue Impact**
   - Ad impressions before/after
   - Revenue per 1000 visits
   - Compare to subscription revenue

**User Satisfaction Metrics:**

1. **Bounce Rate**: Should not increase >5%
2. **Session Duration**: Should remain stable or increase
3. **Return Visitor Rate**: Should increase
4. **NPS Score**: Monitor for changes

### 5.3 A/B Testing Variants

**Test 1: Timing (Page Counts)**

- Variant A: 2, 7, 12, 17... (baseline)
- Variant B: 3, 8, 13, 18... (later start)
- Variant C: 2, 5, 8, 11... (more frequent)

**Test 2: Messaging Tone**

- Variant A: Community-focused (baseline)
- Variant B: Feature-focused (benefits)
- Variant C: Direct ask (support us)

**Test 3: Offcanvas Design**

- Variant A: Slide-up from bottom (baseline)
- Variant B: Center overlay
- Variant C: Banner at top

**Test 4: Dismissal Timeout**

- Variant A: 1 hour (baseline)
- Variant B: Session-only (resets on new session)
- Variant C: 24 hours

---

## 6. Implementation Phases

### Phase 1: Core Infrastructure (Week 1)

**Tasks:**

1. ✅ Review client-side usage tracking (already done)
2. 🔨 Create `useEngagementOffcanvas` composable
3. 🔨 Add TypeScript interfaces/types (`EngagementConfig`)
4. 🔨 Add configuration to `appsettings.json`
5. 🔨 Update `MenuContext` model
6. 🔨 Update `_head.cshtml` to pass config

**Testing:**

- Unit tests for `useEngagementOffcanvas` logic
- Test all page count scenarios (1, 2, 3-6, 7, 8-11, 12+)
- Test dismissal behavior
- Test configuration loading
- Test integration with existing `useUsageTracking`

**Deliverables:**

- Working composable with tests
- Configuration infrastructure
- Documentation

### Phase 2: UI Components (Week 2)

**Tasks:**

1. 🔨 Create `EngagementOffcanvas.vue` component using BOffcanvas
2. 🔨 Configure BOffcanvas with `placement="bottom"`
3. 🔨 Create level-based content (Level 1, 2, 3)
4. 🔨 Leverage Bootstrap's built-in accessibility
5. 🔨 Create `GoogleAdsController.vue`
6. 🔨 Test responsive behavior (mobile and desktop)

**Testing:**

- Component unit tests
- Visual regression tests
- Accessibility audit (axe-core) - verify Bootstrap's ARIA
- Cross-browser testing (Chrome, Firefox, Safari, Edge)
- Mobile device testing (iOS Safari, Android Chrome)

**Deliverables:**

- Reusable offcanvas component using bootstrap-vue-next
- Bootstrap animations (no custom animations needed)
- Mobile-responsive design
- Accessible markup (Bootstrap provides)

### Phase 3: Integration (Week 3)

**Tasks:**

1. 🔨 Integrate components into `MainMenu.vue`
2. 🔨 Update Google Ads integration
3. 🔨 Add feature flag checks (`FeatureManagement:EngagementOffcanvas`)
4. 🔨 Test with real usage data (page counts)
5. 🔨 Add analytics events tracking
6. 🔨 Create admin dashboard for monitoring

**Testing:**

- End-to-end tests (Playwright/Cypress)
- Test with localStorage at different page counts
- Test dismissal persistence (sessionStorage)
- Test Google Ads hiding/showing logic
- Test configuration changes (hot reload)

**Deliverables:**

- Fully integrated engagement system
- Analytics tracking
- Admin monitoring

### Phase 4: Measurement & Optimization (Week 4+)

**Tasks:**

1. 🔨 Deploy to production with feature flag OFF
2. 🔨 Test with small % of traffic
3. 🔨 Monitor metrics for 1 week
4. 🔨 Analyze conversion data
5. 🔨 Iterate on messaging
6. 🔨 A/B test variants
7. 🔨 Full rollout

**Testing:**

- Load testing (handle scale)
- Monitor error rates
- Track conversion funnel
- User feedback collection

**Deliverables:**

- Production deployment
- Metrics dashboard
- Optimization recommendations
- A/B test results

---

## 7. Risk Mitigation

### 7.1 Potential Risks

| Risk                                                      | Impact | Likelihood | Mitigation                                                       |
| --------------------------------------------------------- | ------ | ---------- | ---------------------------------------------------------------- |
| Users find offcanvas annoying                             | High   | Medium     | Easy dismissal, session tracking, A/B test timing                |
| Bounce rate increases                                     | High   | Low        | Monitor closely, feature flag for instant rollback               |
| Technical errors break site                               | High   | Low        | Comprehensive testing, feature flags, graceful degradation       |
| Ad revenue drops more than subscription revenue increases | Medium | Medium     | Careful timing, monitor both metrics, adjust strategy            |
| localStorage unavailable                                  | Low    | Medium     | Already handled by canonical GUID fallback                       |
| Mobile performance issues                                 | Medium | Low        | Bootstrap optimized, minimal custom code                         |
| BOffcanvas compatibility issues                           | Low    | Low        | Well-tested component, bootstrap-vue-next is actively maintained |

### 7.2 Rollback Plan

**Immediate Rollback (< 5 minutes):**

1. Set `EngagementOffcanvas:Enabled = false` in Azure App Configuration
2. No code deployment needed
3. Monitoring continues to measure impact

**Gradual Rollback:**

1. Increase `FirstShowPageCount` to push back timing
2. Increase `RepeatInterval` to reduce frequency
3. Monitor for improvement

**Complete Removal:**

1. Feature flag OFF
2. Deploy code removal in next release
3. Keep infrastructure for future use

### 7.3 Fallback Strategy

**If engagement modal doesn't improve conversions:**

1. **Plan B**: Focus on improving existing banner for logged-in users
2. **Plan C**: Email campaign to registered non-subscribers
3. **Plan D**: Improve value proposition messaging throughout site
4. **Plan E**: Partner with dance organizations for bulk subscriptions

---

## 8. Success Criteria

### 8.1 Launch Criteria (Go/No-Go)

**Must Have:**

- ✅ All unit tests passing (100% coverage on core logic)
- ✅ Component tests passing
- ✅ Accessibility audit clean (0 critical issues)
- ✅ Cross-browser testing complete (Chrome, Firefox, Safari, Edge)
- ✅ Mobile testing complete (iOS Safari, Android Chrome)
- ✅ Feature flag functional
- ✅ Rollback tested
- ✅ Analytics events firing correctly
- ✅ Performance budget met (< 100ms impact on page load)

**Nice to Have:**

- Documentation complete
- A/B test variants ready
- Admin dashboard functional

### 8.2 Success Metrics (After 30 Days)

**Primary Goals:**

- 📊 Subscription conversion rate: +20% (from X% to Y%)
- 📊 Account registration rate: +30% (from A% to B%)

**Secondary Goals:**

- 📊 Modal CTR: > 5% (industry average for non-intrusive modals)
- 📊 Session duration: Maintained or increased
- 📊 Bounce rate: < 5% increase
- 📊 Return visitor rate: Increased

**Tertiary Goals:**

- 📊 Net revenue: Subscription revenue - lost ad revenue > 0
- 📊 User satisfaction: NPS maintained or improved
- 📊 Mobile conversion: Equal or better than desktop

### 8.3 Learning Goals

**Questions to Answer:**

1. What engagement level (1, 2, 3) has highest conversion rate?
2. What page count timing is optimal?
3. Which CTA has highest click-through rate?
4. Do mobile users convert differently than desktop?
5. Is there a page count threshold where users never convert?
6. How does engagement affect long-term retention?

---

## 9. Future Enhancements

### 9.1 Short-Term (Next Quarter)

1. **Personalized Messaging**
   - Track which dance styles users view most
   - Customize modal: "Looking for Swing music? Premium users get..."
   - Use most-searched dance in CTA

2. **Social Proof**
   - "Join 5,000+ dancers who support music4dance"
   - Show recent subscriber count growth
   - Testimonials from dance instructors

3. **Urgency Elements**
   - Holiday promotion messaging
   - Competition season promotions
   - Limited-time features

4. **Progressive Web App (PWA) Prompt**
   - At visit 4, suggest "Add to Home Screen"
   - Native app-like experience
   - Better retention

### 9.2 Medium-Term (Next 6 Months)

1. **Gamification**
   - "You've searched for 15 dances – collect all 50!"
   - Achievement badges
   - Unlock free month for active contributors

2. **Referral Program**
   - "Share m4d with 3 friends, get 1 month free"
   - Track referral conversions
   - Incentivize word-of-mouth

3. **Email Nurture Campaign**
   - Capture emails at page 2 or 7 (with incentive)
   - Send weekly "best music for X dance" emails
   - Convert via email at page 10+

4. **Partnership Modal**
   - "Are you a dance instructor?" → Special pricing
   - "Part of a dance studio?" → Bulk discounts
   - B2B conversion funnel

### 9.3 Long-Term (Next Year)

1. **Intelligent Timing**
   - Machine learning model predicts optimal show time per user
   - A/B test ML model vs. rule-based

2. **Dynamic Pricing**
   - Show different offers based on engagement patterns
   - Regional pricing
   - Student discounts

3. **Chat Support**
   - Offer to chat with real dancer at visit 5
   - Answer questions about features
   - Convert through personal touch

4. **Integration with Dance Event Platforms**
   - "Going to [CompName]? Subscribe to build your playlist"
   - Partner with competition organizers
   - Event-based conversions

---

## 10. Appendix

### 10.1 Technical Specifications

**Browser Support:**

- Chrome 90+
- Firefox 88+
- Safari 14+
- Edge 90+
- Mobile Safari (iOS 14+)
- Chrome Mobile (Android 8+)

**Performance Budgets:**

- Offcanvas component: Uses bootstrap-vue-next (already loaded)
- Composable JS: < 5KB gzipped
- Time to Interactive impact: < 100ms
- Animation performance: 60fps (Bootstrap handles)

**Dependencies:**

- Vue 3 (existing)
- Bootstrap 5 (existing)
- bootstrap-vue-next (existing) - **BOffcanvas component**
- VueUse (existing)

### 10.2 Glossary

- **Engagement Level**: Progressive stage in user journey (1=Awareness, 2=Consideration, 3=Conversion)
- **Page Count**: Number of page loads tracked in localStorage (from useUsageTracking)
- **Page Load**: A single page request (navigation OR return visit) - both signal engagement
- **CTA**: Call to Action (button or link encouraging user action)
- **Offcanvas**: Slide-in overlay UI component (bottom placement for mobile-friendly UX)
- **Bot Detection**: Client-side logic to exclude crawlers/spiders from tracking
- **Feature Flag**: Configuration toggle to enable/disable features without code deployment
- **MPA (Multi-Page Application)**: Traditional server-rendered pages (vs SPA)

### 10.3 Related Resources

**Internal:**

- [Client-Side Usage Logging Architecture](./client-side-usage-logging.md)
- [Testing Patterns](./testing-patterns.md)
- [Identity Endpoint Protection](./identity-endpoint-protection.md)

**External References:**

- [BOffcanvas Component Docs](https://bootstrap-vue-next.github.io/bootstrap-vue-next/docs/components/offcanvas.html)
- [Modal UX Best Practices](https://www.nngroup.com/articles/modal-nonmodal-dialog/)
- [Progressive Engagement Patterns](https://www.lukew.com/ff/entry.asp?1945)
- [Conversion Rate Optimization](https://www.thinkwithgoogle.com/)

### 10.4 Decision Log

| Date       | Decision                                 | Rationale                                                           | Alternatives Considered                                       |
| ---------- | ---------------------------------------- | ------------------------------------------------------------------- | ------------------------------------------------------------- |
| 2026-03-06 | Use BOffcanvas (bottom) not custom modal | Leverages bootstrap-vue-next, less code, better accessibility       | Custom modal (more code), center modal (too aggressive)       |
| 2026-03-06 | Treat all page loads as engagement       | Simpler than distinguishing navigation vs return, both signal value | Track sessions separately (complex), use referer (unreliable) |
| 2026-03-06 | Session-agnostic messaging               | Works for both navigation and return visits in MPA context          | "Welcome back!" (assumes return), session-specific messaging  |
| 2026-03-06 | Page 2 first show                        | Balance engagement signal with avoiding annoyance                   | Page 1 (too early), Page 3 (too late)                         |
| 2026-03-06 | Every 5 pages repeat                     | Sufficient engagement gap to not annoy                              | Every 3 (too frequent), Every 10 (too rare)                   |
| 2026-03-06 | No ads on page 1                         | Clean first impression most important                               | Show ads immediately (may distract from core value)           |
| 2026-03-06 | Session-based dismissal (1 hour)         | Respect user in current session, re-engage in future                | Permanent dismissal (lose conversion opportunity)             |

---

**Document Version:** 1.0
**Last Updated:** March 6, 2026
**Author:** GitHub Copilot (Claude Sonnet 4.5)
**Reviewed By:** [Pending]
**Status:** 📋 Planning Phase
