param([string]$UnityPath='C:\Program Files\Unity\Hub\Editor\6000.2.9f1\Editor\Unity.exe')
$ErrorActionPreference='Stop'
$cosmeticRepo=Split-Path -Parent $PSScriptRoot
$cosmeticProject=Join-Path $cosmeticRepo 'tests\UnityCloneRegression'
Copy-Item -LiteralPath (Join-Path $cosmeticRepo 'src\Cosmetics\CosmeticMotion.cs') -Destination (Join-Path $cosmeticProject 'Assets\Runtime\CosmeticMotion.cs') -Force
Copy-Item -LiteralPath (Join-Path $cosmeticRepo 'src\Valuables\SkeletonSway.cs') -Destination (Join-Path $cosmeticProject 'Assets\Runtime\SkeletonSway.cs') -Force
$cosmeticReport=Join-Path $cosmeticProject 'cosmetic-motion-result.txt'
if(Test-Path -LiteralPath $cosmeticReport){Remove-Item -LiteralPath $cosmeticReport}
$cosmeticLog=Join-Path $cosmeticRepo 'work\cosmetic-motion-unity.log'
$cosmeticArgs=@('-batchmode','-nographics','-quit','-projectPath',('"'+$cosmeticProject+'"'),'-executeMethod','CosmeticMotionRegression.Run','-logFile',('"'+$cosmeticLog+'"'))
$cosmeticProcess=Start-Process -FilePath $UnityPath -ArgumentList $cosmeticArgs -WindowStyle Hidden -Wait -PassThru
if($cosmeticProcess.ExitCode -ne 0 -or -not(Test-Path -LiteralPath $cosmeticReport)){throw ('Cosmetic motion checks failed. See '+$cosmeticLog)}
Get-Content -LiteralPath $cosmeticReport
