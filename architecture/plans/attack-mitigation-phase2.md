# Attack Mitigation: Phase 2 and Beyond

**Type:** Plan
**Status:** Proposed. Not started; Phase 1 shipped 2026-03-05.
**Last verified:** 2026-10-01

Follow-on hardening after the Phase 1 identity-endpoint defenses described in
[bot-and-abuse-defense](../security/bot-and-abuse-defense.md). The aim is to move from in-memory,
manually watched stats to persistent telemetry with alerts, and to add edge and form-level
defenses.

Before starting, re-check two dependencies:
- **Application Insights is not enabled** today. The cheaper log-persistence options are compared
  in [application-log-persistence-plan](../observability/application-log-persistence-plan.md).
- **Azure Front Door is not deployed.** The edge/WAF items overlap the Front Door caching plan
  (`plans/front-door-caching.md`).

## Phase 2: Enhanced Protection

### 2.1 Application Insights Integration

**Goal**: Centralized monitoring, alerting, and anomaly detection

**Features to Implement**:

- Custom telemetry for authentication events
- Failed login metrics and alerts
- Rate limiting metrics
- Attack pattern detection

**Alert Thresholds**:

- Global rate limit hits: 10/hour (warning), 30/hour (critical)
- Failed logins: 50/hour (warning), 100/hour (critical)
- Distinct IPs per username: 5/5min (credential stuffing alert)

**Why Phase 2**: Need to establish baseline patterns first, then implement intelligent alerting

---

### 2.2 Azure Front Door Basic + IP Reputation

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

**Tools**: Azure Application Insights Anomaly Detection (requires Phase 2 AppInsights setup)

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

## Phase 2 Detail: Monitoring and Alerting

**Upgrade Path:**

When Phase 1 baseline data indicates alert thresholds, implement:

**Key Metrics to Track** (via Application Insights):

1. **Rate Limiting Metrics**
   - Per-IP rate limit hits per hour
   - Global rate limit hits per hour
   - CAPTCHA challenge rate
   - Average requests per IP

2. **Authentication Metrics**
   - Failed login attempts per hour
   - Failed logins per username
   - Distinct IPs per username (distributed attack indicator)
   - Account lockout events
   - Successful logins after lockout

3. **Attack Indicators**
   - Suspicious returnUrl detections
   - Honeypot field submissions (Phase 2 feature)
   - Low reputation IP attempts (Phase 2 feature)
   - Suspicious User-Agent strings (Phase 2 feature)

**Email Alert Thresholds** (Recommended - tune after Phase 1 baseline established)

| Metric                         | Warning Email | Critical Email |
| ------------------------------ | ------------- | -------------- |
| Global rate limit hits         | 10/hour       | 30/hour        |
| Failed logins (total)          | 50/hour       | 100/hour       |
| Failed logins (per username)   | 5/5min        | 10/5min        |
| Distinct IPs per username      | 5/5min        | 10/5min        |
| Non-local returnUrl detections | 5/hour        | 20/hour        |
| Account lockouts               | 10/hour       | 25/hour        |

**Email Configuration**:

- Send alerts to: [security team email]
- Include: Top 5 attacking IPs, targeted usernames, time window
- Throttle: Max 1 email per 15 minutes per alert type
- Digest: Hourly summary during active attacks

## Phase 2 Settings

```json
"ApplicationInsights": {
  "ConnectionString": "[YOUR_APP_INSIGHTS_CONNECTION_STRING]",
  "EnableAdaptiveSampling": false
},
"Alerting": {
  "EmailRecipient": "[security-team@example.com]",
  "ThrottleMinutes": 15
},
"IPReputation": {
  "AbuseIPDBApiKey": "[YOUR_ABUSEIPDB_KEY]",
  "CacheDurationHours": 1,
  "MinimumAbuseScore": 75
}
```

## Phase 2 Success Metrics

When Phase 2 is implemented, success will be demonstrated by:

- Security team receives email alerts within 5 minutes of attack detection
- Application Insights provides comprehensive attack telemetry and historical analysis
- Historical pattern analysis enables trend detection and predictive defense
- Alert throttling prevents email fatigue (max 1 email/15min per alert type)
- False positive rate < 5%
- IP reputation service blocks known bad actors proactively
