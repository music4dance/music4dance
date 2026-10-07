# Configure Service-Failure Email Alerts

**Type:** Runbook
**Status:** Current
**Last verified:** 2026-10-07

## When to use

Turning on, or checking, the admin emails that `ServiceHealthNotifier` sends: a status report
each time an instance starts in Azure, and failure and recovery emails when a dependency goes
down or comes back. For how they behave (one failure email per incident, a 30-minute cooldown per
service, recovery emails), see
[service-resilience § Admin notifications](../infrastructure/service-resilience.md#admin-notifications).

## What You Need to Configure

Two things: the Azure Communication Services connection string (the same one used for account
email), and the `ServiceHealth:AdminNotifications` settings. The settings are `Enabled`,
`Recipients`, `IncludeStackTrace`, `SenderAddress` (default `donotreply@music4dance.net`) and
`StartupStatus`. `Enabled` and `StartupStatus` default to `false`. `StartupStatus` turns on the
status email sent each time the app starts; set it per environment.

The settings are read each time an email is sent. In Azure, though, the running app only reloads
App Configuration when `Configuration:Sentinel` changes, so after adding or changing these keys,
bump the sentinel or restart the app. The startup status email is sent only at startup, so test
it with a restart.

## Option 1: Local Testing with User Secrets (Recommended for Development)

```bash
# In the m4d project directory
dotnet user-secrets set "Authentication:AzureCommunicationServices:ConnectionString" "endpoint=https://your-resource.communication.azure.com/;accesskey=YOUR_KEY"
dotnet user-secrets set "ServiceHealth:AdminNotifications:Enabled" "true"
dotnet user-secrets set "ServiceHealth:AdminNotifications:Recipients:0" "your-email@example.com"
dotnet user-secrets set "ServiceHealth:AdminNotifications:StartupStatus" "true"
```

## Option 2: Azure App Configuration (Production)

Add these keys to your Azure App Configuration:

- Key: `Authentication:AzureCommunicationServices:ConnectionString`

  - Value: `endpoint=https://your-resource.communication.azure.com/;accesskey=YOUR_KEY`

- Key: `ServiceHealth:AdminNotifications:Enabled`

  - Value: `true`

- Key: `ServiceHealth:AdminNotifications:Recipients:0`
  - Value: `admin@music4dance.net`

- Key: `ServiceHealth:AdminNotifications:StartupStatus`
  - Value: `true`

Leave the label empty to apply a key to every environment, or label it with the environment name
(`Production`, `Staging`) to apply it to one.

## Option 3: Environment Variables (Azure Web App)

In your Azure Web App Configuration:

```
Authentication__AzureCommunicationServices__ConnectionString = endpoint=https://...
ServiceHealth__AdminNotifications__Enabled = true
ServiceHealth__AdminNotifications__Recipients__0 = admin@music4dance.net
```

(Note: Double underscore `__` is used instead of colon `:` in environment variables)

## Getting the Azure Communication Services Connection String

1. Go to Azure Portal
2. Navigate to your Azure Communication Services resource
3. Click "Keys" in the left menu
4. Copy the "Primary connection string" or "Secondary connection string"

The connection string format is:

```
endpoint=https://your-resource-name.communication.azure.com/;accesskey=abcd1234...
```

## Configuration Structure

`appsettings.json` does **not** include an `AdminNotifications` section, so notifications are off
unless one of the sources above sets them. The full shape is:

```json
{
  "ServiceHealth": {
    "AdminNotifications": {
      "Enabled": true,
      "Recipients": ["admin@music4dance.net"],
      "IncludeStackTrace": false
    }
  },
  "Authentication": {
    "AzureCommunicationServices": {
      "ConnectionString": "endpoint=https://your-acs-resource.communication.azure.com/;accesskey=YOUR_ACCESS_KEY_HERE"
    }
  }
}
```

Never put a real access key in `appsettings.json`. Use one of the methods above.

## Testing Email Notifications

1. Configure the connection string and settings using user secrets (Option 1), with your own
   address as the recipient.
2. Break a dependency **before** starting. For example, point
   `ConnectionStrings:DanceMusicContextConnection` at a server that doesn't exist. The migration
   fails and `Database` is marked unavailable.
3. Start the application. The console should show the migration failure.
4. Check your inbox (and spam folder) for the "Service Failure: Database" email.
5. Restore the setting. With `StartupStatus` set, every start also sends a "Status: Started
   healthy" (or "Started degraded") email, which is the quickest end-to-end check.

The email will include:

- Service name that failed
- Timestamp
- Error message
- Full status of all services
- Impact assessment

## Troubleshooting

**No email received?**

Check the console output for:

- `[Notifications] Enabled=..., Recipients=..., StartupStatus=..., EmailService=...`, printed
  once the app has started. It shows the settings as the notifier sees them: `Enabled=False` or
  an empty `Recipients` means the settings didn't load, and `EmailService=missing` means the
  connection string didn't.
- `Service health ... email not sent (...): notifications are disabled or have no recipients` (means the `ServiceHealth:AdminNotifications` settings didn't load)
- `Service health ... email sent to [email]: [subject]` (means email was sent)
- `Failure email for '[ServiceName]' suppressed` (a failure email for that service went out in the last 30 minutes)
- `✗ EmailService: Unavailable` in the startup report, and `Email service is unavailable - message to ... was not sent` warnings (means the connection string is missing, so the no-op `NullEmailSender` is in use)
- `Failed to send service health ... email` (means email sending failed - check connection string)

**Want to test without breaking a real dependency?** Temporarily add
`serviceHealth.MarkUnavailable("TestService", "This is a test failure");` right after the
`ServiceHealthManager` is created in `M4dApplicationExtensions`, run once, then revert it.

## Security Note

**DO NOT commit the actual connection string to source control!**

Use one of the secure configuration methods:

- User Secrets (development)
- Azure App Configuration (production)
- Environment Variables (Azure Web App)
- Azure Key Vault (highest security)

The placeholder in appsettings.json is safe to commit as it contains a fake key.
