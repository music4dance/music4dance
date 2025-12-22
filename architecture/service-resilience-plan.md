# Service Resilience Plan for music4dance.net

## Executive Summary

This document outlines a comprehensive plan to make music4dance.net resilient to external service failures. The goal is to ensure that when any external service becomes unavailable (either during startup or runtime), the site degrades gracefully rather than failing completely.

**Key Principle**: Every page should render at minimum the navigation menu and a clear status message when services are unavailable. The site should never completely crash due to external service failures.

## Current External Service Dependencies

Based on code analysis, the site depends on the following external services:

### Critical Services (Required for Core Functionality)

1. **SQL Server Database** (`DanceMusicContextConnection`)

   - Connection string: SQL Server connection
   - Used for: User accounts, song data, playlists, dance information, voting
   - Impact if down: Cannot retrieve or store any data
   - **Resilience Strategy**: Degrade gracefully - many pages don't require database (home, FAQ, static content). Dance pages use JSON file cache as fallback.
   - Current behavior: Application throws exception and fails to start
   - Target behavior: Application starts successfully, serves cached/static content

2. **Azure App Configuration**

   - Endpoint: `https://music4dance.azconfig.io`
   - Used for: Feature flags, configuration refresh, Key Vault integration
   - Impact if down: Cannot load feature flags or dynamic configuration
   - Current behavior: Application may fail to start (depends on mode)

3. **Azure Cognitive Search** (multiple indexes)
   - Endpoint: `https://music4dance.search.windows.net`
   - Indexes: songs-prod-2, songs-prod-3, songs-test-2, songs-test-3, songs-experimental
   - Used for: Song search functionality
   - Impact if down: Search features unavailable
   - Current behavior: Application throws exception and fails to start

### Authentication Services (Optional but Important)

4. **Google OAuth**

   - ClientId/ClientSecret required
   - Used for: User authentication via Google
   - Impact if down: Users cannot log in with Google
   - Current behavior: Login page shows option but fails on click

5. **Facebook OAuth**

   - ClientId/ClientSecret required
   - Used for: User authentication via Facebook
   - Impact if down: Users cannot log in with Facebook
   - Current behavior: Login page shows option but fails on click

6. **Spotify OAuth**
   - ClientId/ClientSecret required
   - Used for: User authentication via Spotify, playlist integration
   - Impact if down: Users cannot log in with Spotify or create playlists
   - Current behavior: Login page shows option but fails on click

### Supporting Services

7. **Azure Communication Services**

   - ConnectionString required
   - Used for: Email sending (confirmation emails, notifications)
   - Impact if down: Users cannot receive confirmation emails
   - Current behavior: Likely throws exception when trying to send email

8. **reCAPTCHA v2**

   - SiteKey/SecretKey required
   - Used for: Bot prevention on registration/forms
   - Impact if down: Forms may be vulnerable to spam, or may block legitimate users
   - Current behavior: Unknown

9. **AutoMapper**
   - License key required
   - Used for: Object mapping between models
   - Impact if down: Application may fail to map objects
   - Current behavior: May throw exception during mapping operations

## Proposed Resilience Architecture

### Phase 1: Service Health Monitoring

#### 1.1 Create Service Health Infrastructure

Create a new service health tracking system:

```
m4d/Services/
  ServiceHealth/
    IServiceHealthCheck.cs           - Interface for health checks
    ServiceHealthStatus.cs           - Status enum and model
    ServiceHealthManager.cs          - Central health tracking
    ServiceHealthMiddleware.cs       - HTTP middleware for health context

    Checks/
      DatabaseHealthCheck.cs         - SQL Server connectivity
      AppConfigHealthCheck.cs        - Azure App Configuration
      SearchServiceHealthCheck.cs    - Azure Cognitive Search
      AuthProviderHealthCheck.cs     - OAuth providers
      EmailServiceHealthCheck.cs     - Azure Communication Services
      ReCaptchaHealthCheck.cs        - reCAPTCHA service
```

#### 1.2 Health Check Features

**ServiceHealthStatus Model**:

```csharp
public enum ServiceStatus
{
    Unknown,        // Not yet checked
    Healthy,        // Service is working
    Degraded,       // Service is slow or partially working
    Unavailable     // Service is down
}

public class ServiceHealthStatus
{
    public string ServiceName { get; set; }
    public ServiceStatus Status { get; set; }
    public DateTime LastChecked { get; set; }
    public DateTime? LastHealthy { get; set; }
    public string ErrorMessage { get; set; }
    public TimeSpan? ResponseTime { get; set; }
    public int ConsecutiveFailures { get; set; }
}
```

**ServiceHealthManager**:

- Singleton service registered at startup
- Maintains in-memory dictionary of service health statuses
- Performs health checks on configurable intervals (default: 1 minute)
- Provides methods to query service health
- Logs all status changes
- Exposes events for status changes

#### 1.3 Startup Health Checks

During application startup (`Program.cs`):

1. Wrap each service registration in try-catch blocks
2. On failure, log error and mark service as unavailable
3. **DO NOT throw exception** - continue startup
4. Register fallback/null implementations for failed services
5. Log comprehensive startup report showing which services are available

Example pattern:

```csharp
try
{
    // Existing service registration
    services.AddDbContext<DanceMusicContext>(...);
    serviceHealth.MarkHealthy("Database");
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to configure database");
    serviceHealth.MarkUnavailable("Database", ex.Message);
    // Register null/fallback implementation
    services.AddSingleton<IDatabaseFallback, NullDatabaseContext>();
}
```

### Phase 2: Graceful Degradation

#### 2.1 Controller/Page Level Checks

Every controller/page that depends on external services should:

1. Check service health before attempting operations
2. Return degraded views when services are unavailable
3. Log attempts to use unavailable services
4. Provide user-friendly error messages

#### 2.2 Fallback Views

Create partial views for common degraded states:

```
m4d/Views/Shared/
  ServiceStatus/
    _ServiceUnavailableNotice.cshtml  - Standard notice banner
    _DatabaseUnavailable.cshtml       - Database-specific message
    _SearchUnavailable.cshtml         - Search-specific message
    _AuthUnavailable.cshtml           - Auth-specific message
```

#### 2.3 Page-Specific Degradation Strategy

| Page/Feature                              | Required Services                   | Degraded Behavior                                                                                                      | Phase |
| ----------------------------------------- | ----------------------------------- | ---------------------------------------------------------------------------------------------------------------------- | ----- |
| Home and most other home controller pages | None (static content)               | Always renders fully - no database required                                                                            | 1     |
| Tempi and Counter pages                   | Database (with JSON cache fallback) | Serve from JSON file cache if database unavailable; show notice if cache also missing                                  | 2     |
| Dance Pages                               | Database (with JSON cache fallback) | Serve from JSON file cache if database unavailable; show notice if cache also missing                                  | 2     |
| Song Search/List                          | Search Service                      | ✅ Phase 2: Backend returns empty results with ViewData flag; Phase 3: Show user-friendly notice                       | 2/3   |
| Song Details                              | Search Service                      | ✅ Phase 2: Backend returns error view; **Phase 3**: Create Vue component with friendly message and navigation options | 2/3   |
| User Login                                | Database + Auth Providers           | ✅ Phase 2: Check OAuth provider health, disable unavailable options                                                   | 2     |
| User Registration                         | Database + Email Service            | Disabled when database unavailable (cannot create accounts)                                                            | 2     |
| Playlist Creation                         | Database + Spotify                  | Disable feature with explanatory message                                                                               | 3     |
| Browse Dances                             | Database                            | Show static dance list from JSON cache if available, otherwise menu only                                               | 2     |
| Admin Pages                               | Database                            | Show status dashboard, disable modification features                                                                   | 3     |

**Phase 2 vs Phase 3 Note**: Phase 2 focuses on backend graceful degradation (no exceptions, proper HTTP status codes, ViewData flags for views). Phase 3 will enhance user experience with Vue components, status banners, and friendly error messages. For example, song details pages currently return a generic error view in Phase 2, but Phase 3 will create a proper Vue error component with clear messaging and navigation options.

**JSON File Cache Strategy**:

- Dance pages already use JSON file caching mechanism
- Files located in web root, generated from database
- When database unavailable, serve from existing JSON files
- Display notice: "Viewing cached data - live features unavailable"
- If JSON cache also missing, show menu with explanatory message

#### 2.4 Frontend Integration

Update Vue.js frontend to handle service unavailability:

1. Add service health endpoint: `/api/health/status`
2. Create Vue composable: `useServiceHealth()`
3. Poll health status periodically (every 30 seconds)
4. Show status banner when services are degraded
5. Disable features that depend on unavailable services
6. Update button states with explanatory tooltips

```typescript
// Example composable
export function useServiceHealth() {
  const health = ref<ServiceHealthStatus[]>([]);
  const isServiceHealthy = (serviceName: string) => {
    return (
      health.value.find((s) => s.serviceName === serviceName)?.status ===
      "Healthy"
    );
  };
  // ... polling logic
  return { health, isServiceHealthy };
}
```

### Phase 3: User Communication

#### 3.1 Status Banner Component

Create a global status banner that:

- Appears at top of all pages when any service is degraded
- Shows specific message about which features are affected
- Includes timestamp of when issue was detected
- Has "Details" link to full status page
- Can be minimized but not dismissed
- Uses appropriate color coding (yellow for degraded, red for unavailable)

#### 3.2 Status Page

Create dedicated status page (`/status`):

- Lists all services and their current status
- Shows last successful check time
- Shows response times and performance metrics
- Includes historical uptime information (last 24 hours)
- Auto-refreshes every 10 seconds
- Accessible even when most services are down

#### 3.3 Error Messages

Standardize error messages:

- **User-friendly**: Avoid technical jargon
- **Actionable**: Tell users what they can/cannot do
- **Transparent**: Be honest about issues
- **Temporary**: Always indicate we're working on it

Examples:

- "We're experiencing issues connecting to our music search service. Browsing and login features are still available."
- "Email delivery is currently delayed. You can still register, but your confirmation email may arrive later."
- "Spotify playlist features are temporarily unavailable. All other site features are working normally."

### Phase 4: Automatic Recovery

#### 4.1 Service Health Monitoring

Background service that:

- Continuously monitors service health
- Attempts to reconnect to failed services
- Uses exponential backoff for retry attempts
- Logs all recovery attempts
- Triggers events when services recover
- Updates ServiceHealthManager state

#### 4.2 Circuit Breaker Pattern

Implement circuit breaker for each external service:

- **Closed**: Service is healthy, requests go through
- **Open**: Service has failed, requests are blocked (return fallback immediately)
- **Half-Open**: Testing if service has recovered

Parameters:

- Failure threshold: 3 consecutive failures opens circuit
- Reset timeout: 30 seconds in open state before testing recovery
- Success threshold: 2 consecutive successes closes circuit

#### 4.3 Caching Strategy

**Primary Cache Source - Dance JSON Files**:

The application already has a robust JSON file caching system for dance pages:

- **Location**: Web root (wwwroot or similar)
- **Content**:
  - Dance categories and metadata
  - Tempo ranges and competition requirements
  - Top 10 songs per dance style (via `DanceStats.TopSongs`)
  - Dance relationships and hierarchies
- **Generation**: Files are generated from database when available
- **Persistence**: JSON files survive application restarts
- **Fallback Behavior**: Serve from existing files when database is unavailable
- **Update Frequency**: Refreshed when database is available and data changes

**Cache Configuration**:

- Default TTL: **30 minutes** (configurable via `ServiceHealth:CacheDuration`)
- Stale-while-revalidate: Serve stale cache when service is unavailable
- Cache invalidation: On service recovery and manual admin trigger
- No additional in-memory caching needed for Phase 1 implementation

**Notes**:

- Home page relies on file-based blog content (markdown files) and hardcoded dance categories - works without database
- No separate "popular songs" cache needed - this data is part of dance page JSON files

### Phase 5: Logging and Monitoring

#### 5.1 Enhanced Logging

Log the following events:

- Service health check results (with response times)
- Service failures (with full exception details)
- Automatic recovery attempts
- Circuit breaker state changes
- Fallback activations (e.g., serving from JSON cache)
- User-facing error pages served
- Admin notification sent events

#### 5.2 Admin Notifications

**Email Alerts on Service Failure**:

- Send email to configured admin addresses on initial service failure
- Use existing Azure Communication Services (same service used for password resets)
- **One email per failure incident** - no repeated notifications
- Reset notification state when service recovers
- Email includes:
  - Service name and failure timestamp
  - Error message/stack trace
  - Current status of all services
  - Link to status dashboard
  - Estimated impact on users

**Notification Configuration**:

```json
{
  "ServiceHealth": {
    "AdminNotifications": {
      "Enabled": true,
      "Recipients": ["admin@music4dance.net"],
      "OnlyOnInitialFailure": true,
      "IncludeStackTrace": true
    }
  }
}
```

**Recovery Notifications**:

- Optional: Send email when service recovers after failure
- Includes: Duration of outage, recovery timestamp, performance metrics

#### 5.3 Startup Report

Generate detailed startup report:

```
=== music4dance.net Startup Report ===
Database: ✓ Healthy (connection verified)
App Configuration: ✓ Healthy (feature flags loaded)
Search Service: ✗ UNAVAILABLE (connection timeout)
  - Fallback: Using cached results only
Google OAuth: ✓ Healthy
Facebook OAuth: ✓ Healthy
Spotify OAuth: ✗ UNAVAILABLE (invalid credentials)
  - Impact: Playlist features disabled
Email Service: ✓ Healthy
reCAPTCHA: ✓ Healthy

Overall Status: DEGRADED (2 services unavailable)
Application started successfully in degraded mode.
Admin notification email sent to: admin@music4dance.net
```

#### 5.4 Metrics

Track and expose metrics:

- Service uptime percentages
- Average response times
- Failure rates
- Recovery times
- Number of requests blocked by circuit breakers
- Number of fallback activations

### Phase 6: Testing Strategy

#### 6.1 Integration Tests

Create tests that simulate service failures:

- Database connection failure
- Azure service timeouts
- OAuth provider failures
- Network connectivity issues

#### 6.2 Resilience Tests

Verify:

- Application starts successfully with each service disabled
- All pages render (even if degraded) with services down
- Error messages are appropriate
- Logging captures all failures
- Automatic recovery works correctly

#### 6.3 Manual Testing Checklist

Before release, verify:

- [ ] Application starts with database unavailable
- [ ] Home page renders fully without database
- [ ] FAQ and static pages render without database
- [ ] Dance pages serve from JSON cache when database unavailable
- [ ] Dance pages show appropriate notice when cache also missing
- [ ] Application starts with App Config unavailable
- [ ] Application starts with Search service unavailable
- [ ] Each OAuth provider can be disabled without breaking login page
- [ ] Email service failure doesn't break registration
- [ ] Admin email sent on first service failure only
- [ ] No duplicate admin emails on continued failure
- [ ] Status page accessible when all services are down
- [ ] Status banner appears appropriately
- [ ] Services automatically recover when restored
- [ ] 30-minute cache TTL works correctly
- [ ] Cache configuration can be changed via appsettings

## Implementation Phases

### Phase 1: Foundation (Week 1) ✅ COMPLETE

- Create ServiceHealthManager and health check infrastructure
- Wrap all service registrations in try-catch blocks
- Add basic logging
- Ensure application starts with any service unavailable

### Phase 2: Backend Degradation ✅ COMPLETE

**Scope**: Server-side health checks, error handling, and graceful degradation in controllers and APIs.

Backend Work:

- ✅ Create ResilientController base class with health check helpers
- ✅ Create HealthController with monitoring endpoints (/api/health, /api/health/status, /api/health/report)
- ✅ Create service unavailability partial views (\_SearchUnavailable.cshtml, etc.)
- ✅ Add health checks to API SearchController (page search)
- ✅ Add health checks to Authentication pages (OAuth provider availability)
- ✅ Add ServiceHealthManager to all controllers (threaded through entire hierarchy)
- ✅ Background logging guards - skip when database unavailable
- ✅ UserMapper resilience - stale cache preservation, Privacy field semantics documented

**Key Deliverables**:

- ✅ Controllers return proper HTTP status codes (503) when services unavailable
- ✅ MVC controllers render views with degraded state notices (using partial views)
- ✅ API controllers return JSON error responses
- ✅ All pages render the navigation menu even when services are down
- ✅ Proper logging of all degradation scenarios

### Phase 3: Frontend User Experience ✅ COMPLETE

**Scope**: Vue.js components to display service status and enhance user experience.

Frontend Work:

- ✅ Create ServiceStatusBanner component in header and PageFrame
- ✅ Add frontend health monitoring (poll /api/health/status endpoint every 30 seconds)
- ✅ UserQuery model with isUnavailable, isAnonymous, displayName getters
- ✅ UserLink component renders <strong>UNAVAILABLE</strong> without link
- ✅ Identity area pages blocked when database unavailable (\_bs5-Layout.cshtml)
- ✅ Anonymous user handling when database down with no cache
- ✅ Polling mechanism to detect service recovery

**Key Deliverables**:

- ✅ User-friendly error messages for all degraded scenarios
- ✅ Visual indicators (banners) showing service status
- ✅ Polling mechanism to detect service recovery
- ✅ Smooth user experience even during outages

### Phase 4: Final Polish & Monitoring ✅ COMPLETE

**Scope**: Complete feature with email notifications and update warning integration.

**Final Tasks Before Completion**:

1. ✅ **Email Notifications on First Service Failure** (Section 5.2)

   - Send email to configured admin addresses on initial service failure
   - Track notification state to prevent duplicate emails
   - Use existing Azure Communication Services (same as password resets)
   - Reset notification tracking when service recovers
   - Implementation: ServiceHealthNotifier class with HTML email templates
   - Configuration: `ServiceHealth:AdminNotifications` section in appsettings

2. ✅ **Integrate Manual Update Warning into Health Polling**
   - Add GlobalState.UpdateMessage to health status API response
   - Update ServiceStatusBanner to display update messages (info style, separate from service warnings)
   - Use same polling mechanism (30 seconds) so banner clears when warning removed
   - Maintain existing UpdateWarning admin endpoint functionality
   - Implementation: HealthController includes updateMessage, ServiceStatusBanner shows both update messages and service warnings

**Key Deliverables**:

- ✅ Email alerts for service failures (one per incident)
- ✅ Update warning banner automatically refreshes with health polling
- ✅ Unified status communication system (service health + manual updates)

## Configuration

Add new configuration section to `appsettings.json`:

```json
{
  "ServiceHealth": {
    "HealthCheckInterval": "00:01:00",
    "EnableBackgroundMonitoring": true,
    "CacheDuration": "00:30:00",
    "AdminNotifications": {
      "Enabled": true,
      "Recipients": ["admin@music4dance.net"],
      "OnlyOnInitialFailure": true,
      "SendRecoveryNotification": false,
      "IncludeStackTrace": true
    },
    "CircuitBreaker": {
      "FailureThreshold": 3,
      "ResetTimeout": "00:00:30",
      "SuccessThreshold": 2
    },
    "Services": {
      "Database": {
        "Critical": false,
        "HealthCheckTimeout": "00:00:05",
        "FallbackStrategy": "JsonCache",
        "JsonCachePath": "wwwroot/cache"
      },
      "AppConfiguration": {
        "Critical": false,
        "HealthCheckTimeout": "00:00:10"
      },
      "SearchService": {
        "Critical": false,
        "HealthCheckTimeout": "00:00:10",
        "FallbackStrategy": "InMemoryCache"
      },
      "GoogleOAuth": {
        "Critical": false,
        "HealthCheckTimeout": "00:00:05"
      },
      "FacebookOAuth": {
        "Critical": false,
        "HealthCheckTimeout": "00:00:05"
      },
      "SpotifyOAuth": {
        "Critical": false,
        "HealthCheckTimeout": "00:00:05"
      },
      "EmailService": {
        "Critical": false,
        "HealthCheckTimeout": "00:00:05",
        "FallbackStrategy": "QueueForLater"
      },
      "ReCaptcha": {
        "Critical": false,
        "HealthCheckTimeout": "00:00:05"
      }
    }
  }
}
```

## Success Criteria

The implementation is successful when:

1. ✅ Application starts successfully even if ALL external services are unavailable (including database)
2. ✅ Home page and static pages (FAQ, Help) render fully without database access
3. ✅ Dance pages serve from JSON file cache when database is unavailable
4. ✅ Users see clear, actionable messages when services are unavailable
5. ✅ Services automatically recover without manual intervention
6. ✅ All failures are logged with appropriate context
7. ✅ Admin receives email notification on initial service failure only
8. ✅ No duplicate admin emails on continued failures
9. ✅ Status page provides real-time visibility into service health
10. ✅ No user-facing exceptions or error pages for service failures
11. ✅ Admin dashboard shows service health metrics
12. ✅ Integration tests verify resilient behavior
13. ✅ 30-minute cache TTL is configurable via appsettings
14. ✅ Documentation explains all degraded operation modes

## Resolved Decisions

The following decisions have been made and incorporated into the plan:

### 1. Database Resilience ✓

**Decision**: Degrade gracefully on database unavailability. Application should start and serve content without database.

**Rationale**: Many pages (home, FAQ, static content) don't require database access. Dance pages can use existing JSON file cache as fallback. This provides maximum availability.

**Implementation**: Mark database as non-critical, serve from JSON cache, show appropriate notices.

### 2. Page Requirements ✓

**Decision**: Home page, FAQ, and other static content pages do NOT require database access.

**Rationale**: These pages contain static content that can be served without database queries. Ensures users can always access basic information even during database outages.

**Implementation**: Separate static content rendering from database-dependent features.

### 3. Dance Page Caching ✓

**Decision**: Leverage existing JSON file caching mechanism for dance pages. Ensure it degrades gracefully if JSON files are also missing.

**Rationale**: System already has this infrastructure. JSON files are persistent across restarts and provide excellent fallback.

**Implementation**:

- Verify/improve existing JSON cache generation
- Serve from cache when database unavailable
- Show notice: "Viewing cached data"
- If cache missing, show menu + explanatory message

### 4. Cache Duration ✓

**Decision**: Start with 30-minute cache TTL, make it configurable.

**Rationale**: Balances freshness with resilience. Short enough to show recent updates, long enough to be useful during outages. Configuration allows tuning based on operational experience.

**Implementation**: `ServiceHealth:CacheDuration` configuration setting (00:30:00 format).

### 5. Admin Notifications ✓

**Decision**: Send email notification to admins on initial service failure only. Use existing Azure Communication Services.

**Rationale**:

- Alerts admins immediately when issues occur
- Prevents notification fatigue with repeated emails
- Leverages existing, proven email infrastructure
- No additional service dependencies

**Implementation**:

- Track notification state per service
- Send email on first failure detection
- Reset notification state on recovery
- Use same Azure Communication Services as password resets

### 6. Feature Flag Integration ✓

**Decision**: Keep service health monitoring separate from the feature flag system for initial implementation.

**Rationale**:

- Clear separation of concerns - feature flags for planned feature control, service health for runtime failures
- Avoids confusion about why flags are disabled (admin action vs. automatic)
- Service health provides runtime status checks that controllers/pages use directly
- Feature flags remain manually controlled for intentional feature toggles
- Simpler implementation and testing
- Can revisit integration in future if operational experience shows benefit

**Implementation**:

- Service health monitoring is independent system
- Controllers check `ServiceHealthManager` directly for runtime decisions
- Feature flags continue to work as designed for manual feature control
- Status dashboard shows service health separately from feature flags

## Open Questions & Decisions Needed

~~1. **Critical Services**: Should we allow the application to start if the database is completely unavailable? Or is database the one service that must be available?~~ **RESOLVED**: Degrade gracefully on database failure.

~~2. **Cache Duration**: What's appropriate cache TTL for different data types when services are unavailable?~~ **RESOLVED**: 30 minutes (configurable).

~~3. **User Notifications**: Should we send notifications (email/push) to admins when services fail?~~ **RESOLVED**: Yes, email on initial failure only.

~~4. **Feature Flag Integration**: Should service health status integrate with the existing feature flag system?~~ **RESOLVED**: Keep separate for now.

## Future Improvements

The following enhancements can be considered after the core resilience features are implemented:

### 1. Advanced Caching

- **Search Results Caching**: Cache common search queries and results for anonymous users
  - Reduces load on search service
  - Provides better experience during search service degradation
  - Requires cache invalidation strategy
- **User Session Data Caching**: In-memory caching of user preferences and session state
  - Reduces database queries for authenticated users
  - Improves performance
  - Requires careful cache invalidation on user actions

### 2. Third-party Service Monitoring

- **Proactive Status Checks**: Check external service status pages before marking services as failed
  - Azure Status API: Check Azure service health
  - Google OAuth Status: https://www.google.com/appsstatus
  - Facebook Platform Status: https://developers.facebook.com/status/
  - Spotify Status: https://status.spotify.com/
- **Benefits**: Distinguish between our issues vs. third-party outages, provide better user messaging
- **Complexity**: Additional HTTP calls, parsing different status formats, reliability of status pages

### 3. Partial Failure Handling

- **Adaptive Circuit Breakers**: Handle scenarios where service is partially available (e.g., 50% success rate)
  - Use sliding window to track success/failure rates
  - Implement half-open state with gradual recovery
  - Adaptive thresholds based on service patterns
- **Request Hedging**: Send duplicate requests to increase reliability for critical operations

### 4. Geographic Redundancy

- **Multi-region Failover**: Automatic failover to different Azure regions when primary region is unavailable
  - Requires: Database replication, geo-distributed search indexes, traffic management
  - Complexity: Data consistency, latency, cost
  - Best for: Scenarios requiring highest availability guarantees

### 5. Static Fallback Generation

- **Static HTML Snapshots**: Pre-generate static HTML versions of critical pages as ultimate fallbacks
  - Generate snapshots of: Home, FAQ, Dance index, Popular dances
  - Serve from CDN when application is completely unavailable
  - Update snapshots daily or on content changes
  - Provides "read-only mode" during complete outages

### 6. Enhanced Status Dashboard

- **Comprehensive Admin Status Page**: Detailed dashboard showing service health metrics
  - Real-time health status with auto-refresh
  - Historical uptime information (last 24 hours)
  - Response times and performance metrics
  - Circuit breaker state visualization
  - Manual recovery/reset controls

### 7. Advanced Circuit Breaker Implementation

- **Per-Service Circuit Breakers**: Implement full circuit breaker pattern for each external service
  - **Closed**: Service healthy, requests go through
  - **Open**: Service failed, requests blocked (return fallback immediately)
  - **Half-Open**: Testing if service has recovered
- **Configuration**: Failure threshold (3), reset timeout (30s), success threshold (2)

### 8. Comprehensive Testing

- **Integration Tests**: Simulate service failures for all external dependencies
- **Resilience Tests**: Verify graceful degradation scenarios
- **Load Testing**: Test system behavior under partial service availability
- **Chaos Engineering**: Randomly inject failures to verify resilience

### 9. Enhanced Monitoring & Metrics

- **Service Uptime Tracking**: Track and expose uptime percentages per service
- **Performance Metrics**: Average response times, failure rates, recovery times
- **Fallback Activation Tracking**: Number of cache hits, circuit breaker activations
- **Azure Monitor Integration**: Export metrics to Azure Monitor for alerting and dashboards

### 10. Song Search Enhancements

- **Vue Error Component for Song Details**: Replace generic error with proper Vue component showing clear message and navigation when search unavailable
- **Song List Banner**: Display "Search temporarily unavailable" notice when ViewData["SearchUnavailable"] is set
- **Graceful Search Degradation**: Show cached recent searches or top songs when search service unavailable

## References

- ASP.NET Core Health Checks: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks
- Circuit Breaker Pattern: https://learn.microsoft.com/en-us/azure/architecture/patterns/circuit-breaker
- Polly Resilience Library: https://github.com/App-vNext/Polly
- Azure Monitor: https://learn.microsoft.com/en-us/azure/azure-monitor/

## Revision History

| Date       | Version | Author         | Changes                              |
| ---------- | ------- | -------------- | ------------------------------------ |
| 2025-12-14 | 1.0     | GitHub Copilot | Initial draft based on code analysis |
