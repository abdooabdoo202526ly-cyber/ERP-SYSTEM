# =============================================================================
# ERP-SYSTEM v1.0.18 - NUKE AND INSTALL (one-shot definitive installer)
# =============================================================================
# Plain ASCII only - PowerShell 7 safe (no &, no em-dash, no smart quotes)
# =============================================================================

[CmdletBinding()]
param(
    [switch]$Help
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir   = Resolve-Path "$ScriptDir\.."
$DockerDir = Join-Path $RootDir "infra\docker"
$ComposeFile = Join-Path $DockerDir "docker-compose.dev.yml"
$ProjectName = "erp-system"
$ApiUrl = "http://localhost:5000"
$FrontendUrl = "http://localhost:3000"

# --- helpers (all single-quoted strings to avoid parser issues) ---------------
function W($c, $m) { Write-Host $m -ForegroundColor $c }

function Test-Docker {
    W 'Yellow' '[*] Checking Docker...'
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { W 'Red' '[X] Docker not installed'; exit 1 }
    try { docker info | Out-Null } catch { W 'Red' '[X] Docker daemon not running'; exit 1 }
    W 'Green' '[+] Docker OK'
}

function Nuke-All {
    W 'Yellow' ''
    W 'Yellow' '=== STEP 1: Nuking all ERP artifacts (containers + images + volumes) ==='
    W 'Yellow' ''

    W 'Yellow' '[*] Stopping erp-* containers...'
    $ids = docker ps -a --filter "name=erp-" --format "{{.ID}}" 2>$null
    if ($ids) { $ids | ForEach-Object { docker stop $_ 2>$null | Out-Null; docker rm -f $_ 2>$null | Out-Null } }
    W 'Green' '[+] Containers gone'

    W 'Yellow' '[*] Removing erp-system_postgres_data volume...'
    docker volume ls --format "{{.Name}}" 2>$null | Select-String "postgres_data" | ForEach-Object {
        W 'Yellow' ('    Removing ' + $_ + '...')
        docker volume rm $_ 2>$null | Out-Null
    }
    W 'Green' '[+] Volumes gone'

    W 'Yellow' '[*] Removing erp-system images...'
    docker images --format "{{.Repository}}:{{.Tag}} {{.ID}}" 2>$null | Select-String "erp-system" | ForEach-Object {
        $parts = $_ -split ' '
        $id = $parts[-1]
        W 'Yellow' ('    Removing ' + $id + '...')
        docker rmi -f $id 2>$null | Out-Null
    }
    W 'Green' '[+] Images gone'

    W 'Yellow' '[*] Removing erp-system networks...'
    docker network ls --format "{{.Name}}" 2>$null | Select-String "erp-system" | ForEach-Object {
        W 'Yellow' ('    Removing ' + $_ + '...')
        docker network rm $_ 2>$null | Out-Null
    }
    W 'Green' '[+] Networks gone'
}

function Build-Fresh {
    W 'Yellow' ''
    W 'Yellow' '=== STEP 2: Building fresh images ==='
    W 'Yellow' ''
    Push-Location $DockerDir
    try {
        docker compose -p $ProjectName -f $ComposeFile build --no-cache 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { W 'Red' '[X] build failed'; exit 1 }
    } finally { Pop-Location }
    W 'Green' '[+] Images built'
}

function Start-Fresh {
    W 'Yellow' ''
    W 'Yellow' '=== STEP 3: Starting containers (fresh volume, SQL init will run) ==='
    W 'Yellow' ''
    Push-Location $DockerDir
    try {
        docker compose -p $ProjectName -f $ComposeFile up -d 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { W 'Red' '[X] up failed'; exit 1 }
    } finally { Pop-Location }
    W 'Green' '[+] Containers started'
}

function Wait-Http($url, $label, $max = 90) {
    W 'Yellow' ('[*] Waiting for ' + $label + ' (' + $url + ')...')
    for ($i = 1; $i -le $max; $i++) {
        try {
            $r = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
            if ($r.StatusCode -lt 400) { W 'Green' ('[+] ' + $label + ' is up after ' + $i + 's'); return $true }
        } catch {}
        Start-Sleep 1
    }
    W 'Red' ('[X] ' + $label + ' did not respond in ' + $max + 's')
    return $false
}

function Verify-Schema {
    W 'Yellow' ''
    W 'Yellow' '=== STEP 4: Verifying schema ==='
    W 'Yellow' ''

    $count = docker exec erp-postgres psql -U erp_user -d erp_system -tA -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public';" 2>$null
    $count = $count.Trim()
    W 'Green' ('[+] Found ' + $count + ' tables in public schema')

    if ([int]$count -lt 45) {
        W 'Red' '[X] Schema incomplete. Run EMERGENCY-RESET-DB.ps1'
        return $false
    }

    $accExists = docker exec erp-postgres psql -U erp_user -d erp_system -tA -c "SELECT EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename = 'accounts');" 2>$null
    $accExists = $accExists.Trim()
    if ($accExists -eq 't') { W 'Green' '[+] accounts table exists' }
    else { W 'Red' '[X] accounts table MISSING'; return $false }

    W 'Yellow' '[*] accounts columns:'
    docker exec erp-postgres psql -U erp_user -d erp_system -tA -c "SELECT column_name FROM information_schema.columns WHERE table_name = 'accounts' ORDER BY ordinal_position;" 2>$null | ForEach-Object {
        W 'Gray' ('    ' + $_)
    }

    return $true
}

function Verify-Accounts-Endpoint {
    W 'Yellow' ''
    W 'Yellow' '=== STEP 5: Verifying /api/finance/accounts ==='
    W 'Yellow' ''

    $login = @{ email = "admin@alfajr.local"; password = "Demo1234" } | ConvertTo-Json
    try {
        $resp = Invoke-RestMethod -Uri "$ApiUrl/api/auth/login" -Method Post -Body $login -ContentType "application/json"
        $token = $resp.accessToken
        W 'Green' '[+] Login OK'
    } catch {
        W 'Red' ('[X] Login failed: ' + $_)
        return $false
    }

    try {
        $resp = Invoke-RestMethod -Uri "$ApiUrl/api/finance/accounts" -Headers @{ Authorization = "Bearer $token" } -ErrorAction Stop
        W 'Green' ('[+] /api/finance/accounts returned ' + $resp.Count + ' accounts')
        return $true
    } catch {
        $sc = $_.Exception.Response.StatusCode.value__
        W 'Red' ('[X] /api/finance/accounts returned ' + $sc)
        W 'Yellow' '    Last API errors:'
        docker logs erp-api --tail 50 2>&1 | Select-String "ERR|finance/accounts|42P01" | Select-Object -Last 5 | ForEach-Object { W 'Gray' ('      ' + $_) }
        return $false
    }
}

# --- main --------------------------------------------------------------------
if ($Help) { Get-Help $MyInvocation.MyCommand.Path; exit 0 }

W 'Cyan' ''
W 'Cyan' '============================================================'
W 'Cyan' '  ERP-SYSTEM v1.0.18 - NUKE AND INSTALL'
W 'Cyan' '  (Deletes EVERYTHING and starts from scratch)'
W 'Cyan' '============================================================'
W 'Cyan' ''

Test-Docker
Nuke-All
Build-Fresh
Start-Fresh

if (-not (Wait-Http "$ApiUrl/health" "API")) { exit 1 }
W 'Yellow' '[*] Waiting 20s for migrations...'
Start-Sleep 20

if (-not (Verify-Schema)) {
    W 'Red' ''
    W 'Red' 'Schema verification failed.'
    exit 1
}

if (-not (Verify-Accounts-Endpoint)) {
    W 'Red' ''
    W 'Red' 'Accounts endpoint failed. Run: docker logs erp-api --tail 200'
    exit 1
}

W 'Green' ''
W 'Green' '============================================================'
W 'Green' '  SUCCESS!'
W 'Green' ('  Open: ' + $FrontendUrl)
W 'Green' '  Login: admin@alfajr.local / Demo1234'
W 'Green' '============================================================'
W 'Green' ''
Start-Process $FrontendUrl
