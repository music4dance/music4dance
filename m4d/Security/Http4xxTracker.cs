namespace m4d.Security;

/// <summary>
/// Tracks 4xx HTTP responses (mostly 400/404) by URL for admin visibility into broken
/// links, scraper probing, and malicious request patterns, without needing per-request
/// URL logging in the default ASP.NET Core log output.
/// Uses a circular buffer to store the last 10,000 events.
/// </summary>
public class Http4xxTracker
{
    private readonly CircularBuffer<Http4xxEvent> _events = new(10000);
    private readonly object _lock = new();

    // Path prefixes for well-known scanner/exploit probes that show up constantly in the
    // 4xx log and aren't actionable bugs in our own code (WordPress/Joomla probing, secret
    // scanning, etc). Matched against the start of the path only (query string stripped).
    private static readonly string[] KnownAttackPathPrefixes =
    [
        "/wp",             // /wp-admin, /wp-login.php, /wp/, and bare /wp - never a real path here
        "/wordpress",
        "/administrator",
        "/sitecore/",
        "/solr/",
        "/owa/",
        "/_profiler/",     // Symfony debug toolbar
        "/engine-ui/",     // Spark/Flink UI probe, usually with an embedded host:port
        "/storage/logs/",  // Laravel log disclosure
        "/userfiles",      // file-manager path traversal (?path=../../../.env)
    ];

    // Substrings for the same category of probe, but ones scanners prefix with a guessed
    // app/framework directory (e.g. "/admin/.env", "/laravel/.env"), so a StartsWith check
    // against the bare pattern would miss most hits. Matched anywhere in the path.
    //
    // Everything here names a file or route that this app has never served and never will.
    // Deliberately *not* here: short generic segments a future feature could plausibly use
    // (/login, /dashboard, /account, /mcp, /sse, /manifest.json), and benign platform probes
    // (/sitemap.xml, /.well-known/*, /apple-touch-icon*.png) that are worth keeping visible.
    // See architecture/distributed-attack-mitigation.md's triage log.
    private static readonly string[] KnownAttackPathSubstrings =
    [
        // Language/runtime fingerprinting
        ".php",
        "phpinfo",
        ".jsp",
        ".shtml",
        "/cgi-bin",
        "/server/php/",        // blueimp jQuery-File-Upload RCE, probed under many prefixes
        "jquery-file-upload",
        "/proc/self/",

        // Dotfile / VCS / editor-config disclosure
        ".env",                // /.env, /admin/.env, /secrets.env, aws_credentials.env
        "/.git",
        "/.svn",
        "/.hg/",
        "/.ssh/",
        "/.aws/",
        "/.docker",            // /.docker/config.json, /.dockercfg, /.dockerenv
        "/.vscode/",
        "/.idea/",
        "/.claude/",
        "/.config/",
        "/.composer",
        "/.circleci/",
        "/.github/",
        "/.gitlab-ci",
        "/.travis",
        "/.npmrc",
        "/.yarnrc",
        "/.s3cfg",
        "/.boto",
        "/.htpasswd",
        "/.htaccess",
        "/.netrc",
        "/.bash",              // /.bashrc, /.bash_profile, /.bash_history
        "/.zsh",
        "/.profile",

        // Key material
        "id_rsa",
        "id_dsa",
        "id_ecdsa",
        "id_ed25519",
        "authorized_keys",
        "known_hosts",
        ".pem",
        ".key",                // server.key, privatekey.key, localhost.key
        "private-key",

        // Cloud / CI / app secrets
        "credentials",         // credentials.json/.yml/.db, aws_credentials.env, .git-credentials
        "secrets.",            // secrets.env/.yml/.json
        "serviceaccount",      // kubernetes.io/serviceaccount, serviceAccountKey.json
        "service-account",
        "service_account",
        "firebase",
        "gcp-key",
        "gcp-sa",
        "google-key",
        "/sa.json",
        "/key.json",
        "keyfile.json",
        "auth.json",
        "composer.json",
        "composer.lock",
        "appsettings",         // appsettings.json, appsettings.Production.json
        "local.settings.json",
        "application.propert",
        "application.y",       // application.yml / .yaml
        "application-properties",  // Atlassian /rest/api/1.0/application-properties
        "terraform.tfstate",
        ".tfvars",
        "serverless.y",
        "/values.yaml",
        "rclone.conf",
        "sftp-config.json",
        "/_environment",
        "/config/env",
        "runtime-env.js",
        "environment.rb",

        // Container / orchestration manifests
        "docker-compose",
        "/compose.y",
        "dockerfile",

        // Database dumps and backup archives
        ".sql",                // dump.sql, db.sql.gz, database.sql
        "/db.",                // /db.zip, /db.tar.gz
        "backup.zip",
        "laravel.log",

        // CMS / appliance exploit paths
        "editor/filemanager/browser/default/browser.html", // FCKeditor/CKEditor file-manager exploit probe
        "/sites/default/files",     // Drupal
        "media/system/js/core.js",  // Joomla fingerprint
        "/modules/mod_",            // Joomla modules, incl. mod_webshell
        "webshell",
        "/blocks/rce/",             // Moodle RCE block
        "/plugins/",
        "/admin/controller/extension/", // OpenCart
        "sugar_version.json",       // SugarCRM
        "telerik.web.ui",           // Telerik RadAsyncUpload deserialization
        "showlogin.cc",             // Sangfor appliance
        "/license.txt",             // WordPress/Joomla version fingerprint
        "/.tmb/",                   // QNAP thumbnail traversal
        "404error_test.html",

        // Build-tool / bundler metadata disclosure
        "__vite_rsc_findsourcemapurl",
        "/.vite/",
        "asset-manifest.json",
        "webpack-stats.json",

        // Exposed dev/AI-agent backdoors (a single sweep on 2026-09-17; see triage log)
        "-debug-trigger",
        "/api/fs/exec",
        "inngest",
        "/api/designer/",
        "/api/templates/preview",
        "/read-document",

        // Miscellaneous
        "/graphql",
        "/ipfs/",
        "/cdn-cgi/",
    ];

    public static bool IsKnownAttackUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        var path = url.Split('?', 2)[0];

        return KnownAttackPathPrefixes.Any(
                prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            || KnownAttackPathSubstrings.Any(
                substring => path.Contains(substring, StringComparison.OrdinalIgnoreCase));
    }

    public void RecordEvent(string url, int statusCode)
    {
        var evt = new Http4xxEvent
        {
            Timestamp = DateTime.UtcNow,
            Url = string.IsNullOrEmpty(url) ? "/" : url,
            StatusCode = statusCode
        };

        lock (_lock)
        {
            _events.Add(evt);
        }
    }

    /// <summary>
    /// Aggregates tracked events by URL+status. Pass <paramref name="topN"/> as null to
    /// return every distinct URL (used for the CSV export) instead of capping the list.
    /// </summary>
    public Http4xxStats GetStats(int? topN = 100, Http4xxUrlFilter filter = Http4xxUrlFilter.All)
    {
        lock (_lock)
        {
            var allEvents = _events.ToList();
            var filteredEvents = filter switch
            {
                Http4xxUrlFilter.KnownAttacksOnly => allEvents.Where(e => IsKnownAttackUrl(e.Url)).ToList(),
                Http4xxUrlFilter.ExcludeKnownAttacks => allEvents.Where(e => !IsKnownAttackUrl(e.Url)).ToList(),
                _ => allEvents
            };

            if (!filteredEvents.Any())
            {
                return new Http4xxStats
                {
                    TotalEventsTracked = 0,
                    TopUrls = new List<Http4xxUrlStats>()
                };
            }

            var lastHour = DateTime.UtcNow.AddHours(-1);

            var topUrls = filteredEvents
                .GroupBy(e => new { e.Url, e.StatusCode })
                .OrderByDescending(g => g.Count())
                .Select(g => new Http4xxUrlStats
                {
                    Url = g.Key.Url,
                    StatusCode = g.Key.StatusCode,
                    Count = g.Count(),
                    LastSeen = g.Max(e => e.Timestamp),
                    IsKnownAttack = IsKnownAttackUrl(g.Key.Url)
                });

            if (topN.HasValue)
            {
                topUrls = topUrls.Take(topN.Value);
            }

            return new Http4xxStats
            {
                TotalEventsTracked = filteredEvents.Count,
                LastHourCount = filteredEvents.Count(e => e.Timestamp >= lastHour),
                OldestEventTime = filteredEvents.Min(e => e.Timestamp),
                TopUrls = topUrls.ToList()
            };
        }
    }
}

public enum Http4xxUrlFilter
{
    All,
    KnownAttacksOnly,
    ExcludeKnownAttacks
}

public class Http4xxEvent
{
    public DateTime Timestamp { get; set; }
    public string Url { get; set; }
    public int StatusCode { get; set; }
}

public class Http4xxStats
{
    public int TotalEventsTracked { get; set; }
    public int LastHourCount { get; set; }
    public DateTime OldestEventTime { get; set; }
    public List<Http4xxUrlStats> TopUrls { get; set; }
}

public class Http4xxUrlStats
{
    public string Url { get; set; }
    public int StatusCode { get; set; }
    public int Count { get; set; }
    public DateTime LastSeen { get; set; }
    public bool IsKnownAttack { get; set; }
}
