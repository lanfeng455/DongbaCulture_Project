param(
    [string]$AdbPath='adb',
    [string]$Serial
)
$ErrorActionPreference='Stop'
$deviceArgs=@();if($Serial){$deviceArgs=@('-s',$Serial)}
$folder='/storage/emulated/0/Android/data/com.dongba.culture.picomr/files'
$request="$folder/dongba-checkpoint.request"
$temporary=[IO.Path]::GetTempFileName()
try {
    [IO.File]::WriteAllText($temporary,'reel',[Text.UTF8Encoding]::new($false))
    & $AdbPath @deviceArgs shell mkdir -p $folder
    if($LASTEXITCODE -ne 0){throw 'Cannot reach the PICO app files.'}
    & $AdbPath @deviceArgs shell rm -f "$request.result"
    & $AdbPath @deviceArgs push $temporary "$request.next"
    if($LASTEXITCODE -ne 0){throw 'Checkpoint upload failed.'}
    & $AdbPath @deviceArgs shell mv "$request.next" $request
    if($LASTEXITCODE -ne 0){throw 'Cannot queue checkpoint.'}
    Write-Output 'Reel checkpoint queued. In the updated development app, choose Story Mode and place the forest. It applies once the stage is ready.'
} finally {Remove-Item -LiteralPath $temporary -Force}