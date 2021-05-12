Function Find-MsBuild([int] $MaxVersion = 2019)
{
    $agentPath2019 = "${Env:programfiles(x86)}\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
    $devPath2019 = "${Env:programfiles(x86)}\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    $proPath2019 = "${Env:programfiles(x86)}\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe"
    $communityPath2019 = "${Env:programfiles(x86)}\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
    $agentPath = "${Env:programfiles(x86)}\Microsoft Visual Studio\2017\BuildTools\MSBuild\15.0\Bin\msbuild.exe"
    $devPath = "${Env:programfiles(x86)}\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\msbuild.exe"
    $proPath = "${Env:programfiles(x86)}\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\msbuild.exe"
    $communityPath = "${Env:programfiles(x86)}\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\msbuild.exe"
    $fallback2015Path = "${Env:ProgramFiles(x86)}\MSBuild\14.0\Bin\MSBuild.exe"
    $fallback2013Path = "${Env:ProgramFiles(x86)}\MSBuild\12.0\Bin\MSBuild.exe"
    $fallbackPath = "C:\Windows\Microsoft.NET\Framework\v4.0.30319"

    If ((2019 -le $MaxVersion) -And (Test-Path $agentPath2019)) { return $agentPath2019 }
    If ((2019 -le $MaxVersion) -And (Test-Path $devPath2019)) { return $devPath2019 }
    If ((2019 -le $MaxVersion) -And (Test-Path $proPath2019)) { return $proPath2019 }
    If ((2019 -le $MaxVersion) -And (Test-Path $communityPath2019)) { return $communityPath2019 }
    If ((2017 -le $MaxVersion) -And (Test-Path $agentPath)) { return $agentPath } 
    If ((2017 -le $MaxVersion) -And (Test-Path $devPath)) { return $devPath } 
    If ((2017 -le $MaxVersion) -And (Test-Path $proPath)) { return $proPath } 
    If ((2017 -le $MaxVersion) -And (Test-Path $communityPath)) { return $communityPath } 
    If ((2015 -le $MaxVersion) -And (Test-Path $fallback2015Path)) { return $fallback2015Path } 
    If ((2013 -le $MaxVersion) -And (Test-Path $fallback2013Path)) { return $fallback2013Path } 
    If (Test-Path $fallbackPath) { return $fallbackPath } 
        
    throw "Yikes - Unable to find msbuild"
}

<# Finish this tomorrow #>
Function Find-SignTool()
{
    $winsdkKey = Get-ItemProperty -Path "Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Microsoft SDKs\Windows\v8.0" -ErrorAction SilentlyContinue

    If (!$winsdkKey -or !$winsdkKey.InstallationFolder) {
        $winsdkKey = Get-ItemProperty -Path "Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Kits\Installed Roots"

        If (!$winsdkKey) { return '' }
        If (!$winsdkKey.KitsRoot10) { return '' }

        $winsdk = $winsdkKey.KitsRoot10

        Write-Host $winsdk
    } Else {
        If(!$winsdkKey.InstallationFolder) { return '' }
        $winsdk = $winsdkKey.InstallationFolder
    }

    $signtoolWinSDK = $winsdk + "\bin\x86\signtool.exe"

    If(Test-Path $signtoolWinSDK) {
        return $signtoolWinSDK
    }

    $win10SDKKey = Get-ItemProperty -Path "Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Microsoft SDKs\Windows\v10.0" -ErrorAction SilentlyContinue

    If(!$win10SDKKey) {
        $win10SDKKey = Get-ItemProperty -Path "Registry::HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Microsoft SDKs\Windows\v10.0" -ErrorAction SilentlyContinue

        if(!$win10SDKKey) {
            return ''
        }
    }

    $win10SDKBinPath = $win10SDKKey.InstallationFolder + "bin\"
    $win10SDKVersion = $win10SDKKey.ProductVersion + ".0\"
    $win10SDKVersionBinPath = $win10SDKBinPath + $win10SDKVersion

    $signtool = $win10SDKVersionBinPath + "x86\signtool.exe"

    if(Test-Path $signtool) {
        return $signtool
    }

    return ''
}

$configuration = "Release"
if($Env:CONFIGURATION -eq "Debug") {
    $configuration = "Debug"
}

$msbuildPath = Find-MsBuild
$signtoolPath = Find-SignTool
$currentLocation = Get-Location

$wixVerifyPath = Join-Path $currentLocation "wix-verify-bin\wix-verify.exe"

$cleanBuild = "Clean,Build"

$builds = @(
    @("AnyCPU", "InstallGuard\InstallGuard.csproj", $cleanBuild),
    @("x86", "InstallerCustomActions\InstallerCustomActions.csproj", $cleanBuild),
    @("x64", "InstallerCustomActions\InstallerCustomActions.csproj", $cleanBuild),
    @("x86", "FilterAgent.Windows\FilterAgent.Windows.csproj", $cleanBuild),
    @("x64", "FilterAgent.Windows\FilterAgent.Windows.csproj", $cleanBuild),
    @("x64", "CloudVeilService\CloudVeilService.csproj", $cleanBuild),
    @("x86", "CloudVeilService\CloudVeilService.csproj", $cleanBuild),
    @("x64", "CloudVeilGUI\CloudVeilGUI.csproj", "Build"),
    @("x86", "CloudVeilGUI\CloudVeilGUI.csproj", "Build"),
    @("AnyCPU", "CloudVeilInstallerUI\CloudVeilInstallerUI.csproj", $cleanBuild)
)

$logFile = Join-Path $currentLocation "build.log"
if(Test-Path $logFile) {
    del $logFile
}

foreach($build in $builds) {
    $projPath = Join-Path $currentLocation $build[1]
    echo ("Cleaning" + $build[1] + " " + $build[0])

    $platform = $build[0]
    
    if($platform -eq "Any CPU") {
        & $msbuildPath /Verbosity:minimal /p:Configuration=$configuration $projPath /t:Clean 
    } else {
        & $msbuildPath /Verbosity:minimal /p:Configuration=$configuration /p:Platform=$platform $projPath /t:Clean 
    }
}

foreach ($build in $builds) {
    $projPath = Join-Path $currentLocation $build[1]
    echo ("Building " + $build[1] + " " + $build[0])

    $platform = $build[0]
    $target = $build[2]

    & $msbuildPath /Verbosity:minimal /p:Configuration=$configuration $projPath /t:Restore 

    if ($platform -eq "Any CPU") {
        & $msbuildPath /Verbosity:minimal /p:Configuration=$configuration $projPath /t:Build 
    } else {
        & $msbuildPath /Verbosity:minimal /p:Configuration=$configuration /p:Platform=$platform $projPath /t:Build 
    }

    if($LastExitCode -ne 0) {
        echo "ERRORS OCCURRED WHILE BUILDING!"
        echo "See build.log for more info."
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
echo "Signing x64 executables"
& $signtoolPath sign /fd SHA512 /tr http://timestamp.digicert.com /a "CloudVeilGUI\bin\$configuration x64\CloudVeil.exe" 
& $signtoolPath sign /fd SHA512 /tr http://timestamp.digicert.com /a "CloudVeilGUI\bin\$configuration x64\FilterServiceProvider.exe" 
& $signtoolPath sign /fd SHA512 /tr http://timestamp.digicert.com /a "CloudVeilGUI\bin\$configuration x64\FilterAgent.Windows.exe" 

echo "Building MSI x64"

& $msbuildPath /p:Configuration=$configuration /p:SolutionDir=$currentLocation $payload64 /t:Clean,Build 
& $msbuildPath /p:Configuration=$configuration /p:SolutionDir=$currentLocation $setup64 /t:Clean,Build,SignMsi 

# echo "Signing MSI x64"
# & $signtoolPath sign /fd SHA512 /tr http://timestamp.digicert.com /a $output64 

<# Sign executable files x86 #>
echo "Signing x86 executables" 
& $signtoolPath sign /fd SHA512 /tr http://timestamp.digicert.com /a "CloudVeilGUI\bin\$configuration x86\CloudVeil.exe" 
& $signtoolPath sign /fd SHA512 /tr http://timestamp.digicert.com /a "CloudVeilGUI\bin\$configuration x86\FilterServiceProvider.exe" 
& $signtoolPath sign /fd SHA512 /tr http://timestamp.digicert.com /a "CloudVeilGUI\bin\$configuration x86\FilterAgent.Windows.exe" 

echo "Building MSI x86"
& $msbuildPath /p:Configuration=$configuration /p:SolutionDir=$currentLocation $payload86 /t:Clean,Build 
& $msbuildPath /p:Configuration=$configuration /p:SolutionDir=$currentLocation $setup86 /t:Clean,Build,SignMsi 

# & $signtoolPath sign /fd SHA512 /tr http://timestamp.digicert.com /a $output86

$versionString = & $wixVerifyPath get $product64 wix.product.version
$versionObj = [System.Version]::Parse($versionString)
$version = $versionObj.ToString(3)

echo "Building installer bundle x86"
& $msbuildPath /p:Configuration=$configuration /p:SolutionDir=$currentLocation $bundleProject /p:Platform=x86 /p:MsiPlatform=x86 /t:Clean,Build,SignBundleEngine,SignBundle 

$finalBundle86 = Join-Path $currentLocation "Installers\CloudVeilInstaller-$version-cv4w-x86.exe"
$final86 = Join-Path $currentLocation "Installers\CloudVeil-$version-winx86.msi"
Copy-Item $bundle86 -Destination $finalBundle86
Copy-Item $output86 -Destination $final86

echo "Building installer bundle x64"
& $msbuildPath /p:Configuration=$configuration /p:SolutionDir=$currentLocation $bundleProject /p:Platform=x86 /p:MsiPlatform=x64 /t:Clean,Build,SignBundleEngine,SignBundle 

$finalBundle64 = Join-Path $currentLocation "Installers\CloudVeilInstaller-$version-cv4w-x64.exe"
$final64 = Join-Path $currentLocation "Installers\CloudVeil-$version-winx64.msi"
Copy-Item $bundle64 -Destination $finalBundle64
Copy-Item $output64 -Destination $final64
