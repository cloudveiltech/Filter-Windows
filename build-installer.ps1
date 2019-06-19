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

<# Finish this tomorrow
Function Find-SignTool()
{
    $winsdkKey = Get-ItemProperty -Path "Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Microsoft SDKs\Windows\v8.0"
    If 
} #>

$configuration = "Release"
if($Env:CONFIGURATION -eq "Debug") {
    $configuration = "Debug"
}

$msbuildPath = Find-MsBuild

$currentLocation = Get-Location

$wixVerifyPath = Join-Path $currentLocation "wix-verify-bin\wix-verify.exe"

$builds = @(
    @("x86", "InstallerCustomActions\InstallerCustomActions.csproj"),
    @("x64", "InstallerCustomActions\InstallerCustomActions.csproj"),
    @("x86", "FilterAgent.Windows\FilterAgent.Windows.csproj"),
    @("x64", "FilterAgent.Windows\FilterAgent.Windows.csproj"),
    @("x64", "CitadelService\CitadelService.csproj"),
    @("x86", "CitadelService\CitadelService.csproj"),
    @("x64", "CitadelGUI\CitadelGUI.csproj"),
    @("x86", "CitadelGUI\CitadelGUI.csproj"),
    @("AnyCPU", "InstallGuard\InstallGuard.csproj"),
    @("AnyCPU", "CloudVeilInstallerUI\CloudVeilInstallerUI.csproj")
)

foreach ($build in $builds) {
    $projPath = Join-Path $currentLocation $build[1]
    echo $projPath

    $platform = $build[0]

    & $msbuildPath /Verbosity:minimal /p:Configuartion=$configuration $projPath /t:Restore

    if ($platform -eq "Any CPU") {
        & $msbuildPath /Verbosity:minimal /p:Configuration=$configuration $projPath /t:Clean,Build
    } else {
        & $msbuildPath /Verbosity:minimal /p:Configuration=$configuration /p:Platform=$platform $projPath /t:Clean,Build
    }

    if($LastExitCode -ne 0) {
        exit
    }
}

$bundleProject = "CloudVeilInstaller\CloudVeilInstaller.wixproj"

$payload86 = Join-Path $currentLocation "Installers\SetupPayload86\SetupPayload86.wixproj"
$setup86 = Join-Path $currentLocation "Installers\SetupProjects\Setup x86.wixproj"
$product86 = Join-Path $currentLocation "Installers\SetupProjects\Product-x86.wxs"
$output86 = Join-Path $currentLocation "Installers\SetupProjects\$configuration\Setup x86.msi"
$bundle86 = Join-Path $currentLocation "CloudVeilInstaller\bin\$configuration\CloudVeilInstaller-x86.exe"

$payload64 = Join-Path $currentLocation "Installers\SetupPayload64\SetupPayload64.wixproj"
$setup64 = Join-Path $currentLocation "Installers\SetupProjects\Setup x64.wixproj"
$product64 = Join-Path $currentLocation "Installers\SetupProjects\Product-x64.wxs"
$output64 = Join-Path $currentLocation "Installers\SetupProjects\$configuration\Setup x64.msi"
$bundle64 = Join-Path $currentLocation "CloudVeilInstaller\bin\$configuration\CloudVeilInstaller-x64.exe"

<# Sign executable files x64 #>
<#
signtool sign /fd SHA512 /tr http://timestamp.comodoca.com /a CitadelGUI\bin\$configuration x64\CloudVeil.exe
signtool sign /fd SHA512 /tr http://timestamp.comodoca.com /a CitadelGUI\bin\$configuration x64\FilterServiceProvider.exe
signtool sign /fd SHA512 /tr http://timestamp.comodoca.com /a CitadelGUI\bin\$configuration x64\FilterAgent.Windows.exe
#>

& $msbuildPath /p:Configuration=$configuration /p:SolutionDir=$currentLocation $payload64 /t:Clean,Build
& $msbuildPath /p:Configuration=$configuration /p:SolutionDir=$currentLocation $setup64 /t:Clean,Build,SignMsi

<# Sign executable files x86 #>
<#
signtool sign /fd SHA512 /tr http://timestamp.comodoca.com /a CitadelGUI\bin\$configuration x86\CloudVeil.exe
signtool sign /fd SHA512 /tr http://timestamp.comodoca.com /a CitadelGUI\bin\$configuration x86\FilterServiceProvider.exe
signtool sign /fd SHA512 /tr http://timestamp.comodoca.com /a CitadelGUI\bin\$configuration x86\FilterAgent.Windows.exe
#>

& $msbuildPath /p:Configuration=$configuration /p:SolutionDir=$currentLocation $payload86 /t:Clean,Build
& $msbuildPath /p:Configuration=$configuration /p:SolutionDir=$currentLocation $setup86 /t:Clean,Build,SignMsi

$version = & $wixVerifyPath get $product64 wix.product.version

& $msbuildPath /p:Configuration=$configuration /p:SolutionDir=$currentLocation $bundleProject /p:Platform=x86 /p:MsiPlatform=x86 /t:Clean,Build,SignBundleEngine,SignBundle

$finalBundle86 = Join-Path $currentLocation "Installers\CloudVeilInstaller-$version-cv4w1.7-x86.exe"
$final86 = Join-Path $currentLocation "Installers\CloudVeil-$version-winx86.msi"
Copy-Item $bundle86 -Destination $finalBundle86
Copy-Item $output86 -Destination $final86

& $msbuildPath /p:Configuration=$configuration /p:SolutionDir=$currentLocation $bundleProject /p:Platform=x86 /p:MsiPlatform=x64 /t:Clean,Build,SignBundleEngine,SignBundle

$finalBundle64 = Join-Path $currentLocation "Installers\CloudVeilInstaller-$version-cv4w1.7-x64.exe"
$final64 = Join-Path $currentLocation "Installers\CloudVeil-$version-winx64.msi"
Copy-Item $bundle64 -Destination $finalBundle64
Copy-Item $output64 -Destination $final64

