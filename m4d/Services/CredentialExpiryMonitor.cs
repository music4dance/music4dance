namespace m4d.Services;

/// <summary>
/// Checks every <see cref="IExpiringCredential"/> registered in DI. Runs from the songstats
/// recompute, which fires every few hours; each credential limits its own emails to one a day.
/// </summary>
public class CredentialExpiryMonitor(
    IEnumerable<IExpiringCredential> credentials, ILogger<CredentialExpiryMonitor> logger)
{
    public IReadOnlyList<IExpiringCredential> Credentials { get; } = [.. credentials];

    /// <summary>
    /// Check each credential. One failing check doesn't stop the rest. Returns the problems found.
    /// </summary>
    public async Task<IReadOnlyList<string>> CheckAll(DateTimeOffset? now = null)
    {
        var problems = new List<string>();
        foreach (var credential in Credentials)
        {
            try
            {
                var problem = await credential.Check(now);
                if (problem != null)
                {
                    problems.Add(problem);
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "{Credential} expiry check failed", credential.Name);
            }
        }

        return problems;
    }
}
