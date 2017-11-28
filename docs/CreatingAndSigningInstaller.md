
This documentation will cover building installers + signing them

This document assumes you have Visual Studio 2017 installed. You will also need Orca, which you can get in the Windows SDK (sorry, yes, they make you download the entire thing)

# Setup build environment

## Install git

First of all, search for `Git Bash` on your computer to make sure git isn't already installed. If you don't find it, go to [https://git-scm.com/downloads](https://git-scm.com/downloads)

Download the recommended version of git.

Install it.

There are approx. seven configuration screens that follow the license screen. They can be left as they are if you wish.

## Install visual studio 2017 installer extension

Go to `Tools > Extensions` then click on `Online`

Search for "installer projects"

Download "Microsoft Visula Studio 2017 Installer Projects"

Once that's done, close VS2017 and it will automatically install an extension.

## Install safenet client to access smart card

[https://support.comodo.com/index.php?/Knowledgebase/Article/View/1211/66/safenet-download-for-ev-codesigning-certificates](https://support.comodo.com/index.php?/Knowledgebase/Article/View/1211/66/safenet-download-for-ev-codesigning-certificates)

Scroll to the bottom of the page for the download link, then follow instructions on page.

When you install the safenet authentication client, make sure you have all browser windows closed

## Cloning project

Now that you've got all that set up, let's clone the project.

Navigate to and open "Git Bash"

I like to have a workspace where all of my git repositories go. I like this to be "C:\git" but it doesn't really matter.

```
mkdir /c/git
cd /c/git
git clone https://github.com/cloudveiltech/Citadel-Windows.git
cd Citadel-Windows
git submodule update --init
```
## Useful git commands

Run these in Git Bash

### get bleeding edge changes
```
git pull origin master
```

### get a specific tagged version for building
First, make sure that the tag exists in the local repository
```
git fetch --all --tags --prune
```

Not sure what tags are available? Run this for a list.
```
git tag
```

Then checkout the tag by running
```
git checkout tags/v.1.6.9
```

## Building installers
Once you've got the latest code pulled down from the server, go ahead and open the solution file in Visual Studio 2017. `(Project Root)\Citadel.sln`

(If this is your first time doing this, you may need to install the .NET targeting pack for 4.6.2)

On the top bar you'll see options for 'Debug' or 'Release' and 'Any CPU' or 'x86'. Let's change them to 'Release' and 'Any CPU' first.

## New instructions
### x64 Installer

1. Change configuration options to 'Release' and 'Any CPU'
2. Right-click CitadelGUI x64 and click Build.
3. Open SetupPayload64 and scroll down to `System.Net.Http.dll` @TechnikEmpire correct me if we don't need a separate step for SetupPayloa64
4. If there are two entries for `System.Net.Http.dll`, check SourcePath in the properties box (right-click, select properties)
5. If SourcePath does not contain `CloudVeilGUI` delete that entry. There should only be one `System.Net.Http.dll`
6. Go into Detected Dependencies (folder under SetupPayload64) and make sure all DLL's are excluded (right click, hit exclude) There should be a little red circle indicating whether the DLL is excluded.
7. Right-click Setup x64 and click Rebuild.

### x64 Orca instructions
These steps are very important and must be done before signing the installer, otherwise you'll end up needing to sign the installer again.

 1. Open Orca, drop the new `.msi` file in to it to open it.
 2. Find the "Binary" table.
 3. In the "Binary" table, find the "InstallUtil" property and double click the "Data" cell to edit it.
 4. From the now-open file dialog, browse to and select `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtilLib.dll` and click "Open".
 5. Ensure that "Read binary from filename" is selected.
 6. Click OK, then click save or ctrl+s to save these changes to the MSI file. **This entire process is necessary in order for the MSI to execute the custom actions we use to start up the GUI and spawn the server when installation is complete.** Without doing this, the installer would fail with a bad format exception, because unmodified, it is unable to execute/launch a 64 bit process.
 
### x86 Installer

1. Change configuration options to 'Release' and 'x86'
2. Right-click CitadelGUI x86 and click Build.
3. Open SetupPayload86 and scroll down to `System.Net.Http.dll` @TechnikEmpire correct me if we don't need a separate step for SetupPayloa64
4. If there are two entries for `System.Net.Http.dll`, check SourcePath in the properties box (right-click, select properties)
5. If SourcePath does not contain `CloudVeilGUI` delete that entry. There should only be one `System.Net.Http.dll`
6. Right-click Setup x86 and click Rebuild.

Follow the next instructions to sign the installer.

## Signing installers

### Method 1 - SignInstallers project.
After building both the x86 and x64 installers, right click SignInstallers and click build.

### Method 2 - Manually
1. Open "Developer Command Prompt for VS 2017" `Start Menu > Visual Studio 2017`
2. Go to wherever the installer was built with `cd` on the command prompt. (i.e. `cd C:\git\Citadel-Windows\Installers\Setup x64\bin\Release`)

With the certificate USB drive plugged in, all you should need to do is run
```
signtool.exe sign /a <Installer Name>.msi
```

Note that you'll need to run this once for both the x86 and the x64 installer.
```
Installers/Setup x64/Release/Setup-x64.msi
Installers/Setup x86/Release/Setup-x86.msi
```

## Verifying signatures
To verify that the sign tool was successful, run
```
signtool.exe verify /pa <Installer Name>.msi
```

You should get something like
```
Successfully verified: CloudVeil-1.6.12-x64.msi
```

# Other misc stuff

```
WARNING: File 'WinDivert64.sys' targeting 'x64' is not compatible with the project's target platform 'x86'
WARNING: File 'WinDivert64.sys' targeting 'x64' is not compatible with the project's target platform 'x86'
```

When you get this error try:

1. Make sure that you are on Release|x86 build
2. Rebuild CitadelGUI x86
3. Rebuild SetupPayload86

That should take care of that problem.
