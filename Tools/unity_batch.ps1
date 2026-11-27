# Executa o Unity em batchmode neste projeto.
# Uso:
#   .\Tools\unity_batch.ps1 -Mode compile
#   .\Tools\unity_batch.ps1 -Mode method -Method ChronoDefense.Editor.MvpDataGenerator.Generate
#   .\Tools\unity_batch.ps1 -Mode tests -Platform EditMode   (ou PlayMode)
param(
    [ValidateSet("compile", "method", "tests")] [string]$Mode = "compile",
    [string]$Method = "",
    [ValidateSet("EditMode", "PlayMode")] [string]$Platform = "EditMode",
    [string]$OutDir = "$PSScriptRoot\..\Logs\Batch"
)
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.10f1\Editor\Unity.exe"
$project = (Resolve-Path "$PSScriptRoot\..").Path
New-Item -ItemType Directory -Force $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path
$log = Join-Path $OutDir "$Mode.log"
$args = @("-batchmode", "-projectPath", "`"$project`"", "-logFile", "`"$log`"")
switch ($Mode) {
    "compile" { $args += @("-nographics", "-quit") }
    "method"  { $args += @("-nographics", "-quit", "-executeMethod", $Method) }
    "tests"   {
        $xml = Join-Path $OutDir "$Platform.xml"
        if (Test-Path $xml) { Remove-Item $xml }
        $args += @("-runTests", "-testPlatform", $Platform, "-testResults", "`"$xml`"")
        if ($Platform -eq "EditMode") { $args += "-nographics" }
    }
}
$p = Start-Process -FilePath $unity -ArgumentList $args -PassThru -Wait
"exit=$($p.ExitCode)"
$errors = Select-String -Path $log -Pattern "error CS\d+" | ForEach-Object { $_.Line.Trim() } | Sort-Object -Unique
if ($errors) { "--- erros de compilação:"; $errors | Select-Object -First 40 }
if ($Mode -eq "tests" -and (Test-Path $xml)) {
    [xml]$x = Get-Content $xml
    $r = $x.'test-run'
    "total=$($r.total) passed=$($r.passed) failed=$($r.failed) skipped=$($r.skipped)"
    $x.SelectNodes("//test-case[@result='Failed']") | ForEach-Object { "FAIL $($_.fullname)`n  $($_.failure.message.'#cdata-section')" }
}
