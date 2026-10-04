<#
.SYNOPSIS
    Reloads the local SQL database and the SongIndexTest search index from the newest
    production backups in local/ (backup-*.txt and index-*.txt).

.DESCRIPTION
    Drives the same /Admin/ReloadDatabase and /Admin/LoadIdx actions as the UploadBackup page,
    against a dev server this script starts itself with a pinned, known-safe environment:
    local LocalDB connection string (PROD_DB/TEST_DB forced off) and SEARCHINDEX=SongIndexTest.
    It never talks to a server it didn't start, and refuses to load the index unless the server
    reports a songs-test-* active index.

    Steps (run separately so a failing step can be fixed and retried):
      start       build and start the dev server (restarts it if this script started one)
      reload-db   POST the newest backup-*.txt to ReloadDatabase (Reload mode)
      load-index  POST the newest index-*.txt to LoadIdx with idxName=SongIndexTest
      stop        stop the dev server this script started
      all         start, reload-db, load-index, stop
#>
param(
    [ValidateSet('start', 'reload-db', 'load-index', 'stop', 'all')]
    [string]$Step = 'all',
    [string]$BackupFile,
    [string]$IndexFile
)

$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$LocalDir = Join-Path $RepoRoot 'local'
$WorkDir = Join-Path $LocalDir 'reload-local-data'
$PidFile = Join-Path $WorkDir 'server.pid'
$ServerLog = Join-Path $WorkDir 'server.log'
$ServerErr = Join-Path $WorkDir 'server.err.log'
$BaseUrl = 'https://localhost:5001'

# The only index this script will ever load. Hard-coded on purpose: never parameterize it.
$TestIndexId = 'SongIndexTest'
$TestIndexNamePattern = '^songs-test-\d+$'

New-Item -ItemType Directory -Force $WorkDir | Out-Null

function Write-Step([string]$message) { Write-Host "==> $message" -ForegroundColor Cyan }
function Fail([string]$message) { Write-Host "FAILED: $message" -ForegroundColor Red; exit 1 }

function Get-NewestFile([string]$pattern) {
    $file = Get-ChildItem -Path $LocalDir -Filter $pattern -File |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $file) { Fail "No $pattern found in $LocalDir" }
    $file.FullName
}

function Get-ServerProcess {
    if (-not (Test-Path $PidFile)) { return $null }
    $id = [int](Get-Content $PidFile)
    Get-Process -Id $id -ErrorAction SilentlyContinue
}

function Test-PortInUse([int]$port) {
    [bool](Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)
}

function Stop-Server {
    $proc = Get-ServerProcess
    if ($proc) {
        Write-Step "Stopping dev server (pid $($proc.Id))"
        # dotnet run spawns the app as a child; kill the whole tree.
        & taskkill /PID $proc.Id /T /F | Out-Null
    }
    Remove-Item $PidFile -ErrorAction SilentlyContinue
}

function Show-ServerLogTail([int]$lines = 60) {
    foreach ($log in @($ServerLog, $ServerErr)) {
        if (Test-Path $log) {
            Write-Host "---- tail of $log (Vite manifest noise removed) ----"
            # The script doesn't build the client, so every page logs missing-Vite-asset errors.
            Get-Content $log | Where-Object { $_ -notmatch 'Vite manifest|ViteTagHelper|ViteManifest|forget to build the assets' } |
                Select-Object -Last $lines
        }
    }
}

function Start-Server {
    Stop-Server
    foreach ($port in 5000, 5001) {
        if (Test-PortInUse $port) {
            Fail "Port $port is already in use. Stop the running dev server first - this script only talks to a server it started with a known-safe environment."
        }
    }
    if ($env:AZURE_SQL_CONNECTIONSTRING) {
        Fail 'AZURE_SQL_CONNECTIONSTRING is set in this environment; it would point the server at Azure SQL.'
    }

    # Pinned environment: local DB, test index. Inherited by the child process only.
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = "$BaseUrl;http://localhost:5000"
    $env:SEARCHINDEX = $TestIndexId
    $env:PROD_DB = 'false'
    $env:TEST_DB = 'false'
    Remove-Item Env:SEARCHINDEXVERSION -ErrorAction SilentlyContinue

    Write-Step 'Building and starting dev server (local DB, SEARCHINDEX=SongIndexTest)'
    $proc = Start-Process dotnet -ArgumentList 'run', '--project', 'm4d', '--no-launch-profile' `
        -WorkingDirectory $RepoRoot -RedirectStandardOutput $ServerLog -RedirectStandardError $ServerErr `
        -NoNewWindow -PassThru
    $proc.Id | Set-Content $PidFile

    $deadline = (Get-Date).AddMinutes(10)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 3
        if ($proc.HasExited) { Show-ServerLogTail; Fail "dev server exited (code $($proc.ExitCode)) during startup" }
        try {
            $null = Invoke-WebRequest "$BaseUrl/" -SkipCertificateCheck -TimeoutSec 30 -MaximumRedirection 0 `
                -SkipHttpErrorCheck
            break
        }
        catch { }
    }
    if ((Get-Date) -ge $deadline) { Show-ServerLogTail; Fail 'dev server did not respond within 10 minutes' }

    $log = Get-Content $ServerLog -Raw
    if ($log -match 'PROD_DB mode|TEST_DB mode: Using') { Stop-Server; Fail 'server picked up a cloud database connection string' }
    if ($log -notmatch 'Using DanceMusicContextConnection') { Show-ServerLogTail; Stop-Server; Fail 'server did not report using the local DanceMusicContextConnection' }
    Write-Step "Dev server is up at $BaseUrl (pid $($proc.Id))"
}

function Get-AdminCredentials {
    $secrets = & dotnet user-secrets list --project (Join-Path $RepoRoot 'm4d')
    $user = ($secrets | Where-Object { $_ -match '^M4D_ADMIN_USER = ' }) -replace '^M4D_ADMIN_USER = ', ''
    $password = ($secrets | Where-Object { $_ -match '^M4D_ADMIN_PASSWORD = ' }) -replace '^M4D_ADMIN_PASSWORD = ', ''
    if (-not $user -or -not $password) { Fail 'M4D_ADMIN_USER / M4D_ADMIN_PASSWORD are not set in m4d user secrets' }
    @{ User = $user; Password = $password }
}

function Get-AntiforgeryToken([string]$html) {
    $m = [regex]::Match($html, 'name="__RequestVerificationToken"\s+type="hidden"\s+value="([^"]+)"')
    if (-not $m.Success) { $m = [regex]::Match($html, 'value="([^"]+)"[^>]*name="__RequestVerificationToken"') }
    if (-not $m.Success) { Fail 'could not find an antiforgery token on the page' }
    $m.Groups[1].Value
}

function Connect-Admin {
    if (-not (Get-ServerProcess)) { Fail "no dev server started by this script is running - run with -Step start first" }
    $creds = Get-AdminCredentials
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession

    $login = Invoke-WebRequest "$BaseUrl/Identity/Account/Login" -WebSession $session -SkipCertificateCheck
    $token = Get-AntiforgeryToken $login.Content
    $null = Invoke-WebRequest "$BaseUrl/Identity/Account/Login" -Method Post -WebSession $session `
        -SkipCertificateCheck -SkipHttpErrorCheck -Form @{
        'Input.UserName'             = $creds.User
        'Input.Password'             = $creds.Password
        'Input.RememberMe'           = 'false'
        '__RequestVerificationToken' = $token
    }

    # Follows redirects: a failed login lands on the login page, which fails the content check.
    $page = Invoke-WebRequest "$BaseUrl/Admin/UploadBackup" -WebSession $session -SkipCertificateCheck -SkipHttpErrorCheck
    if ($page.StatusCode -ne 200 -or $page.Content -notmatch 'Reload the Database') {
        Fail "login as the admin user failed (UploadBackup returned $($page.StatusCode)) - check the M4D_ADMIN_* secrets and that the user has the dbAdmin role"
    }
    @{ Session = $session; Token = (Get-AntiforgeryToken $page.Content) }
}

function Read-AdminResult($response, [string]$what) {
    $html = $response.Content
    $status = [regex]::Match($html, '<h2>[^<]*:\s*(True|False)\s*</h2>')
    $message = [regex]::Match($html, '<p>Message:\s*([^<]*)</p>')
    if ($response.StatusCode -ne 200 -or -not $status.Success) {
        Show-ServerLogTail
        Fail "$what returned HTTP $($response.StatusCode) without a Results page"
    }
    $text = [System.Net.WebUtility]::HtmlDecode($message.Groups[1].Value.Trim())
    if ($status.Groups[1].Value -ne 'True') {
        Show-ServerLogTail
        Fail "$what reported failure: $text"
    }
    Write-Step "$what succeeded: $text"
}

function Invoke-ReloadDatabase {
    $file = if ($BackupFile) { (Resolve-Path $BackupFile).Path } else { Get-NewestFile 'backup-*.txt' }
    $admin = Connect-Admin
    Write-Step "Reloading local database from $file"
    $response = Invoke-WebRequest "$BaseUrl/Admin/ReloadDatabase" -Method Post -WebSession $admin.Session `
        -SkipCertificateCheck -SkipHttpErrorCheck -TimeoutSec 3600 -Form @{
        FileUpload                   = Get-Item $file
        reloadDatabase               = 'Reload'
        __RequestVerificationToken   = $admin.Token
    }
    Read-AdminResult $response 'ReloadDatabase'
}

function Assert-TestIndex($admin) {
    $diag = Invoke-WebRequest "$BaseUrl/Admin/Diagnostics" -WebSession $admin.Session -SkipCertificateCheck
    $env = [regex]::Match($diag.Content, '<b>Enviroment:</b>\s*([^<\s]+)')
    $active = [regex]::Match($diag.Content, '<b>Active Index:</b>\s*([^<\s]+)')
    if (-not $env.Success -or $env.Groups[1].Value -ne $TestIndexId) {
        Fail "server's SEARCHINDEX is '$($env.Groups[1].Value)', not $TestIndexId - refusing to load the index"
    }
    if (-not $active.Success -or $active.Groups[1].Value -notmatch $TestIndexNamePattern) {
        Fail "server's active index is '$($active.Groups[1].Value)', which is not a songs-test-* index - refusing to load"
    }
    Write-Step "Confirmed target: $TestIndexId -> $($active.Groups[1].Value)"
}

function Invoke-LoadIndex {
    $file = if ($IndexFile) { (Resolve-Path $IndexFile).Path } else { Get-NewestFile 'index-*.txt' }
    $admin = Connect-Admin
    Assert-TestIndex $admin
    Write-Step "Loading $TestIndexId from $file"
    $response = Invoke-WebRequest "$BaseUrl/Admin/LoadIdx" -Method Post -WebSession $admin.Session `
        -SkipCertificateCheck -SkipHttpErrorCheck -TimeoutSec 7200 -Form @{
        FileUpload                 = Get-Item $file
        idxName                    = $TestIndexId
        __RequestVerificationToken = $admin.Token
    }
    Read-AdminResult $response 'LoadIdx'
}

switch ($Step) {
    'start' { Start-Server }
    'reload-db' { Invoke-ReloadDatabase }
    'load-index' { Invoke-LoadIndex }
    'stop' { Stop-Server }
    'all' {
        Start-Server
        try { Invoke-ReloadDatabase; Invoke-LoadIndex }
        finally { Stop-Server }
    }
}
