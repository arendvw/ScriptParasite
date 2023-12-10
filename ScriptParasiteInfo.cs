using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace StudioAvw.Gh.Parasites
{
    public class ScriptParasiteInfo : GH_AssemblyInfo
    {

        public override string Name => "ScriptParasite";

        public override string Version => "1.2.1";

        public override Bitmap Icon => Resources.ScriptParasiteIcon;

        public override Guid Id => new Guid("0f9371a5-9665-47f6-b40b-40f7fe835d68");

        public override string AuthorName => "Arend van Waart";

        public override string AuthorContact => "https://github.com/arendvw/ScriptParasite";

        public override GH_LibraryLicense AssemblyLicense => GH_LibraryLicense.opensource;

        public override string AssemblyDescription =>
            "Allows editting of C# Script components with the editor of your choice, (Visual Studio, Visual Studio Code, Rider, etc.";


    }
}
