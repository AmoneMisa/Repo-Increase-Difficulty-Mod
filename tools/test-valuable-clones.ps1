param([string]$UnityPath='C:\Program Files\Unity\Hub\Editor\6000.2.9f1\Editor\Unity.exe')
$ErrorActionPreference='Stop'
$cloneRepo=Split-Path -Parent $PSScriptRoot
$cloneProject=Join-Path $cloneRepo 'tests\UnityCloneRegression'
$cloneRuntime=Join-Path $cloneProject 'Assets\Runtime'
foreach($source in @('src\Visuals\DonorVisuals.cs','src\Valuables\ValuableArtInstance.cs','src\Valuables\CollectionCatalog.cs','src\Valuables\WheeledPropPhysics.cs','src\Valuables\WaterWatch.cs','src\Valuables\ValuableDynamics.cs','src\Patches\GumballRosterPatch.cs','src\Valuables\LiquidFlow.cs','src\Valuables\ValuableLayout.cs','src\Valuables\CollectionPhysics.cs','src\Valuables\TubeLossState.cs','src\Patches\Effects\LateJoinRules.cs','src\Valuables\CollectionLights.cs','src\Valuables\ManekiMotion.cs')) {
    Copy-Item -LiteralPath (Join-Path $cloneRepo $source) -Destination (Join-Path $cloneRuntime (Split-Path -Leaf $source)) -Force
}
$cloneReport=Join-Path $cloneProject 'clone-regression-result.txt'
if(Test-Path -LiteralPath $cloneReport){Remove-Item -LiteralPath $cloneReport}
$cloneLog=Join-Path $cloneRepo 'work\donor-clone-unity.log'
$cloneArgs=@('-batchmode','-nographics','-quit','-projectPath',('"'+$cloneProject+'"'),'-executeMethod','CloneRegression.Run','-logFile',('"'+$cloneLog+'"'))
$cloneProcess=Start-Process -FilePath $UnityPath -ArgumentList $cloneArgs -WindowStyle Hidden -Wait -PassThru
if($cloneProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $cloneReport)){throw ('Unity regression failed. See '+$cloneLog)}
Get-Content -LiteralPath $cloneReport
