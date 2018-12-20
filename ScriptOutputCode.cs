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
        public string SourceCode { get; set; }
        public string AdditionalCode { get; set; }
    }
}
