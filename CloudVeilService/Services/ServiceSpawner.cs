/*
* Copyright © 2017-2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using NLog;
using Gui.CloudVeil.Util;
using System.Diagnostics;
using Win32Task = Microsoft.Win32.TaskScheduler.Task;

// Although we don't actually use Topshelf internally, we need
// to have this using statement present, so that topshelf
// assemblies are linked to us. We want all assemblies that the 
// embedded processes (which need to be compiled) require also
// linked to us, so we can easily fetch everything they'll need
// during compilation by grabbing a list of our own referenced
// assemblies.
using Topshelf;
using Filter.Platform.Common.Util;
using Microsoft.Win32.TaskScheduler;
using CloudVeilService.Util;
using CloudVeil.Core.Windows.Services;


namespace CloudVeilService.Services
{
    internal class ServiceSpawner
    {
        // This entire class is a dummy to force topshelf assemblies
        // to be linked to this assembly. See notes above the topshelf
        // using statement.
        private class TopshelfDummy
        {
            private Topshelf.Credentials dummy;
            private TopshelfDummy()
            {

            }
        }

        private class Governor : BaseProtectiveService
        {
            private Logger logger;

            public Governor() : base("warden", true)
            {
                logger = LoggerUtil.GetAppWideLogger();    
            }

            public override void Shutdown(ExitCodes code)
            {
                // Do nothing.
            }
        }

        private Logger logger;

        private Governor governor;

        private static ServiceSpawner s_instance;

        public static ServiceSpawner Instance
        {
            get
            {
                if(s_instance == null)
                {
                    s_instance = new ServiceSpawner();
                }

                return s_instance;
            }
        }

        private const string serviceCheckTaskName = "CloudVeilCheck";

        private ServiceSpawner()
        {
            // Compile monitoring services.
            var createdServices = new List<string>();
            try
            {
                logger = LoggerUtil.GetAppWideLogger();

                var thisAppDir = AppDomain.CurrentDomain.BaseDirectory;

                foreach(var name in this.GetType().Assembly.GetManifestResourceNames())
                {
                    // Compile everything except our base protective service.
                    if(name.IndexOf("Services", StringComparison.OrdinalIgnoreCase) != -1)
                    {
                        var serviceName = name.Substring(0, name.LastIndexOf('.'));
                        serviceName = serviceName.Substring(serviceName.LastIndexOf('.') + 1);

                        string fileName = $"{thisAppDir}{serviceName}.exe";

                        try
                        {
                            var res = CompileExe(name, fileName);

                            if(res != null && res.Length > 0)
                            {
                                createdServices.Add(res);
                            }
                        }
                        catch(IOException e)
                        {
                            try
                            {
                                List<Process> lockingProcesses = FileLockingUtil.WhoIsLocking(fileName);

                                if (lockingProcesses.Count == 0)
                                {
                                    logger.Warn("IOException occurred, but no locking processes detected.");
                                    LoggerUtil.RecursivelyLogException(logger, e);
                                }
                                else
                                {
                                    StringBuilder messageBuilder = new StringBuilder();
                                    messageBuilder.AppendLine($"Could not compile protective service because it was already opened by {(lockingProcesses.Count > 1 ? "other processes" : "another process")}");
                                    foreach(Process process in lockingProcesses)
                                    {
                                        messageBuilder.AppendLine($"\tProcess {process.Id}: {process.ProcessName}");
                                    }

                                    logger.Warn(messageBuilder.ToString());
                                }
                            }
                            catch(Exception fileException)
                            {
                                logger.Warn("Exception occurred while finding process which locked {0}", fileName);
                                LoggerUtil.RecursivelyLogException(logger, fileException);
                                LoggerUtil.RecursivelyLogException(logger, e);
                            }
                        }
                        catch(Exception e)
                        {
                            if(logger != null)
                            {
                                LoggerUtil.RecursivelyLogException(logger, e);
                            }
                        }
                    }
                }
            }
            catch(Exception e)
            {
                if(logger != null)
                {
                    LoggerUtil.RecursivelyLogException(logger, e);
                }
            }

            try
            {
                // Start the governor. This will run a background thread
                // that will ensure our chain of protective processes are
                // run.
                governor = new Governor();
            }
            catch(Exception e)
            {
                if(logger != null)
                {
                    LoggerUtil.RecursivelyLogException(logger, e);
                }
            }

            try
            {
                using (TaskService service = new TaskService())
                {
                    Win32Task task = service.GetTask(serviceCheckTaskName);
                    if(task != null)
                    {
                        service.RootFolder.DeleteTask(serviceCheckTaskName);
                    }

                    TaskDefinition def = service.NewTask();
                    def.RegistrationInfo.Description = "Ensures that CloudVeil is running";
                    def.Principal.LogonType = TaskLogonType.ServiceAccount;

                    var thisAppDir = AppDomain.CurrentDomain.BaseDirectory;
                    ExecAction action = new ExecAction(string.Format("{0}{1}.exe", thisAppDir, "FilterAgent.Windows.exe"), "start");

                    def.Actions.Add(action);

                    LogonTrigger trigger = (LogonTrigger)def.Triggers.Add(new LogonTrigger());
                    trigger.Delay = new TimeSpan(0, 2, 0);

                    service.RootFolder.RegisterTaskDefinition(serviceCheckTaskName, def);
                }
            }
            catch(Exception e)
            {
                if(logger != null)
                {
                    LoggerUtil.RecursivelyLogException(logger, e);
                }
            }
        }

        public void InitializeServices()
        {
            // Do nothing. At this point the static ctor should be
            // called and this will just ensure that.
        }

        /// <summary>
        /// Compiles an embedded resource into an executable and writes it to disk in the same
        /// directory as the executing assembly.
        /// </summary>
        /// <param name="sourceResourcePath">
        /// The relative resource path, as it must be formatted for a pack URI.
        /// </param>
        /// <param name="absOutputPath">
        /// The absolute path 
        /// </param>
        private string CompileExe(string sourceResourcePath, string absOutputPath)
        {
            logger.Info("Compiling internal service: {0} to output {1}.", sourceResourcePath, absOutputPath);
            string scriptContents = string.Empty;
            
            using(var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(sourceResourcePath))
            {
                using(TextReader tsr = new StreamReader(resourceStream))
                {
                    scriptContents = tsr.ReadToEnd();
                }
            }

            if(scriptContents == null || scriptContents.Length == 0)
            {
                logger.Warn("When compiling internal service {0}, failed to load source code.", sourceResourcePath);
                return string.Empty;
            }

            // The sentinel service is special. It's the only that's going to watch us.
            // So, we need to code our process name into its source before compilation.
            if(sourceResourcePath.IndexOf("Sentinel", StringComparison.OrdinalIgnoreCase) != -1)
            {
                scriptContents = scriptContents.Replace("TARGET_APPLICATION_NAME", Process.GetCurrentProcess().ProcessName);
            }

            HashSet<string> allRefs = new HashSet<string>();

            var dd = typeof(Enumerable).GetTypeInfo().Assembly.Location;
            var coreDir = Directory.GetParent(dd);

            List<MetadataReference> references = new List<MetadataReference>
            {   
                // Here we get the path to the mscorlib and private mscorlib
                // libraries that are required for compilation to succeed.
                //MetadataReference.CreateFromFile(coreDir.FullName + Path.DirectorySeparatorChar + "mscorlib.dll"),
                MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location)
            };

            var referencedAssemblies = RecursivelyGetReferencedAssemblies(Assembly.GetEntryAssembly());

            foreach(var referencedAssembly in referencedAssemblies)
            {
                var mref = MetadataReference.CreateFromFile(referencedAssembly.Location);

                if(referencedAssembly.FullName.Contains("System.Runtime.Extension"))
                {
                    // Have to do this to avoid collisions with duplicate type
                    // definitions between private mscorlib and this assembly.
                    // XXX TODO - Needs to be solved in a better way?
                    mref = mref.WithAliases(new List<string>(new[] { "CorPrivate" }));
                }

                if(!allRefs.Contains(mref.Display))
                {
                    references.Add(mref);
                    allRefs.Add(mref.Display);
                }
            }

            // Setup syntax parse options for C#.
            CSharpParseOptions parseOptions = CSharpParseOptions.Default;
            parseOptions = parseOptions.WithLanguageVersion(LanguageVersion.CSharp6);
            parseOptions = parseOptions.WithDocumentationMode(DocumentationMode.None);
            parseOptions = parseOptions.WithKind(SourceCodeKind.Regular);

            // Parse text into syntax tree.
            SyntaxTree jobSyntaxTree = CSharpSyntaxTree.ParseText(scriptContents, parseOptions);

            // Initialize compilation arguments for the build script we're about
            // to compile.
            var op = new CSharpCompilationOptions(OutputKind.ConsoleApplication);
            op = op.WithAssemblyIdentityComparer(DesktopAssemblyIdentityComparer.Default);
            op = op.WithGeneralDiagnosticOption(ReportDiagnostic.Warn);

            // Initialize the compilation with our options, references and the
            // already parsed syntax tree of the build script.
            CSharpCompilation compilation = CSharpCompilation.Create(
                Path.GetFileNameWithoutExtension(absOutputPath),
                syntaxTrees: new[] { jobSyntaxTree },
                references: references,
                options: op);

            // Compile and emit new assembly into memory.
            using(var ms = new MemoryStream())
            {
                EmitResult result = compilation.Emit(ms);

                if(result.Success)
                {
                    File.WriteAllBytes(absOutputPath, ms.ToArray());
                    logger.Info("Generated service assembly {0} for service {1}.", absOutputPath, Path.GetFileNameWithoutExtension(absOutputPath));
                    return absOutputPath;
                }
                else
                {
                    // Compilation failed.
                    logger.Error("Failed to generate service assembly for service {0}.", Path.GetFileNameWithoutExtension(absOutputPath));

                    foreach(var diag in result.Diagnostics)
                    {   
                        logger.Error(diag.GetMessage());
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Gets every assembly that the supplied assembly connects to, directly
        /// and indirectly.
        /// </summary>
        /// <param name="assembly">
        /// The assembly from which to pull all references.
        /// </param>
        /// <param name="collected">
        /// An optional dictionary to collect all unique references into. If not
        /// supplied, will be created. Only used to ensure a final, unique list.
        /// </param>
        /// <returns>
        /// A unique list containing all recursively referenced assemblies.
        /// </returns>
        private static List<Assembly> RecursivelyGetReferencedAssemblies(Assembly assembly, Dictionary<string, Assembly> collected = null)
        {
            if(collected == null)
            {
                collected = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
            }

            if(!collected.ContainsKey(assembly.FullName))
            {
                collected.Add(assembly.FullName, assembly);
            }

            var reffed = assembly.GetReferencedAssemblies();

            foreach(var entry in reffed)
            {
                try
                {
                    var loaded = Assembly.Load(entry);

                    if(collected.ContainsKey(loaded.FullName))
                    {
                        continue;
                    }

                    collected.Add(loaded.FullName, loaded);

                    var subRes = RecursivelyGetReferencedAssemblies(loaded, collected);

                    foreach(var subEntry in subRes)
                    {
                        if(!collected.ContainsKey(subEntry.FullName))
                        {
                            collected.Add(subEntry.FullName, subEntry);
                        }
                    }
                }
                catch
                {
                   
                }
            }

            return collected.Values.ToList();
        }
    }
}
