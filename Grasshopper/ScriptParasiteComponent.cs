using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using RhinoCodePluginGH.Components;
using ScriptParasite.Watcher;

namespace ScriptParasite;

public class ScriptParasiteComponent : SafeComponent, IParasiteComponent
{
    /// <summary>
    /// Each implementation of GH_Component must provide a public 
    /// constructor without any arguments.
    /// Category represents the Tab in which the component will appear, 
    /// Subcategory the panel. If you use non-existing tab or panel names, 
    /// new tabs/panels will automatically be created.
    /// </summary>
    public ScriptParasiteComponent() : base("ScriptParasite2", "SP", "Link scripts to existing files on your disk",
        "Math", "Script")
    {
    }

    public static string SettingsFile =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GhScriptParasite",
            "DefaultFolder.txt");

    private static string ReadDefaultFolder()
    {
        return File.Exists(SettingsFile) ? File.ReadAllText(SettingsFile) : null;
    }

    public ScriptFilesystemWatcher Watcher { get; set; }
    public ScriptComponentWatcher ComponentWatcher { get; set; }

    public override void CreateAttributes()
    {
        m_attributes = new ParasiteAttributes(this);
    }
    
    /// <summary>
    /// Registers all the input parameters for this component.
    /// </summary>
    protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
    {
        var defaultFolder = ReadDefaultFolder();
        // Use the pManager object to register your output parameters.
        // Output parameters do not have default values, but they too must have the correct access type.
        pManager.AddBooleanParameter("Enable", "E", "Enable the script parasite", GH_ParamAccess.item, false);
        var idx = pManager.AddTextParameter("Path", "P", "Path to the script file", GH_ParamAccess.item, defaultFolder);
        pManager[idx].Optional = true;
    }

    /// <summary>
    /// Registers all the output parameters for this component.
    /// </summary>
    protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
    {
        pManager.AddTextParameter("Updated", "U", "Output text", GH_ParamAccess.item);
    }

    private int _iteration;
    private string _defaultFolder;

    protected override void BeforeSolveInstance()
    {
        if (StoredGuid != null && TargetComponent == null)
        {
            var doc = OnPingDocument();
            var comp = doc?.FindComponent(StoredGuid.Value);
            if (comp != null && IsValid(comp))
            {
                TargetComponent = comp;
            }
        }

        StoredGuid = null;
        _iteration = 0;
    }

    /// <summary>
    /// This is the method that actually does the work.
    /// </summary>
    /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
    /// to store data in output parameters.</param>
    protected override void SolveInstance(IGH_DataAccess da)
    {
        try
        {
            // reset everything, and let's start over..
            Cleanup();
            // things are botched up if this script runs more than once.
            // events might be added more then once if this component somehow has an input of multiple items.
            if (_iteration++ != 0) return;
            var watch = false;
            var folder = "";
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

            if (TargetScriptComponent == null || !IsValid(TargetComponent))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No valid target script component selected");
                return;
            }

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

            if (!WriteScriptToFile(TargetScriptComponent, FileNameSafe)) return;

            // Create a new FileSystemWatcher and set its properties.
            // http://stackoverflow.com/questions/721714/notification-when-a-file-changes
            AddEvents(directory, filename);
            WriteDefaultConfig(directory);
            EnsureProject(Path.Combine(directory, "GrasshopperScripts.csproj"));
            EnsureEditorConfig(Path.Combine(directory, ".editorconfig"));
            da.SetData(0, $"{FileNameSafe}");
        }
        catch (Exception ex)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                $"Unexpected error: {ex.Message}, \nstacktrace: {ex.StackTrace}");
        }
    }

    
    public IGH_Component TargetComponent { get; private set; }
    public BaseLanguageComponent TargetScriptComponent => TargetComponent as BaseLanguageComponent;

    public IGH_Component SetTarget(IGH_Component target)
    {
        if (TargetComponent != target)
        {
            OnPingDocument().ScheduleSolution(10, (_) => ExpireSolution(true));
        }
        TargetComponent = target;
        return TargetComponent;
    }

    public bool IsValid(IGH_Component component)
    {
        return component is BaseLanguageComponent;
    }

    public string Folder { get; set; }

    protected string FileNameSafe
    {
        get
        {
            var name = Regex.Replace(TargetComponent.NickName, @"\W", "_");
            var componentId = TargetComponent.InstanceGuid.ToString().Replace(" - ", "").Substring(0, 5);
            return Path.Combine($"{Folder}", $"{name}-{componentId}.{FileExtension}");
        }
    }
    public string FileExtension => TargetScriptComponent is CSharpComponent ? "cs" : "py";

    private async void ScriptUpdated(object sender, EventArgs e)
    {
        await CheckAndUpdateExport();
    }

    public override void Cleanup()
    {
        CleanUpEvents();
    }
    protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
    {
        base.AppendAdditionalComponentMenuItems(menu);

        Menu_AppendItem(menu, "Open Folder", (s, e) =>
        {
            if (FileNameSafe != null)
            {
                OpenFileBrowser(Folder, FileNameSafe);                
            }
        });

        Menu_AppendItem(menu, "Open File", (s, e) =>
        {
            if (FileNameSafe != null)
            {
                OpenFileWithDefaultApp(FileNameSafe);
            }
            
        });
    }
    public override void CleanUpEvents()
    {
        ComponentWatcher?.Dispose();
        ComponentWatcher = null;
        Watcher?.Dispose();
        Watcher = null;
        Message = "Disabled";
    }

    protected override bool ShouldContinue()
    {
        return base.ShouldContinue() && TargetComponent != null && TargetComponent.OnPingDocument() == OnPingDocument();
    }

    private async Task CheckAndUpdateExport()
    {
        if (!ShouldContinue())
        {
            Cleanup();
            return;
        }

        Watcher.IsWriting = true;
        WriteScriptToFile(TargetScriptComponent, FileNameSafe);
        await Task.Delay(50);
        Watcher.IsWriting = false;
    }

    public void AddEvents(string directory, string filename)
    {
        CleanUpEvents();
        if (TargetScriptComponent == null)
        {
            return;
        }

        ComponentWatcher = new ScriptComponentWatcher(TargetScriptComponent);
        ComponentWatcher.ScriptUpdated += ScriptUpdated;
        Watcher = new ScriptFilesystemWatcher(directory, filename);
        Watcher.FileUpdated += OnFileChanged;
        Message = "Watching";
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
            Grasshopper.Instances.DocumentEditor?.BeginInvoke((Action)(async void () =>
            {
                WriteScriptToComponent(TargetScriptComponent, FileNameSafe);
                TargetComponent.ExpireSolution(false);
                OnPingDocument().ScheduleSolution(10);
                await Task.Delay(10);
                await ghWatcher.WaitForSolutionEnd(2500);
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

    private bool WriteScriptToFile(BaseLanguageComponent script, string filename)
    {
        try
        {
            if (!script.TryGetSource(out var text))
            {
                return false;
            }

            File.WriteAllText(filename, text);
            return true;
        }
        catch (Exception ex)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                $"Could not write script to file {filename}, error: {ex.Message}, stacktrace: {ex.StackTrace}");
            return false;
        }
    }

    protected static void WriteScriptToComponent(BaseLanguageComponent scriptObject, string filename)
    {
        scriptObject.SetSource(File.ReadAllText(filename));
        scriptObject.SetParametersFromScript();
    }

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
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Could not create folder '{folder}', path is too long");
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
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Cannot create folder '{folder}', error: '{ex.Message}");
            return false;
        }

        return true;
    }
    private void WriteDefaultConfig(string directory)
    {
        if (!TryGetFolder(Path.GetDirectoryName(SettingsFile)))
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Could not write default folder settings to file {SettingsFile}");
        }
        File.WriteAllText(SettingsFile, directory);
        _defaultFolder = directory;
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
indent_size = 4";
        File.WriteAllText(file, editorConfig);
    }
    public void EnsureProject(string file)
    {
        // use default settings..
        // write skeleton script component
    }
    public static void OpenFileWithDefaultApp(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return;

        var os = Environment.OSVersion.Platform;
        switch (os)
        {
            case PlatformID.Win32NT:
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true // required to open with default app
                });
                break;

            case PlatformID.MacOSX:
            case PlatformID.Unix:
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = $"\"{filePath}\"",
                    UseShellExecute = false
                });
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }
    public static void OpenFileBrowser(string folder, string fileNameSafe)
    {
        if (folder == null)
        {
            return;
        }

        var os = Environment.OSVersion;
        var pid = os.Platform;
        switch (pid)
        {
            case PlatformID.Win32NT:
                var fileExists = File.Exists(fileNameSafe);
                var startInfo = new ProcessStartInfo
                {
                    Arguments = fileExists ? $"/select,\"{fileNameSafe}\"" : folder, FileName = "explorer.exe"
                };
                Process.Start(startInfo);
                break;
            case PlatformID.MacOSX:
            case PlatformID.Unix:
                // It's probably a mac
                break;

            // we are very unlikely to encounter gh on any other platform
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    /// The Exposure property controls where in the panel a component icon 
    /// will appear. There are seven possible locations (primary to septenary), 
    /// each of which can be combined with the GH_Exposure.obscure flag, which 
    /// ensures the component will only be visible on panel dropdowns.
    /// </summary>
    public override GH_Exposure Exposure => GH_Exposure.primary;

    /// <summary>
    /// Provides an Icon for every component that will be visible in the User Interface.
    /// Icons need to be 24x24 pixels.
    /// You can add image files to your project resources and access them like this:
    /// return Resources.IconForThisComponent;
    /// </summary>
    protected override System.Drawing.Bitmap Icon
    {
        get
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("ScriptParasite.Icon.icon.png");
            return s != null ? new Bitmap(s) : null;
        }
    }
    
    /// <summary>
    /// Each component must have a unique Guid to identify it. 
    /// It is vital this Guid doesn't change otherwise old ghx files 
    /// that use the old ID will partially fail during loading.
    /// </summary>
    public override Guid ComponentGuid => new Guid("33cbbb9a-48d3-466d-897e-3704a016fcb3");
    
    public override bool Write(GH_IWriter writer)
    {
        if (TargetScriptComponent != null)
        {
            writer.SetGuid("TargetComponent", TargetScriptComponent.InstanceGuid);
        }
        return base.Write(writer);
    }

    public override bool Read(GH_IReader reader)
    {
        var id = Guid.Empty;
        
        if (reader.TryGetGuid("TargetComponent", ref id))
        {
            StoredGuid = id;
        }
        
        return base.Read(reader);
    }

    // used temporarily to store the target component untill we can confirm or deny it's existence.
    private Guid? StoredGuid { get; set; }
}