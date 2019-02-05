Function Find-MsBuild([int] $MaxVersion = 2017)
{
    $agentPath = "$Env:programfiles (x86)\Microsoft Visual Studio\2017\BuildTools\MSBuild\15.0\Bin\msbuild.exe"
    $devPath = "$Env:programfiles (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\msbuild.exe"
    $proPath = "$Env:programfiles (x86)\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\msbuild.exe"
    $communityPath = "$Env:programfiles (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\msbuild.exe"
    $fallback2015Path = "${Env:ProgramFiles(x86)}\MSBuild\14.0\Bin\MSBuild.exe"
    $fallback2013Path = "${Env:ProgramFiles(x86)}\MSBuild\12.0\Bin\MSBuild.exe"
    $fallbackPath = "C:\Windows\Microsoft.NET\Framework\v4.0.30319"
        
    If ((2017 -le $MaxVersion) -And (Test-Path $agentPath)) { return $agentPath } 
    If ((2017 -le $MaxVersion) -And (Test-Path $devPath)) { return $devPath } 
    If ((2017 -le $MaxVersion) -And (Test-Path $proPath)) { return $proPath } 
    If ((2017 -le $MaxVersion) -And (Test-Path $communityPath)) { return $communityPath } 
    If ((2015 -le $MaxVersion) -And (Test-Path $fallback2015Path)) { return $fallback2015Path } 
    If ((2013 -le $MaxVersion) -And (Test-Path $fallback2013Path)) { return $fallback2013Path } 
    If (Test-Path $fallbackPath) { return $fallbackPath } 
        
    throw "Yikes - Unable to find msbuild"
}

$msbuildPath = Find-MsBuild

$currentLocation = Get-Location

$wixVerifyPath = Join-Path $currentLocation "wix-verify-bin\wix-verify.exe"

$builds = @(
    @("x86", "InstallerCustomActions\InstallerCustomActions.csproj"),
    @("x64", "InstallerCustomActions\InstallerCustomActions.csproj"),
    @("x64", "CitadelService\CitadelService.csproj"),
    @("x86", "CitadelService\CitadelService.csproj"),
    @("x64", "CitadelGUI\CitadelGUI.csproj"),
    @("x86", "CitadelGUI\CitadelGUI.csproj")
)

foreach ($build in $builds) {
    $projPath = Join-Path $currentLocation $build[1]
    echo $projPath

    $platform = $build[0]

    if ($platform -eq "Any CPU") {
        & $msbuildPath /Verbosity:minimal /p:Configuration=Release $projPath /t:Clean,Build
    } else {
        & $msbuildPath /Verbosity:minimal /p:Configuration=Release /p:Platform=$platform $projPath /t:Clean,Build

    }
}

$payload86 = Join-Path $currentLocation "Installers\SetupPayload64\SetupPayload86.wixproj"
$setup86 = Join-Path $currentLocation "Installers\SetupProjects\Setup x86.wixproj"
$product86 = Join-Path $currentLocation "Installers\SetupProjects\Product-x86.wxs"
$output86 = Join-Path $currentLocation "Installers\SetupProjects\Release\Setup x86.msi"

$payload64 = Join-Path $currentLocation "Installers\SetupPayload64\SetupPayload64.wixproj"
$setup64 = Join-Path $currentLocation "Installers\SetupProjects\Setup x64.wixproj"
$product64 = Join-Path $currentLocation "Installers\SetupProjects\Product-x64.wxs"
$output64 = Join-Path $currentLocation "Installers\SetupProjects\Release\Setup x64.msi"

<# TODO: Sign executable files x64 #>

& $msbuildPath /p:Configuration=Release /p:SolutionDir=$currentLocation $payload64 /t:Clean,Build
& $msbuildPath /p:Configuration=Release /p:SolutionDir=$currentLocation $setup64 /t:Clean,Build

<# TODO: Sign executable files x86 #>

& $msbuildPath /p:Configuration=Release /p:SolutionDir=$currentLocation $payload86 /t:Clean,Build
& $msbuildPath /p:Configuration=Release /p:SolutionDir=$currentLocation $setup86 /t:Clean,Build

$version = & $wixVerifyPath get $product64 wix.product.version

$final64 = Join-Path $currentLocation "Installers\CloudVeil-$version-x64.msi"
$final86 = Join-Path $currentLocation "Installers\CloudVeil-$version-x86.msi"

<# TODO: Sign MSI #>

Copy-Item $output64 -Destination $final64
Copy-Item $output86 -Destination $final86