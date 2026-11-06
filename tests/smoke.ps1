# Windows PowerShell 5.1 / PowerShell 7：独立临时DB进行真实编译和HTTP联调。
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$api = Join-Path $root 'src\OpenAHU.Api\OpenAHU.Api.csproj'
$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ('OpenAHU-smoke-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tmp -Force | Out-Null
$env:OpenAHU__DatabasePath = Join-Path $tmp 'campus.db'
$env:OpenAHU__SessionPath = Join-Path $tmp 'session.json'
$proc = $null
function Assert($value,$message) { if(-not $value) { throw "FAIL: $message" } }
function Start-Api {
    $dll = Join-Path $root 'src\OpenAHU.Api\bin\Release\net10.0\OpenAHU.Api.dll'
    $script:proc = Start-Process -FilePath 'dotnet' -ArgumentList @('"' + $dll + '"') -PassThru -WindowStyle Hidden `
       -RedirectStandardOutput (Join-Path $tmp 'stdout.log') -RedirectStandardError (Join-Path $tmp 'stderr.log')
    for($i=0;$i -lt 100;$i++) {
        if(Test-Path $env:OpenAHU__SessionPath) {
            try {
                $s = Get-Content $env:OpenAHU__SessionPath -Raw -Encoding UTF8 | ConvertFrom-Json
                if($s.port -gt 0 -and $s.token -and $s.processId -eq $script:proc.Id) {return $s}
            } catch { }
        }
        if($script:proc.HasExited) { throw 'API unexpectedly exited; inspect temp logs' }
        Start-Sleep -Milliseconds 150
    }
    throw 'API failed to publish session.json in 15 sec'
}
function Stop-Api {
    if($script:proc -and -not $script:proc.HasExited) { $script:proc.Kill(); $script:proc.WaitForExit() }
    $script:proc = $null
    # abnormal stop simulates stale handshake; next boot must safely replace it
}
function ExpectStatus($url,$headers,[int]$expect) {
    try { Invoke-WebRequest -Uri $url -Headers $headers -UseBasicParsing | Out-Null; throw "Expected $expect, got 2xx" }
    catch {
        if(-not $_.Exception.Response) { throw }
        $status = [int]$_.Exception.Response.StatusCode
        Assert ($status -eq $expect) "Expected HTTP $expect, got $status"
    }
}
try {
    Write-Host 'Restoring and building (.NET 10 SDK required)...'
    dotnet restore $api; Assert ($LASTEXITCODE -eq 0) 'restore failed'
    dotnet build $api -c Release --no-restore; Assert ($LASTEXITCODE -eq 0) 'build failed'
    $s = Start-Api
    $h = @{ 'X-OpenAHU-Session' = $s.token }
    $base = $s.baseUrl + '/api/v1'
    $health = Invoke-RestMethod "$base/health" -Headers $h
    Write-Host ("Health response: " + ($health | ConvertTo-Json -Compress))
    Assert ($health.databaseReady -and $health.status -eq 'ok') 'health not ready'
    ExpectStatus "$base/terms" @{} 401
    ExpectStatus "$base/courses" $h 400
    ExpectStatus "$base/courses?termId=unknown" $h 404
    $terms = Invoke-RestMethod "$base/terms" -Headers $h
    Write-Host ("Demo terms count: " + $terms.Count)
    Assert ($terms.Count -ge 2) 'demo terms not seeded'
    $coursesBefore = Invoke-RestMethod "$base/courses?termId=term-2026-fall" -Headers $h
    Assert ($coursesBefore.Count -ge 3) 'demo courses not returned'
    $grades = Invoke-RestMethod "$base/grades?termId=term-2026-fall" -Headers $h
    Assert ($grades.Count -ge 3) 'demo grades not returned'
    $payload = @{ name='自动化测试课程';teacher='测试教师';location='测试楼';dayOfWeek=5;
        startSection=7;endSection=8;weekNumbers=@(1,3,5);termId='term-2026-fall' } | ConvertTo-Json -Depth 5 -Compress
    $created = Invoke-RestMethod "$base/local/courses" -Method POST -Headers $h -ContentType 'application/json; charset=utf-8' -Body ([System.Text.Encoding]::UTF8.GetBytes($payload))
    Assert ($created.id -like 'local-*') 'course was not created'
    $id = $created.id
    Stop-Api
    $s = Start-Api
    $h = @{ 'X-OpenAHU-Session' = $s.token }; $base = $s.baseUrl + '/api/v1'
    $coursesAfter = Invoke-RestMethod "$base/courses?termId=term-2026-fall" -Headers $h
    Assert (($coursesAfter | Where-Object id -eq $id).Count -eq 1) 'course was not persisted after restart'
    $updatedBody = @{ name='已修改课程';teacher='测试教师';location='测试楼';dayOfWeek=5;
        startSection=7;endSection=8;weekNumbers=@(1,3,5);termId='term-2026-fall' } | ConvertTo-Json -Depth 5 -Compress
    $updated = Invoke-RestMethod "$base/local/courses/$id" -Method PUT -Headers $h -ContentType 'application/json; charset=utf-8' -Body ([System.Text.Encoding]::UTF8.GetBytes($updatedBody))
    Assert ($updated.name -eq '已修改课程') 'course update failed'
    Invoke-RestMethod "$base/local/courses/$id" -Method DELETE -Headers $h | Out-Null
    $coursesAfterDelete = Invoke-RestMethod "$base/courses?termId=term-2026-fall" -Headers $h
    Assert (($coursesAfterDelete | Where-Object id -eq $id).Count -eq 0) 'course delete failed'
    # Member 05 safety regression: unmanaged SQLite files must not be adopted or overwritten.
    Stop-Api
    $unmanaged = Join-Path $tmp 'unmanaged.db'
    & python -c 'import sqlite3,sys; db=sqlite3.connect(sys.argv[1]); db.execute("CREATE TABLE user_notes (id INTEGER PRIMARY KEY,note TEXT NOT NULL)"); db.execute("INSERT INTO user_notes(note) VALUES (?)",("keep-this-row",)); db.commit(); db.close()' $unmanaged
    Assert ($LASTEXITCODE -eq 0) 'could not create unmanaged database for smoke test'
    $env:OpenAHU__DatabasePath = $unmanaged
    $s = Start-Api
    $h = @{ 'X-OpenAHU-Session' = $s.token }; $base = $s.baseUrl + '/api/v1'
    $unsafeHealth = Invoke-RestMethod "$base/health" -Headers $h
    Assert (($unsafeHealth.status -eq 'degraded') -and (-not $unsafeHealth.databaseReady)) 'unmanaged database incorrectly reported ready'
    Stop-Api
    & python -c 'import sqlite3,sys; db=sqlite3.connect(sys.argv[1]); assert db.execute("SELECT note FROM user_notes").fetchone()==("keep-this-row",); assert db.execute("SELECT name FROM sqlite_master WHERE name=?",("__EFMigrationsHistory",)).fetchone() is None; db.close()' $unmanaged
    Assert ($LASTEXITCODE -eq 0) 'unmanaged database was modified'
    Write-Host 'PASS: health / session 401 / term 400&404 / read / CRUD / restart persistence / unmanaged database preservation'
}
catch {
    Write-Host ("Smoke test failure: " + $_.Exception.Message)
    foreach($logName in @('stdout.log','stderr.log')) {
        $logPath = Join-Path $tmp $logName
        if(Test-Path $logPath) {
            Write-Host "--- $logName (last 120 lines) ---"
            Get-Content $logPath -Tail 120 | ForEach-Object { Write-Host $_ }
        }
    }
    throw
}
finally {
    Stop-Api
    Remove-Item Env:\OpenAHU__DatabasePath -ErrorAction SilentlyContinue
    Remove-Item Env:\OpenAHU__SessionPath -ErrorAction SilentlyContinue
    Write-Host "Temporary smoke folder: $tmp"
}
