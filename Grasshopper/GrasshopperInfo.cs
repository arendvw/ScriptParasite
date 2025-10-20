using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace ScriptParasite;

public class GrasshopperInfo : GH_AssemblyInfo
{
    public override string Name => "ScriptParasite2";

    //Return a 24x24 pixel bitmap to represent this GHA library.
    public override Bitmap Icon
    {
        get
        {
            using var s = typeof(GrasshopperInfo).Assembly.GetManifestResourceStream("ScriptParasite.Icon.icon.png");
            return s != null ? new Bitmap(s) : null;
        }
    }

    //Return a short string describing the purpose of this GHA library.
    public override string Description => "Edit C# and Python scripts in your favorite code editors, compatible with Rhino 8's new script editors";

    public override Guid Id => new Guid("4af0f527-36a1-4db6-9923-d0d16afb894b");

    //Return a string identifying you or your company.
    public override string AuthorName => "Arend van Waar";

    //Return a string representing your preferred contact details.
    public override string AuthorContact => "https://github.com/arendvw/ScriptParasite";
    public override GH_LibraryLicense License => GH_LibraryLicense.opensource;

    //Return a string representing the version.  This returns the same version as the assembly.
    public override string AssemblyVersion => GetType().Assembly.GetName().Version.ToString();
}