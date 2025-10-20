using Grasshopper.Kernel;

namespace ScriptParasite;

public interface IParasiteComponent
{
    public IGH_Component SetTarget(IGH_Component component);

    public bool IsValid(IGH_Component component);
    
    public IGH_Component TargetComponent { get; }
}