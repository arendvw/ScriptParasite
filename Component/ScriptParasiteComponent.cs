using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Grasshopper.GUI.Script;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Parameters.Hints;
using Grasshopper.Kernel.Special;
using Rhino;
using ScriptComponents;
using StudioAvw.Gh.Parasites.Helper;
using StudioAvw.Gh.Parasites.Template;
using StudioAvw.Gh.Parasites.Watcher;

namespace StudioAvw.Gh.Parasites.Component
{
    public class ScriptParasiteComponent : SafeComponent
    {
        /// <inheritdoc />
        public ScriptParasiteComponent() : base("Script Parasite", "ScriptPar",
            "Allow C# scripts to be edited in an external editor. Will recompile when the exported file changes.", "Math", "Scripts")
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

        public override string Description => $"Allow C# scripts to be edited in an external editor. Will recompile when the exported file changes, and will write parameter and code changes back to the C# script automatically." +
                                          $"\n\nCurrent output path: {DefaultOutputFolder}";

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
        protected string FileNameOnly
        {
            get
            {
                var name = Regex.Replace(TargetComponent.NickName, @"\W", "_");
                var componentId = TargetComponent.InstanceGuid.ToString().Replace(" - ", "").Substring(0, 5);
                return $"{name}-{componentId}.cs";
            }
        }

        protected Component_CSNET_Script TargetComponent { get; set; }

        public override Guid ComponentGuid => new Guid("{8FCBDF63-7922-4B7D-9C9D-17E7F58AE23A}");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            var defaultFolder = ReadDefaultFolder();

            pManager.AddBooleanParameter("Enable", "E", "Enable listening for changes for the current file?", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Folder", "F", "Folder to write scripts to", GH_ParamAccess.item, defaultFolder);
        }

        public static string SettingsFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GhScriptParasite", "DefaultFolder.txt");

        private static string ReadDefaultFolder()
        {
            return File.Exists(SettingsFile) ? File.ReadAllText(SettingsFile) : null;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("File", "F", "The file that corresponds to the scrip being monitored", GH_ParamAccess.item);
        }

        private int _iteration;
        private string _defaultFolder;

        protected override void BeforeSolveInstance()
        {
            _iteration = 0;
        }

        protected override void SolveInstance(IGH_DataAccess da)
        {
            try
            {
                // reset everything, and let's start over..
                Cleanup();
                // things are botched up if this script runs more than once.
                // events might be added more then once if this component somehow has an input of multiple items.
                if (_iteration++ != 0) return;

                bool watch = false;
                string folder = "";

                if (!da.GetData(0, ref watch)) return;
                if (!da.GetData(1, ref folder)) return;

                if (folder == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Scripts Folder was not set");
                    return;
                }

                folder = folder.Trim();

                if (!TryGetDirectoryVerbose(folder)) return;

                if (!watch)
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
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                        $"Invalid project name or component name, error {ex.Message}");
                    return;
                }

                if (!WriteScriptToFile(TargetComponent, FileNameSafe)) return;

                // Create a new FileSystemWatcher and set its properties.
                // http://stackoverflow.com/questions/721714/notification-when-a-file-changes
                AddEvents(directory, filename);

                WriteDefaultConfig(directory);

                EnsureProject(Path.Combine(directory, "GrasshopperScripts.csproj"));
                EnsureEditorConfig(Path.Combine(directory, ".editorconfig"));

                //show which file is used for the selected script
                da.SetData(0, FileNameOnly);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Unexpected error: {ex.Message}, \nstacktrace: {ex.StackTrace}");
            }
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
            var fileContent = ReadFile(filename);
            var runscript = GetRegion(fileContent, "Runscript");
            var rsLines = runscript.Split(new[] {Environment.NewLine}, StringSplitOptions.None);
            runscript = string.Join(Environment.NewLine, rsLines.Skip(2).Take(rsLines.Length - 3).ToList());
            
            scriptObject.SourceCodeChanged(new GUI.Script.Cs.GH_ScriptEditor(GUI.Script.Cs.GH_ScriptLanguage.CS));
            var additional = GetRegion(fileContent, "Additional");
            scriptObject.ScriptSource.ScriptCode = runscript;
            scriptObject.ScriptSource.AdditionalCode = additional;
            scriptObject.ScriptSource.UsingCode = GetUsing(fileContent);
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

        public ScriptFilesystemWatcher Watcher { get; set; }

        public ScriptComponentWatcher ComponentWatcher { get; set; }

        public void AddEvents(string directory, string filename)
        {
            CleanUpEvents();

            if (TargetComponent == null)
            {
                return;
            }

            ComponentWatcher = new ScriptComponentWatcher(TargetComponent);
            ComponentWatcher.ScriptUpdated += ScriptUpdated;
            Watcher = new ScriptFilesystemWatcher(directory, filename);
            Watcher.FileUpdated += OnFileChanged;
            Message = "Watching";
        }

        private async void ScriptUpdated(object sender, EventArgs e)
        {
            await CheckAndUpdateExport();
        }

        public override void CleanUpEvents()
        {
            ComponentWatcher?.Dispose();
            ComponentWatcher = null;
            Watcher?.Dispose();
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

        public bool IsWritingToFile { get; set; }

        private async Task CheckAndUpdateExport()
        {
            if (!ShouldContinue())
            {
                Cleanup();
                return;
            }
            Watcher.IsWriting = true;
            WriteScriptToFile(TargetComponent, FileNameSafe);
            await Task.Delay(50);
            Watcher.IsWriting = false;
        }

        public async void OnFileChanged(object sender, EventArgs eventArgs)
        {
            if (!ShouldContinue())
            {
                Cleanup();
                return;
            }

            ComponentWatcher.IsUpdating = true;
            var ghWatcher = new GrasshopperDocumentWatcher(OnPingDocument());
            try
            {
                await ghWatcher.WaitForSolutionEnd(10000);
                Grasshopper.Instances.DocumentEditor?.BeginInvoke((Action) (async () =>
                {
                    WriteScriptToComponent(TargetComponent, FileNameSafe);
                    TargetComponent.ExpireSolution(false);
                    OnPingDocument().ScheduleSolution(10);
                    await Task.Delay(50);
                    ComponentWatcher.IsUpdating = false;
                }));
            }
            catch (TimeoutException)
            {
                // do nothing..
                // grasshopper is still busy after 100 seconds..
                // time to bail out.
            }
        }

        /// <summary>
        /// Remove all events and status. Make sure there are no
        /// events left attached after this component is deleted, disabled
        /// </summary>
        public override void Cleanup()
        {
            CleanUpEvents();
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
                UniqueNamespace = script.InstanceGuid.ToString().Replace("-", "").Substring(0, 5),
                SourceCode = script.ScriptSource.ScriptCode,
                Using = script.ScriptSource.UsingCode ?? "",
                AdditionalCode = script.ScriptSource.AdditionalCode,
            };

            try
            {
                var text = output.TransformText();
                File.WriteAllText(filename, text);
                return true;
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Could not write script to file {filename}, error: {ex.Message}, stacktrace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Extract the parameters of a script
        /// </summary>
        /// <param name="script"></param>
        /// <returns></returns>
        internal static string ExtractScriptParameters(Component_CSNET_Script script)
        {
            var elements = new List<string>();
            var map = BuildMap();

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
            return result.Success ? result.Groups[1].Value : input;
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
        static void AddMethod(Dictionary<string, string[]> map, string hint, string method, string ghgoo, string native,
            string nspace)
        {
            map.Add(hint, new[] {method, ghgoo, native, nspace});
        }

        /// <summary>
        ///  Build a map of typehints.
        /// </summary>
        /// <returns>A dictionary of how to convert certain GH types to native types, to script types.</returns>
        public static Dictionary<string, string[]> BuildMap()
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
            var regex = @"#region |name|\r?\n?\s*?( *.*?)\s*\r?\n?#endregion".Replace("|name|", regionName);
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

        public void EnsureEditorConfig(string file)
        {
            var dir = Path.GetDirectoryName(file);

            
            var hasEditorConfig = Directory.GetFiles(dir).Any(s => s == ".editorconfig");

            if (hasEditorConfig) return;
            const string editorConfig = @"root = true
[*.cs]
indent_style = space
indent_size = 2";
            File.WriteAllText(file, editorConfig);
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

        public override void CreateAttributes()
        {
            m_attributes = new DetectDoubleClick(this);
        }

        public void OpenFileBrowesr()
        {
            OperatingSystem os = Environment.OSVersion;
            PlatformID pid = os.Platform;
            switch (pid)
            {
                case PlatformID.Win32NT:
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                case PlatformID.WinCE:
                    //"I'm on windows!"
                    if (Directory.Exists(Folder))
                    {
                        ProcessStartInfo startInfo = new ProcessStartInfo
                        {
                            Arguments = Folder,
                            FileName = "explorer.exe"
                        };

                        Process.Start(startInfo);
                    }
                    else
                    {
                        System.Windows.Forms.MessageBox.Show($"{0} directory does not exist! Double click cannot open explorer.");
                    }
                    break;
                case PlatformID.Unix:
                    //"I'm a linux box!"
                    break;
                case PlatformID.MacOSX:
                    //"I'm a mac!"
                    break;
                default:
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Unknonw OS. Double click to open is disabled");
                    break;
            }
        }

        public class DetectDoubleClick : Grasshopper.Kernel.Attributes.GH_ComponentAttributes
        {
            public DetectDoubleClick(IGH_Component component) : base(component)
            {
            }

            public override GH_ObjectResponse RespondToMouseDoubleClick(GH_Canvas sender, GH_CanvasMouseEvent e)
            {
                (Owner as ScriptParasiteComponent)?.OpenFileBrowesr();
                return GH_ObjectResponse.Handled;
            }
        }


    }

    public class TimeoutException : Exception
    {
    }
}