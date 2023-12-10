using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;

namespace StudioAvw.Gh.Parasites.Component
{
    public class DetectDoubleClickAttributes : Grasshopper.Kernel.Attributes.GH_ComponentAttributes
    {
        public DetectDoubleClickAttributes(IGH_Component component) : base(component)
        {
        }

        public override GH_ObjectResponse RespondToMouseDoubleClick(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            (Owner as ScriptParasiteComponent)?.OpenFileBrowser();
            return GH_ObjectResponse.Handled;
        }
    }
}