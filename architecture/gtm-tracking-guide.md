# Google Tag Manager Tracking Guide - Engagement System

## Overview

The engagement system includes data attributes on key elements for easy Google Tag Manager tracking via CSS selectors. All tracking focuses on **impressions** (when UI appears) and **user actions** (clicks).

## How GTM Works with This System

1. **Code provides data attributes** → Already done in components
2. **You configure Triggers in GTM** → Use CSS selectors below to detect when elements appear or get clicked
3. **Triggers fire Tags** → Tags send events to GA4 with custom parameters
4. **No frontend code needed** → GTM handles all dataLayer interactions automatically

**What you'll configure:** Triggers (when to fire) + Tags (what to send) using the CSS selectors provided in this guide.

## Quick Start Example

**To track Level 1 anonymous impressions:**

1. **In GTM → Variables:** Create DOM Element variable
   - Name: `DLV - Engagement Level`
   - Type: DOM Element, CSS Selector: `#engagement-offcanvas`, Attribute: `data-engagement-level`

2. **In GTM → Triggers:** Create Element Visibility trigger
   - Name: `Engagement Level 1 Impression`
   - Type: Element Visibility
   - CSS Selector: `#engagement-offcanvas[data-engagement-level="1"][data-engagement-user-type="anonymous"]`
   - Minimum % visible: 50, Fire on: Once per page

3. **In GTM → Tags:** Create GA4 Event tag
   - Name: `GA4 - Engagement Level 1 Impression`
   - Type: Google Analytics: GA4 Event
   - Configuration Tag: (your GA4 config tag)
   - Event Name: `engagement_level1_impression`
   - Event Parameters: `engagement_level` = `1`, `engagement_user_type` = `anonymous`
   - Triggering: Select the trigger from step 2

4. **Preview & Publish:** Test in GTM Preview mode, then publish container

That's it! No code changes needed - GTM will automatically fire when the element appears.

## Data Attributes Reference

### Common Attributes

All engagement elements include:

- `data-engagement-element` - Component type (`"bottom-bar"` or `"offcanvas"`)
- `data-engagement-action` - Event type (`"impression"`, click types)

### Offcanvas-Specific Attributes

The offcanvas overlay includes additional context:

- `data-engagement-level` - Message level shown:
  - `"1"` - Level 1 anonymous (pages 2-6): "Exploring music4dance?"
  - `"2"` - Level 2 anonymous (pages 7-11): "Still searching for music?"
  - `"3"` - Level 3 anonymous (pages 12+): "Finding everything you need?"
  - `"loggedin"` - Logged-in message: "Upgrade to Premium"
- `data-engagement-user-type` - User authentication state:
  - `"anonymous"` - Not logged in
  - `"authenticated"` - Logged in (but not premium)

---

## GTM Triggers Configuration

### 1. Bottom Bar Impression

**GTM Trigger Setup:**

- **Trigger Type:** Element Visibility
- **Selection Method:** CSS Selector
- **CSS Selector:** `[data-engagement-element="bottom-bar"][data-engagement-action="impression"]`
- **Minimum % Visible:** 50
- **Fire On:** Once per page
- **Observe HTML changes:** Checked (recommended)

**GTM Tag Setup:**

- **Tag Type:** GA4 Event
- **Event Name:** `engagement_bottom_bar_impression`
- **Event Parameters:**
  - `engagement_element` = `bottom-bar`
- **Triggering:** Fire on the trigger you created above

**Use Case:** Track how many users see the collapsed bottom bar.

---

### 2. Offcanvas Impression (All Users)

**GTM Trigger Setup:**

- **Trigger Type:** Element Visibility
- **Selection Method:** CSS Selector
- **CSS Selector:** `#engagement-offcanvas[data-engagement-action="impression"]`
- **Minimum % Visible:** 50
- **Fire On:** Once per page
- **Observe HTML changes:** Checked

**GTM Variables Setup** (Create these first):

1. **Variable Name:** `DLV - Engagement Level`
   - **Type:** DOM Element
   - **Selection Method:** CSS Selector
   - **Element Selector:** `#engagement-offcanvas`
   - **Attribute Name:** `data-engagement-level`

2. **Variable Name:** `DLV - Engagement User Type`
   - **Type:** DOM Element
   - **Selection Method:** CSS Selector
   - **Element Selector:** `#engagement-offcanvas`
   - **Attribute Name:** `data-engagement-user-type`

**GTM Tag Setup:**

- **Tag Type:** GA4 Event
- **Event Name:** `engagement_offcanvas_impression`
- **Event Parameters:**
  - `engagement_element` = `offcanvas`
  - `engagement_level` = `{{DLV - Engagement Level}}` (use variable)
  - `engagement_user_type` = `{{DLV - Engagement User Type}}` (use variable)
- **Triggering:** Fire on the trigger you created above

**Use Case:** Track impressions with level and user type captured automatically from data attributes.

---

### 3. Offcanvas Impression by Level (Segmented)

**Alternative approach:** For separate tracking of each message type, create 4 triggers with specific selectors:

#### Level 1 Anonymous Impression

**GTM Trigger:**

- **Type:** Element Visibility
- **CSS Selector:** `#engagement-offcanvas[data-engagement-level="1"][data-engagement-user-type="anonymous"]`
- **Minimum % Visible:** 50
- **Fire On:** Once per page

**GTM Tag:**

- **Event Name:** `engagement_level1_impression`
- **Event Parameters:** `engagement_level` = `1`, `engagement_user_type` = `anonymous`

#### Level 2 Anonymous Impression

**GTM Trigger:**

- **CSS Selector:** `#engagement-offcanvas[data-engagement-level="2"][data-engagement-user-type="anonymous"]`

**GTM Tag:**

- **Event Name:** `engagement_level2_impression`
- **Event Parameters:** `engagement_level` = `2`, `engagement_user_type` = `anonymous`

#### Level 3 Anonymous Impression

**GTM Trigger:**

- **CSS Selector:** `#engagement-offcanvas[data-engagement-level="3"][data-engagement-user-type="anonymous"]`

**GTM Tag:**

- **Event Name:** `engagement_level3_impression`
- **Event Parameters:** `engagement_level` = `3`, `engagement_user_type` = `anonymous`

#### Logged-In Upgrade Impression

**GTM Trigger:**

- **CSS Selector:** `#engagement-offcanvas[data-engagement-level="loggedin"][data-engagement-user-type="authenticated"]`

**GTM Tag:**

- **Event Name:** `engagement_loggedin_impression`
- **Event Parameters:** `engagement_level` = `loggedin`, `engagement_user_type` = `authenticated`

**Note:** This approach gives you separate events per level (easier reporting), vs Trigger #2 which uses variables to capture level dynamically (one event with parameter).

---

## Click Tracking

### 4. Sign Up Button Click (Anonymous Users)

**GTM Trigger Setup:**

- **Trigger Type:** Click - All Elements
- **This trigger fires on:** Some Clicks
- **Click Element Matches CSS Selector:** `[data-engagement-action="signup-click"]`

**GTM Tag Setup:**

- **Tag Type:** GA4 Event
- **Event Name:** `engagement_cta_click`
- **Event Parameters:**
  - `engagement_action` = `signup`
  - `engagement_user_type` = `anonymous`
  - (Optional) `link_url` = `{{Click URL}}` (built-in GTM variable)
- **Triggering:** Fire on the click trigger you created above

---

### 5. Sign In Button Click (Anonymous Users)

**GTM Trigger:**

- **Type:** Click - All Elements
- **CSS Selector:** `[data-engagement-action="signin-click"]`

**GTM Tag:**

- **Event Name:** `engagement_cta_click`
- **Event Parameters:** `engagement_action` = `signin`, `engagement_user_type` = `anonymous`

---

### 6. Subscribe Now Button Click (Logged-In Users)

**GTM Trigger:**

- **Type:** Click - All Elements
- **CSS Selector:** `[data-engagement-action="subscribe-click"]`

**GTM Tag:**

- **Event Name:** `engagement_cta_click`
- **Event Parameters:** `engagement_action` = `subscribe`, `engagement_user_type` = `authenticated`

---

### 7. Learn More Button Click (Logged-In Users)

**GTM Trigger:**

- **Type:** Click - All Elements
- **CSS Selector:** `[data-engagement-action="learnmore-click"]`

**GTM Tag:**

- **Event Name:** `engagement_cta_click`
- **Event Parameters:** `engagement_action` = `learnmore`, `engagement_user_type` = `authenticated`

---

### 8. Maybe Later / Dismiss Button Click

**GTM Trigger:**

- **Type:** Click - All Elements
- **CSS Selector:** `[data-engagement-action="dismiss-click"]`

**GTM Tag:**

- **Event Name:** `engagement_cta_click`
- **Event Parameters:**
  - `engagement_action` = `dismiss`
  - `engagement_level` = `{{DLV - Engagement Level}}` (use variable from Trigger #2)
  - `engagement_user_type` = `{{DLV - Engagement User Type}}` (use variable)

**Use Case:** Track dismissal rate by level (which messages are users rejecting?).

---

### 9. Header Collapse Click

**GTM Trigger:**

- **Type:** Click - All Elements
- **CSS Selector:** `[data-engagement-action="collapse-click"]`

**GTM Tag:**

- **Event Name:** `engagement_collapse`
- **Event Parameters:**
  - `engagement_action` = `collapse`
  - `engagement_level` = `{{DLV - Engagement Level}}`
  - `engagement_user_type` = `{{DLV - Engagement User Type}}`

**Use Case:** Track users collapsing via down arrow (less intentional than "Maybe Later").

---

## Recommended GTM Setup Priority

### Essential (Week 1)

1. **Offcanvas Impression by Level** (4 triggers) - Track which messages users see
2. **Sign Up Click** - Primary conversion for anonymous users
3. **Subscribe Click** - Primary conversion for logged-in users
4. **Maybe Later Click** - Track rejection rate

### Important (Week 2)

5. **Bottom Bar Impression** - Baseline visibility metric
6. **Sign In Click** - Returning user engagement
7. **Learn More Click** - Interest without immediate conversion
8. **Header Collapse** - Alternative dismissal method

---

## GA4 Event Reference

When you configure Tags in GTM (as shown above), they will automatically send events to GA4. Here's what those events will look like in GA4 for reference when building reports:

### Impression Events

**Event Names you'll see in GA4:**

- `engagement_bottom_bar_impression`
- `engagement_offcanvas_impression` (or level-specific: `engagement_level1_impression`, etc.)

**Parameters you'll see:**

- `engagement_type` - `"bottom-bar"` or `"offcanvas"`
- `engagement_level` - `"1"`, `"2"`, `"3"`, or `"loggedin"`
- `engagement_user_type` - `"anonymous"` or `"authenticated"`

### CTA Click Events

**Event Name in GA4:** `engagement_cta_click`

**Parameters you'll see:**

- `engagement_action` - `"signup"`, `"signin"`, `"subscribe"`, `"learnmore"`, `"dismiss"`
- `engagement_level` - Message level when clicked (for dismiss button)
- `engagement_user_type` - `"anonymous"` or `"authenticated"`
- `link_url` - Button destination (if you configure GTM to capture `{{Click URL}}`)

### Collapse Events

**Event Name in GA4:** `engagement_collapse`

**Parameters you'll see:**

- `engagement_action` - `"collapse"`
- `engagement_level` - Message level when collapsed
- `engagement_user_type` - `"anonymous"` or `"authenticated"`

---

## Conversion Funnel Tracking

To measure full funnel, set up these GA4 events in sequence:

1. **Awareness:** `engagement_offcanvas_impression` (any level)
2. **Interest:** Click on any CTA (not "Maybe Later")
3. **Action:** Account registration or subscription completion

**Example GA4 Exploration (Funnel Exploration):**

```
Step 1: Offcanvas Impression
  Event: engagement_impression
  Filter: engagement_type = "offcanvas"

Step 2: CTA Click
  Event: engagement_cta_click
  Filter: engagement_action != "dismiss"

Step 3: Conversion
  Event: sign_up OR purchase (existing GA4 events)
```

**Segment by:**

- `engagement_level` - Which message drives most conversions?
- `user_type` - Anonymous signup rate vs logged-in upgrade rate

---

## Testing GTM Configuration

### 1. Enable Preview Mode

1. Open GTM workspace
2. Click "Preview" button
3. Enter music4dance.net URL
4. Wait for Tag Assistant to connect

### 2. Test Impression Tracking

1. Navigate to page 2 (or clear localStorage and reload)
2. Wait for bottom bar to appear
3. Verify `engagement_bottom_bar_impression` fires in Tag Assistant
4. Let offcanvas auto-expand
5. Verify `engagement_level1_impression` fires (or appropriate level)

### 3. Test Click Tracking

1. With offcanvas open, click "Sign Up Free"
2. Verify `engagement_cta_click` with `engagement_action = "signup"` fires
3. Go back, click "Maybe Later"
4. Verify `engagement_cta_click` with `engagement_action = "dismiss"` fires

### 4. Test Level Progression

1. Clear localStorage to reset page count
2. Navigate through pages 2, 7, 12 (trigger pages)
3. Verify different level impression events fire:
   - Page 2 → `engagement_level1_impression`
   - Page 7 → `engagement_level2_impression`
   - Page 12 → `engagement_level3_impression`

---

## Data Analysis Examples

### Key Metrics to Calculate

**Impression Rate:**

```
(Offcanvas Impressions / Page Views) × 100
```

**CTA Click-Through Rate (CTR):**

```
(CTA Clicks / Offcanvas Impressions) × 100
```

**Conversion Rate:**

```
(Sign Ups or Subscriptions / Offcanvas Impressions) × 100
```

**Dismissal Rate:**

```
("Maybe Later" Clicks / Offcanvas Impressions) × 100
```

**Conversion by Level:**

```
Level 1 Conversions / Level 1 Impressions
Level 2 Conversions / Level 2 Impressions
Level 3 Conversions / Level 3 Impressions
Logged-In Conversions / Logged-In Impressions
```

### Questions to Answer

1. **Which message converts best?**
   - Compare conversion rates by engagement level
   - Is Level 3 (high insistence) more or less effective?

2. **Do users dismiss at certain levels?**
   - Track dismissal rate by level
   - High dismissal at Level 1 might mean messaging is too early

3. **Is bottom bar effective?**
   - Compare bottom bar visibility to voluntary offcanvas expansions
   - Are users clicking bottom bar or only seeing auto-expand?

4. **Anonymous vs Logged-In Performance:**
   - Compare signup rate (anonymous) to upgrade rate (logged-in)
   - Which audience is more receptive to engagement?

---

## Troubleshooting

### Impressions Not Firing

**Issue:** No impression events in Tag Assistant

**Check:**

1. Element Visibility trigger set to "Once per page" (not "Once per element")
2. Minimum visibility % is 50% or lower
3. Fire On: "DOM Ready" or "Window Loaded"
4. GTM container is published (not just saved)

### Clicks Not Capturing Level

**Issue:** Click events missing `engagement_level` parameter

**Check:**

1. DOM Element variables are created (`DLV - data-engagement-level`)
2. Variable selector matches `#engagement-offcanvas` exactly
3. Click trigger fires AFTER offcanvas is visible (timing issue)
4. Try using "Click Element" variable instead of separate DOM Element variable

### Multiple Impressions Firing

**Issue:** Same impression event fires multiple times on one page

**Check:**

1. Trigger "Limit" set to "Once per page" (not "No limit")
2. Single Page Application (SPA) mode NOT enabled (music4dance is MPA)
3. No duplicate tags with same trigger

---

## Appendix: All CSS Selectors Quick Reference

```css
/* Impressions */
[data-engagement-element="bottom-bar"][data-engagement-action="impression"]
#engagement-offcanvas[data-engagement-action="impression"]
#engagement-offcanvas[data-engagement-level="1"][data-engagement-user-type="anonymous"]
#engagement-offcanvas[data-engagement-level="2"][data-engagement-user-type="anonymous"]
#engagement-offcanvas[data-engagement-level="3"][data-engagement-user-type="anonymous"]
#engagement-offcanvas[data-engagement-level="loggedin"][data-engagement-user-type="authenticated"]

/* Click Actions */
[data-engagement-action="signup-click"]
[data-engagement-action="signin-click"]
[data-engagement-action="subscribe-click"]
[data-engagement-action="learnmore-click"]
[data-engagement-action="dismiss-click"]
[data-engagement-action="collapse-click"]
```

---

**Document Version:** 1.0
**Last Updated:** March 11, 2026
**Related:** `architecture/visitor-engagement-monetization.md`
