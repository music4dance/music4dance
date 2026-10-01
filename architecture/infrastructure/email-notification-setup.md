# Email Notification Configuration Guide

## What You Need to Configure

To receive email notifications when services fail, you need to configure the Azure Communication Services connection string in your configuration.

## Option 1: Local Testing with User Secrets (Recommended for Development)

```bash
# In the m4d project directory
dotnet user-secrets set "Authentication:AzureCommunicationServices:ConnectionString" "endpoint=https://your-resource.communication.azure.com/;accesskey=YOUR_KEY"
dotnet user-secrets set "ServiceHealth:AdminNotifications:Enabled" "true"
dotnet user-secrets set "ServiceHealth:AdminNotifications:Recipients:0" "your-email@example.com"
```

## Option 2: Azure App Configuration (Production)

Add these keys to your Azure App Configuration:

- Key: `Authentication:AzureCommunicationServices:ConnectionString`

  - Value: `endpoint=https://your-resource.communication.azure.com/;accesskey=YOUR_KEY`

- Key: `ServiceHealth:AdminNotifications:Enabled`

  - Value: `true`

- Key: `ServiceHealth:AdminNotifications:Recipients:0`
  - Value: `admin@music4dance.net`

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

The appsettings.json already has placeholder configuration:

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

Replace `YOUR_ACCESS_KEY_HERE` with your actual connection string, or use one of the secure methods above.

## Testing Email Notifications

1. Configure the connection string using one of the methods above
2. Update the recipient email address to your email
3. Start the application
4. Break a service (like you did with the database)
5. Check your email inbox (and spam folder) for the notification

The email will include:

- Service name that failed
- Timestamp
- Error message
- Full status of all services
- Impact assessment

## Troubleshooting

**No email received?**

Check the console output for:

- `Service health email notifications enabled for 1 recipients` (means config loaded correctly)
- `Service failure notification sent to [email] for [ServiceName]` (means email was sent)
- `WARNING: Service health notifications enabled but no email connection string configured` (means connection string missing)
- `Failed to send service failure notification` (means email sending failed - check connection string)

**Want to test without breaking services?**

You can temporarily modify `ServiceHealthManager.cs` to mark a service as unavailable:

```csharp
serviceHealth.MarkUnavailable("TestService", "This is a test failure");
```

Then restart the application.

## Security Note

**DO NOT commit the actual connection string to source control!**

Use one of the secure configuration methods:

- User Secrets (development)
- Azure App Configuration (production)
- Environment Variables (Azure Web App)
- Azure Key Vault (highest security)

The placeholder in appsettings.json is safe to commit as it contains a fake key.
