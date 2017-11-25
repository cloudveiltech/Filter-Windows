
This documentation will cover building installers + signing them

**THIS IS NOT COMPLETE**

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
```
## Useful git commands

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

## Signing installers

Open "Developer Command Prompt for VS 2017" which can be found in "Start Menu > Visual Studio 2017" (or by searching)

Navigate to wherever the installer was built with the `cd` command on the command prompt.

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