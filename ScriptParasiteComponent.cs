using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Grasshopper.GUI.Script;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Parameters.Hints;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;
using Rhino;
using ScriptComponents;

namespace StudioAvw.Gh.Parasites
{
    public class ScriptParasiteComponent : SafeComponent
    {
        /// <inheritdoc />
        public ScriptParasiteComponent() : base("Script Parasite", "ScriptPar",
            "Allow C# scripts to edited in an external editor. Will recompile when the exported file changes.", "Math", "Scripts")
        {
            Message = "Disabled";
        }

        protected override Bitmap Icon => Resources.ScriptParasiteIcon;
        
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected string DefaultOutputFolder
        {
            get
            {
                if (_defaultFolder == null)
                {
                    // check if we saved before..
                    _defaultFolder = ReadDefaultFolder();
                }

                if (_defaultFolder == null)
                {
                    // otherwise, create a default.
                    _defaultFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GrasshopperScripts");
                }
                return _defaultFolder;
            }
        }

        protected string Folder { get; set; }

        public override string Description => $"Allow C# scripts to edited in an external editor. Will recompile when the exported file changes, and will write parameter changes back to the C# script.\nCurrent output path: {DefaultOutputFolder}";

        protected override string HelpDescription => $"Saved script output path: {DefaultOutputFolder}";

        protected string FileNameSafe
        {
            get
            {
                var name = Regex.Replace(TargetComponent.NickName, @"\W", "_");
                var componentId = TargetComponent.InstanceGuid.ToString().Replace(" - ", "").Substring(0, 5);
                return Path.Combine($"{Folder}",$"{name}-{componentId}.cs");
            }
        }

        protected Component_CSNET_Script TargetComponent { get; set; }

        public override Guid ComponentGuid => new Guid("{8FCBDF63-7922-4B7D-9C9D-17E7F58AE23A}");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            var defaultFolder = ReadDefaultFolder();

            var enableParam = pManager.AddBooleanParameter("Enable", "E", "Enable listening for changes for the current file?",
                GH_ParamAccess.tree, false);
            pManager[enableParam].Optional = true;

            var folderParam = pManager.AddTextParameter("Folder", "F", "Folder to write scripts to",
                GH_ParamAccess.tree, defaultFolder);
            pManager[folderParam].Optional = true;
        }

        public static string SettingsFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GhScriptParasite", "DefaultFolder.txt");

        private static string ReadDefaultFolder()
        {
            return File.Exists(SettingsFile) ? File.ReadAllText(SettingsFile) : null;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
        }

        private int _iteration;
        private string _defaultFolder;

        protected override void BeforeSolveInstance()
        {
            _iteration = 0;
        }

        protected override void SolveInstance(IGH_DataAccess da)
        {
            // reset everything, and let's start over..
            Cleanup();
            // things are botched up if this script runs more than once.
            if (_iteration++ != 0) return;
            if (!da.GetDataTree<GH_Boolean>(0, out var tree)) return;
            if (!da.GetDataTree<GH_String>(1, out var stringTree)) return;

            var folder = stringTree.AllData(true).OfType<GH_String>().Select(x => x.Value).FirstOrDefault();
            if (folder == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Scripts Folder was not set");
                return;
            }

            folder = folder.Trim();

            if (!TryGetDirectoryVerbose(folder)) return;

            var allBooleans = tree.AllData(true).Select(c => c.ScriptVariable()).Cast<bool>().ToList();
            var success = allBooleans.Count == 1 && allBooleans[0];

            if (!success)
            {
                return;
            }

            // Find the scripts in the current group.
            var scripts = FindObjectsOfTypeInCurrentGroup<Component_CSNET_Script>();

            if (scripts.Count != 1)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "This component should be added in a group with exactly one C# script.");
                return;
            }

            if (IsBusy)
            {
                return;
            }

            TargetComponent = scripts[0];


            Folder = folder;

            string directory;
            string filename;
            try
            {
                directory = Path.GetDirectoryName(FileNameSafe);
                filename = Path.GetFileName(FileNameSafe);
                if (directory == null || filename == null)
                {
                    throw new Exception("No file name found");
                }
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Invalid project name or component name, error {ex.Message}");
                return;
            }


            if (!WriteScriptToFile(TargetComponent, FileNameSafe)) return;
            
            // Create a new FileSystemWatcher and set its properties.
            // http://stackoverflow.com/questions/721714/notification-when-a-file-changes
            AddEvents(directory, filename);

            WriteDefaultConfig(directory);

            EnsureProject(Path.Combine(directory, "GrasshopperScripts.csproj"));
        }

        /// <summary>
        /// Write the "config" file (just a txt file), with the last successfully used setting.
        /// </summary>
        /// <param name="directory"></param>
        private void WriteDefaultConfig(string directory)
        {
            if (!TryGetFolder(Path.GetDirectoryName(SettingsFile)))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Could not write default folder settings to file {SettingsFile}");
            }
            File.WriteAllText(SettingsFile, directory);
            _defaultFolder = directory;
        }

        /// <summary>
        /// Create a directory, and add friendly error messages why it's not possible, if it's not possible.
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        protected bool TryGetDirectoryVerbose(string folder)
        {
            if (Directory.Exists(folder)) return true;

            try
            {
                Directory.CreateDirectory(folder);
            }
            catch (UnauthorizedAccessException ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Could not create folder '{folder}', permission denied: {ex.Message}");
                return false;
            }
            catch (ArgumentException)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Could not create folder '{folder}', path contains invalid characters");
                return false;
            }
            catch (PathTooLongException)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Could not create folder '{folder}', path is too long");
                return false;
            }
            catch (DirectoryNotFoundException ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Could not find drive for folder '{folder}', error: '{ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Cannot create folder '{folder}', error: '{ex.Message}");
                return false;
            }

            return true;
        }

        protected static void WriteScriptToComponent(Component_CSNET_Script scriptObject, string filename)
        {
            var usingCodePropertyInfo = scriptObject.ScriptSource.GetType().GetProperty("UsingCode");
            var fileContent = ReadFile(filename);
            var runscript = GetRegion(fileContent, "Runscript");
            var rsLines = runscript.Split(new[] {Environment.NewLine}, StringSplitOptions.None);
            runscript = string.Join(Environment.NewLine, rsLines.Skip(2).Take(rsLines.Length - 3).ToList());
            scriptObject.SourceCodeChanged(new GH_ScriptEditor(GH_ScriptLanguage.CS));
            var additional = GetRegion(fileContent, "Additional");
            scriptObject.ScriptSource.ScriptCode = runscript;
            scriptObject.ScriptSource.AdditionalCode = additional;
            if (usingCodePropertyInfo != null)
            {
                usingCodePropertyInfo.SetValue(scriptObject.ScriptSource, GetUsing(fileContent), null);
            }
        }

        private static string GetUsing(string fileContent)
        {
            List<string> allMatches = GetUsingList(fileContent);

            var output = "";
            foreach (var item in allMatches)
            {
                output = output + $"using {item};\r\n";
            }
            return output;
        }

        private static List<string> GetUsingList(string fileContent)
        {
            // remove the default types if they're added...
            var defaults = new[]
            {
                "System", "System.Collections", "System.Collections.Generic",
                "Rhino", "Rhino.Geometry", "Grasshopper", "Grasshopper.Kernel",
                "Grasshopper.Kernel.Data", "Grasshopper.Kernel.Types",
            }.ToList();
            // \s([0-9a-zA-Z\._]+);$
            return Regex.Matches(fileContent, @"using\s+([0-9a-zA-Z\._=<>\ ]+);")
                .OfType<Match>()
                .Select(m => m.Groups[1].Value)
                .Where(v => !defaults.Contains(v)).ToList();
        }

        public FileSystemWatcher Watcher { get; set; }

        public void AddEvents(string directory, string filename)
        {
            if (Watcher != null)
            {
                CleanUpEvents();
            }

            if (TargetComponent == null)
            {
                return;
            }

            TargetComponent.AttributesChanged += CurrentTargetOnAttributesChanged;
            TargetComponent.ObjectChanged += CurrentTargetOnChanged;

            foreach (var item in TargetComponent.Params)
            {
                item.ObjectChanged -= CurrentTargetOnParamChanged;
                item.ObjectChanged += CurrentTargetOnParamChanged;
            }

            Watcher = new FileSystemWatcher(directory, filename)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime
            };

            Watcher.Changed += OnFileChanged;
            Watcher.Deleted += OnFileChanged;
            Watcher.EnableRaisingEvents = true;
            Message = "Watching";
        }

        private void CurrentTargetOnParamChanged(IGH_DocumentObject sender, GH_ObjectChangedEventArgs e)
        {
            var componentSender = sender.Attributes.GetTopLevel.DocObject;
            CheckAndUpdateExport(componentSender);
        }

        public override void CleanUpEvents()
        {
            if (TargetComponent != null)
            {
                TargetComponent.AttributesChanged -= CurrentTargetOnAttributesChanged;
                TargetComponent.ObjectChanged -= CurrentTargetOnChanged;

                foreach (var item in TargetComponent.Params)
                {
                    item.ObjectChanged -= CurrentTargetOnParamChanged;
                }
            }

            TargetComponent = null;

            if (Watcher == null)
                return;
            Watcher.Changed -= OnFileChanged;
            Watcher.Deleted -= OnFileChanged;
            Watcher.Dispose();
            Watcher = null;
            Message = "Disabled";
        }

        /// <summary>
        /// Check for unhealthy situations where we would not like to continue
        /// If the component is locked, or the solution is locked
        /// </summary>
        /// <returns></returns>
        protected override bool ShouldContinue()
        {
            return base.ShouldContinue() &&
                   TargetComponent != null &&
                   TargetComponent.OnPingDocument() == OnPingDocument();
        }


        protected void CurrentTargetOnAttributesChanged(IGH_DocumentObject sender, GH_AttributesChangedEventArgs e)
        {
            CheckAndUpdateExport(sender);
        }

        protected void CurrentTargetOnChanged(IGH_DocumentObject sender, GH_ObjectChangedEventArgs e)
        {
            CheckAndUpdateExport(sender);
        }

        private void CheckAndUpdateExport(IGH_DocumentObject sender)
        {
            if (!ShouldContinue() || sender != TargetComponent)
            {
                Cleanup();
                return;
            }

            if (IsBusy)
                return;

            IsBusy = true;
            OnPingDocument().SolutionEnd += UpdateFileCallback;
        }

        private void UpdateFileCallback(object sender, GH_SolutionEventArgs e)
        {
            if (!ShouldContinue()) return;
            if (!(sender is GH_Document doc)) return;
            doc.SolutionEnd -= UpdateFileCallback;
            ExpireSolution(false);
            OnPingDocument().ScheduleSolution(100);
            IsBusy = false;
        }

        public void OnFileChanged(Object sender, FileSystemEventArgs e)
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                if (!ShouldContinue())
                {
                    Cleanup();
                    return;
                }

                // Visual studio will rename a file to a temp file,
                // Then write new contents to the original file
                // So here we check if a file that stopped existing, starts existing again..
                if (!File.Exists(FileNameSafe))
                {
                    Thread.Sleep(200);
                    if (!File.Exists(FileNameSafe))
                    {
                        Cleanup();
                        return;
                    }
                }

                // Modify the script at the end of the solution, rather than now
                // so we know for sure that nothing is in the middle of a 
                // solution, and could botch up things.
                OnPingDocument().SolutionEnd += WriteCodeToComponent;

                OnPingDocument().ScheduleSolution(10, doc =>
                {
                    // expire the script in the next solution, so it will recompute.
                    TargetComponent.ExpireSolution(false);
                });
            }
            catch (Exception exp)
            {
                RhinoApp.WriteLine($"Error in filehandler:{exp.Message}\\n{exp.StackTrace}");
            }
        }

        private void WriteCodeToComponent(object sender, GH_SolutionEventArgs e)
        {
            if (!ShouldContinue()) return;
            if (!(sender is GH_Document doc)) return;

            doc.SolutionEnd -= WriteCodeToComponent;
            WriteScriptToComponent(TargetComponent, FileNameSafe);
            TargetComponent.ExpireSolution(true);
            IsBusy = false;
        }

        /// <summary>
        /// Happens if the file handler is busy
        /// </summary>
        public bool IsBusy { get; set; }

        /// <summary>
        /// Remove all events and status. Make sure there are no
        /// events left attached after this component is deleted, disabled
        /// </summary>
        public override void Cleanup()
        {
            CleanUpEvents();
            IsBusy = false;
        }


        /// <summary>
        /// Write script to file using a template
        /// This is called from the UI thread / solve instance.
        /// </summary>
        /// <param name="script"></param>
        /// <param name="filename"></param>
        private bool WriteScriptToFile(Component_CSNET_Script script, string filename)
        {
            var methodArguments = ExtractScriptParameters(script);
            var output = new ScriptOutput
            {
                InputOutput = methodArguments,
                UniqueNamespace = "ns" + script.InstanceGuid.ToString().Replace("-", "").Substring(0, 5),
                // Regex to fix the padding to match 4 tabs.
                // somehow all code is still botched up. Not sure if I should use some other way to format code.
                SourceCode = Regex.Replace(script.ScriptSource.ScriptCode, @"^( {4})( *)", @"            $2$2",
                    RegexOptions.Multiline | RegexOptions.IgnoreCase),
                AdditionalCode = Regex.Replace(script.ScriptSource.AdditionalCode, @"^( {2})( *)", @"        $2$2",
                    RegexOptions.Multiline | RegexOptions.IgnoreCase),
            };

            var usingCode = script.ScriptSource.GetType()
                .GetProperty("UsingCode")?
                .GetValue(script.ScriptSource);
            if (usingCode is string code)
            {
                output.CustomUsings = GetUsingList(code);
            }
            var text = output.TransformText();
            try
            {
                File.WriteAllText(filename, text);
                return true;
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Could not write script to file {filename}, error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Extract the parameters of a script
        /// </summary>
        /// <param name="script"></param>
        /// <returns></returns>
        private string ExtractScriptParameters(Component_CSNET_Script script)
        {
            var elements = new List<string>();
            var map = BuildMap();

            // todo: Add usings to script for RH6 support.
            foreach (var ghParam in script.Params.Input.OfType<Param_ScriptVariable>())
            {
                var th = ghParam.TypeHint ?? new GH_NullHint();
                var key = th.ToString().Replace("Grasshopper.Kernel.Parameters.Hints.", "");
                var objectType = map[key][2];
                var itemString = "";
                var cleanNickname = CleanNickname(ghParam.NickName);
                switch (ghParam.Access)
                {
                    case GH_ParamAccess.tree:
                        itemString = $"DataTree<{objectType}> {cleanNickname}";
                        break;
                    case GH_ParamAccess.list:
                        itemString = $"List<{objectType}> {cleanNickname}";
                        break;
                    case GH_ParamAccess.item:
                        itemString = $"{objectType} {cleanNickname}";
                        break;
                }

                elements.Add(itemString);
            }

            foreach (var pso in script.Params.Output)
            {
                if (pso is Param_GenericObject)
                {
                    elements.Add($"ref object {CleanNickname(pso.NickName)}");
                }
            }

            var methodArguments = string.Join(", ", elements);
            return methodArguments;
        }

        private static string CleanNickname(string input)
        {
            var result = Regex.Match(input, @"\((.*)\)");
            if (result.Success)
            {
                return result.Groups[1].Value;
            }

            return input;
        }

        /// <summary>
        /// Read a file and return its contents as a string.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        static string ReadFile(string filename)
        {
            var stringBuilder = new StringBuilder();
            var fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using (var streamReader = new StreamReader(fileStream))
            {
                string line;
                while ((line = streamReader.ReadLine()) != null)
                {
                    stringBuilder.AppendLine(line);
                }
            }

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Support function for the BuildMap methods.
        /// </summary>
        /// <param name="map"></param>
        /// <param name="hint"></param>
        /// <param name="method"></param>
        /// <param name="ghgoo"></param>
        /// <param name="native"></param>
        /// <param name="nspace"></param>
        void AddMethod(Dictionary<string, string[]> map, string hint, string method, string ghgoo, string native,
            string nspace)
        {
            map.Add(hint, new[] {method, ghgoo, native, nspace});
        }

        /// <summary>
        ///  Build a map of typehints.
        /// </summary>
        /// <returns>A dictionary of how to convert certain GH types to native types, to script types.</returns>
        public Dictionary<string, string[]> BuildMap()
        {
            var map = new Dictionary<string, string[]>();
            AddMethod(map, "GH_NullHint", "AddGenericParameter ", "GH_ObjectWrapper", "object", "System");
            AddMethod(map, "GH_BooleanHint_CS", "AddBooleanParameter", "GH_Boolean", "bool", "System");
            AddMethod(map, "GH_IntegerHint_CS", "AddIntegerParameter", "GH_Integer", "int", "System");
            AddMethod(map, "GH_DoubleHint_CS", "AddNumberParameter", "GH_Number", "double", "System");
            AddMethod(map, "GH_ComplexHint", "AddComplexParameter", "GH_ComplexNumber", "Complex",
                "Grasshopper.Kernel.Types");
            AddMethod(map, "GH_StringHint_CS", "AddTextParameter", "GH_String", "string", "System");
            AddMethod(map, "GH_DateTimeHint", "AddDateTimeParameters", "GH_Time", "DateTime", "System");
            AddMethod(map, "GH_ColorHint", "AddColorParameter", "GH_Colour", "Color", "System.Drawing");
            AddMethod(map, "GH_GuidHint", "AddGuidParameter", "GH_Guid", "Guid", "System");
            AddMethod(map, "GH_Point3dHint", "AddPointParameter", "GH_Point", "Point3d", "Rhino.Geometry");
            AddMethod(map, "GH_Vector3dHint", "AddVector3dParameter", "GH_Vector", "Vector3d", "Rhino.Geometry");
            AddMethod(map, "GH_PlaneHint", "AddPlaneParameter", "GH_Plane", "Plane", "Rhino.Geometry");
            AddMethod(map, "GH_IntervalHint", "AddIntervalParameter", "GH_Interval", "Interval", "Rhino.Geometry");
            AddMethod(map, "GH_UVIntervalHint", "AddInterval2DParameter", "GH_Interval2d", "UVInterval",
                "Grasshopper.Kernel.Types");
            AddMethod(map, "GH_Rectangle3dHint", "AddRectangle3dParameter", "GH_Rectangle", "Rectangle3d",
                "Rhino.Geometry");
            AddMethod(map, "GH_BoxHint", "AddBoxParameter", "GH_Box", "Box", "Rhino.Geometry");
            AddMethod(map, "GH_TransformHint", "AddTramsformParameter", "GH_Transform", "Transform", "Rhino.Geometry");
            AddMethod(map, "GH_LineHint", "AddLineParameter", "GH_Line", "Line", "Rhino.Geometry");
            AddMethod(map, "GH_CircleHint", "AddCircleParameter", "GH_Circle", "Circle", "Rhino.Geometry");
            AddMethod(map, "GH_ArcHint", "AddArcParameter", "GH_Arc", "Arc", "Rhino.Geometry");
            AddMethod(map, "GH_PolylineHint", "AddPolylineParameter", "GH_Polyline", "Polyline", "Rhino.Geometry");
            AddMethod(map, "GH_CurveHint", "AddCurveParameter", "GH_Curve", "Curve", "Rhino.Geometry");
            AddMethod(map, "GH_SurfaceHint", "AddSurfaceParameter", "GH_Surface", "Surface", "Rhino.Geometry");
            AddMethod(map, "GH_BrepHint", "AddBrepParameter", "GH_Brep", "Brep", "Rhino.Geometry");
            AddMethod(map, "GH_MeshHint", "AddMeshParameter", "GH_Mesh", "Mesh", "Rhino.Geometry");
            AddMethod(map, "GH_GeometryBaseHint", "AddGeometryParameter", "IGH_GeometricGoo", "GeometryBase",
                "Rhino.Geometry");
            return map;
        }

        /// <summary>
        /// Replace C# regions inside a template
        /// </summary>
        /// <param name="source"></param>
        /// <param name="regionName"></param>
        /// <returns></returns>
        public static string GetRegion(string source, string regionName)
        {
            var regex = @"#region |name|\s+(.*?)\s+#endregion".Replace("|name|", regionName);
            var match = Regex.Match(source, regex, RegexOptions.Singleline);
            return match.Groups.Count >= 2 ? match.Groups[1].ToString() : "";
        }

        /// <summary>
        /// Find all objects of type T in any group this current script instance is a member of.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>A list of objects of type T that belong to group T</returns>
        private List<T> FindObjectsOfTypeInCurrentGroup<T>() where T : IGH_ActiveObject
        {
            // find all groups that this object is in.
            var groups = OnPingDocument()
                .Objects
                .OfType<GH_Group>()
                .Where(gr => gr.ObjectIDs.Contains(InstanceGuid))
                .ToList();

            // find in the groups that this object is in all objects of type T.
            var output = groups.Aggregate(new List<T>(), (list, item) =>
            {
                list.AddRange(
                    OnPingDocument().Objects.OfType<T>()
                        .Where(obj => item.ObjectIDs.Contains(obj.InstanceGuid))
                );
                return list;
            }).Distinct().ToList();

            return output;
        }

        protected static bool TryGetFolder(string folder)
        {
            if (Directory.Exists(folder)) return true;
            try
            {
                Directory.CreateDirectory(folder);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        public void EnsureProject(string file)
        {
            var dir = Path.GetDirectoryName(file);

            if (dir == null || !TryGetFolder(dir)) return;

            var hasCsproj = Directory.GetFiles(dir).Any(s => s.ToLowerInvariant().EndsWith(".csproj"));

            if (hasCsproj) return;

            var output = new ProjectOutput
            {
                RhinoCommonPath = System.Reflection.Assembly.GetAssembly(typeof(RhinoApp)).Location,
                GrasshopperPath = System.Reflection.Assembly.GetAssembly(typeof(GH_Component)).Location,
                GrasshopperIoPath = System.Reflection.Assembly.GetAssembly(typeof(GH_IO.GH_ISerializable)).Location
            };
            File.WriteAllText(file, output.TransformText());
        }
    }
}