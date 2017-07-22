/*
* Copyright © 2017 Jesse Nicholson  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Microsoft.CodeAnalysis;
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
using Te.Citadel.Util;
using System.Diagnostics;

// Although we don't actually use Topshelf internally, we need
// to have this using statement present, so that topshelf
// assemblies are linked to us. We want all assemblies that the 
// embedded processes (which need to be compiled) require also
// linked to us, so we can easily fetch everything they'll need
// during compilation by grabbing a list of our own referenced
// assemblies.
using Topshelf;

namespace Te.Citadel.Services
{
    internal class ServiceSpawner
    {
        // This entire class is a dummy to force topshelf assemblies
        // to be linked to this assembly. See notes above the topshelf
        // using statement.
        private class TopshelfDummy
        {
            private Topshelf.Credentials m_dummy;
            private TopshelfDummy()
            {

            }
        }

        private class Governor : BaseProtectiveService
        {
            private Logger m_logger;

            public Governor() : base("warden", true)
            {
                m_logger = LoggerUtil.GetAppWideLogger();    
            }

            public override void Shutdown()
            {
                // Do nothing.
            }
        }

        private Logger m_logger;

        private Governor m_governor;

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

        private ServiceSpawner()
        {
            // Compile monitoring services.
            var createdServices = new List<string>();
            try
            {
                m_logger = LoggerUtil.GetAppWideLogger();

                var thisAppDir = AppDomain.CurrentDomain.BaseDirectory;

                var asm = Assembly.GetEntryAssembly();
                string resName = asm.GetName().Name + ".g.resources";
                using(var stream = asm.GetManifestResourceStream(resName))
                using(var reader = new System.Resources.ResourceReader(stream))
                {
                    var resNames = reader.Cast<DictionaryEntry>().Select(entry => (string)entry.Key).ToArray();

                    foreach(var name in resNames)
                    {
                        // Compile everything except our base protective service.
                        if(name.IndexOf("Te/Citadel/Services", StringComparison.OrdinalIgnoreCase) != -1)
                        {   
                            try
                            {
                                var res = CompileExe(name, string.Format("{0}{1}.exe", thisAppDir, Path.GetFileNameWithoutExtension(name)));

                                if(res != null && res.Length > 0)
                                {
                                    createdServices.Add(res);
                                }
                            }
                            catch(Exception e)
                            {
                                if(m_logger != null)
                                {
                                    LoggerUtil.RecursivelyLogException(m_logger, e);
                                }
                            }
                        }
                    }
                }
            }
            catch(Exception e)
            {
                if(m_logger != null)
                {
                    LoggerUtil.RecursivelyLogException(m_logger, e);
                }
            }

            try
            {
                // Start the governor. This will run a background thread
                // that will ensure our chain of protective processes are
                // run.
                m_governor = new Governor();
            }
            catch(Exception e)
            {
                if(m_logger != null)
                {
                    LoggerUtil.RecursivelyLogException(m_logger, e);
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
            var iconPackUri = new Uri(string.Format("pack://application:,,,/{0}", sourceResourcePath));
            var resourceStream = Application.GetResourceStream(iconPackUri);

            string scriptContents = string.Empty;

            using(StreamReader reader = new StreamReader(resourceStream.Stream))
            {
                scriptContents = reader.ReadToEnd();
            }

            if(scriptContents == null || scriptContents.Length == 0)
            {
                m_logger.Warn("When compiling internal service {0}, failed to load source code.", sourceResourcePath);
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
                    m_logger.Error("Generated service assembly {0} for service {1}.", absOutputPath, Path.GetFileNameWithoutExtension(absOutputPath));
                    return absOutputPath;
                }
                else
                {
                    // Compilation failed.
                    m_logger.Error("Failed to generate service assembly for service {0}.", Path.GetFileNameWithoutExtension(absOutputPath));

                    foreach(var diag in result.Diagnostics)
                    {   
                        m_logger.Error(diag.GetMessage());
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

            return collected.Values.ToList();
        }
    }
}
