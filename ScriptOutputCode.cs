using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudioAvw.Gh.Parasites
{
    /// <summary>
    /// Simple properties for the csproj template
    /// </summary>
    partial class ScriptOutput
    {
        public string UniqueNamespace { get; set; }
        public string InputOutput { get; set; }
        public string Using { get; set; }
        public string SourceCode { get; set; }
        public string AdditionalCode { get; set; }

        // Grasshopper < 1.0 default usings..
        public List<string> CustomUsings { get; set; } = new List<string>
        {
            "Rhino.DocObjects",
            "Rhino.Collections",
            "GH_IO",
            "GH_IO.Serialization",
            "System.IO",
            "System.Xml",
            "System.Xml.Linq",
            "System.Linq",
            "System.Data",
            "System.Drawing",
            "System.Reflection",
            "System.Runtime.InteropServices"
        };
    }
}
