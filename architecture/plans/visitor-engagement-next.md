# Visitor Engagement: Next Steps

**Type:** Plan
**Status:** Proposed. These were the post-launch priorities and ideas as of March 2026; they
haven't been re-checked against usage data.
**Last verified:** 2026-10-01

Follow-ons to the engagement system described in
[visitor-engagement-monetization](../pages/visitor-engagement-monetization.md).

## Immediate Priorities (Post-Launch)

**1. Configure Google Tag Manager Tracking (Week 1)**

Done: the code is instrumented. Follow [runbooks/gtm-ga4-setup](../runbooks/gtm-ga4-setup.md) to configure GTM:

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

## Medium-Term Enhancements

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

## Long-Term Vision

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

## Research & Investigation

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
