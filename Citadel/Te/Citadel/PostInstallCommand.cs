using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Reflection;

namespace Te.Citadel
{
    // XXX TODO There are some steps you need to take for the post-install exec to work correctly
    // when making a 64 bit MSI installer. You need to modify the 64 bit MSI as described at the
    // following locations:
    // http://stackoverflow.com/questions/10275106/badimageformatexception-x64-issue/10281533#10281533
    // http://stackoverflow.com/questions/5475820/system-badimageformatexception-when-installing-program-from-vs2010-installer-pro/6797989#6797989
    //
    // Just in case. Steps are: First, ensure you have Orca installed. Run Orca and open your
    // project's MSI folder Select the Binary table Double click the cell [Binary Data] for the
    // record InstallUtil Make sure "Read binary from filename" is selected Click the Browse button
    // Browse to C:\Windows\Microsoft.NET\Framework64\v4.0.30319 Select InstallUtilLib.dll Click the
    // Open button and then the OK button
    [RunInstaller(true)]
    public partial class PostInstallCommand : System.Configuration.Install.Installer
    {
        public PostInstallCommand()
        {
            InitializeComponent();
        }

        public override void Commit(IDictionary savedState)
        {
            base.Commit(savedState);
            System.Diagnostics.Process.Start(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\CloudVeil.exe");
            base.Dispose();
        }
    }
}