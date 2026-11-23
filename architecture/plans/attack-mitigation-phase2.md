# Attack Mitigation: Phase 2 and Beyond

**Type:** Plan
**Status:** Proposed. Not started; Phase 1 shipped 2026-03-05.
**Last verified:** 2026-11-20

Follow-on hardening after the Phase 1 identity-endpoint defenses described in
[bot-and-abuse-defense](../security/bot-and-abuse-defense.md): edge and form-level defenses.

**Monitoring and alerting moved out (2026-11-20).** This plan used to propose Application
Insights telemetry with email alerts on failed logins, lockouts and rate-limit hits. Those
alerts are now part of [site-monitoring-and-alerting](site-monitoring-and-alerting.md)
(section 4, security signals), built on the in-memory trackers the app already has, without
Application Insights.

Before starting, re-check: **Azure Front Door is not deployed**, and Cloudflare (Free plan) is
now the edge ([cloudflare-migration](cloudflare-migration.md)). The edge/WAF items overlap the
Front Door caching plan (`plans/front-door-caching.md`).

## Phase 2: Enhanced Protection

### 2.1 Monitoring and alerting

Moved to [site-monitoring-and-alerting](site-monitoring-and-alerting.md). When the features
below are built (honeypot submissions, low-reputation IPs, suspicious user agents), add their
signals to that plan's detector.

---

### 2.2 Azure Front Door Basic + IP Reputation

**Decided 2026-11-04: Cloudflare (Free plan to start) instead of Front Door.** The steps are in
[plans/cloudflare-migration](cloudflare-migration.md). The Front Door notes below are kept for
reference; the IP reputation options still apply.

**Azure Front Door Basic Benefits**:

- **DDoS Protection**: Basic level included (protects against volumetric attacks)
- **WAF Capability**: Not included in Basic tier (requires Premium)
- **Global Load Balancing**: Improves performance but limited security features
- **Custom Rules**: Some basic rule capability may be available

**Recommended Configuration for AFD Basic**:

1. **Geographic Restrictions (if supported)**:
   - Block or challenge traffic from regions with no legitimate users
   - Requires Premium for full WAF rule capabilities

2. **Rate Limiting at Edge**:
   - May have basic rate limiting features
   - Reduces load on origin servers
   - Check AFD Basic documentation for capabilities

3. **Logging Integration**:
   - Forward AFD logs to Log Analytics or Storage
   - Analyze attack patterns at the edge

**Free/Inexpensive IP Reputation Options**:

1. **AbuseIPDB Free Tier**:
   - 1,000 checks per day
   - Check suspicious IPs against known abuse database
   - API integration: `https://api.abuseipdb.com/api/v2/check`

2. **Azure App Service IP Restrictions**:
   - $0 - built into App Service
   - Manually block known attacking IP ranges
   - Good for permanent blocks of repeat offenders

3. **Cloudflare Free Tier** (if using as CDN):
   - Basic DDoS protection
   - Challenge pages for suspicious traffic
   - Better than AFD Basic for security

**Implementation Plan**:

```csharp
// In RateLimitingMiddleware
private async Task<bool> IsKnownBadActor(string ipAddress)
{
    // Check local cache first (prevents repeated API calls)
    if (_badActorCache.TryGetValue(ipAddress, out var isBad))
    {
        return isBad;
    }

    // Check AbuseIPDB (rate limited to 1000/day)
    try
    {
        var response = await _httpClient.GetAsync(
            $"https://api.abuseipdb.com/api/v2/check?ipAddress={ipAddress}&maxAgeInDays=90");

        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadAsJsonAsync<AbuseIPDBResponse>();
            var isBadActor = data.AbuseConfidenceScore > 75;

            // Cache for 1 hour
            _badActorCache.Set(ipAddress, isBadActor, TimeSpan.FromHours(1));

            return isBadActor;
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to check IP reputation for {IP}", ipAddress);
    }

    return false;
}
```

**Cost Comparison**:

- **AFD Basic**: ~$35/month (CDN + routing, limited security)
- **AFD Premium**: ~$330/month (includes WAF, managed rules)
- **Cloudflare Free/Pro**: $0-20/month (better security than AFD Basic)
- **AbuseIPDB**: Free tier sufficient for low-traffic sites

**Recommendation**:

- If already using AFD: Enable Basic and use App Service IP restrictions
- If starting fresh: Consider Cloudflare for better free security features
- Both: Integrate AbuseIPDB for IP reputation checks

---

### 2.3 Implement Honeypot Fields

**Goal**: Catch automated bots

**Method**:

- Add hidden form fields to login/register forms
- CSS hides from humans, bots fill them in
- Automatic rejection if honeypot is filled

**Example**:

```html
<input
  type="text"
  name="website"
  style="display:none"
  tabindex="-1"
  autocomplete="off"
/>
```

---

### 2.4 User Agent Analysis

**Detection Patterns**:

- Missing or suspicious User-Agent strings
- Known bot signatures
- Outdated browser versions unlikely to be legitimate

**Action**: Apply stricter rate limits or require CAPTCHA

---

## Phase 3: Advanced Protection (Future)

### 3.1 Behavioral Analysis with Machine Learning

**Machine Learning Approach**:

- Profile normal authentication patterns
- Detect anomalies in:
  - Request timing
  - Geographic distribution
  - User-Agent patterns
  - Failed/success ratios

**Tools**: Azure Application Insights Anomaly Detection. Application Insights isn't part of the
alerting plan any more, so this would mean bringing it back, with a daily cap
([log-persistence-options](log-persistence-options.md)).

**Prerequisites**:

- Sufficient baseline data (2-4 weeks)
- Application Insights configured and collecting telemetry
- Budget for AI/ML features

---

### 3.2 Geolocation-Based Rules

**Options**:

- Block countries with no legitimate users
- Apply stricter limits to high-risk regions
- Require CAPTCHA for international traffic

**Caution**: May impact legitimate international users

---

### 3.3 Device Fingerprinting

**Goal**: Track devices across sessions without cookies

**Method**: Browser fingerprinting (canvas, WebGL, fonts, etc.)

**Privacy Consideration**: May conflict with privacy regulations (GDPR, CCPA)

## Phase 2 Settings

```json
"IPReputation": {
  "AbuseIPDBApiKey": "[YOUR_ABUSEIPDB_KEY]",
  "CacheDurationHours": 1,
  "MinimumAbuseScore": 75
}
```

## Phase 2 Success Metrics

When Phase 2 is implemented, success will be demonstrated by:

- False positive rate < 5%
- IP reputation service blocks known bad actors proactively

Alerting success (email within minutes of an attack, throttled per alert type) is measured in
[site-monitoring-and-alerting](site-monitoring-and-alerting.md).
