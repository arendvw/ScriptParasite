using Grasshopper.Kernel;

namespace ScriptParasite;
public abstract class SafeComponent(
    string name,
    string nickname,
    string description,
    string category,
    string subCategory)
    : GH_Component(name, nickname, description, category, subCategory)
{
    protected bool InIteration { get; set; }
    /// <summary>
    /// Override this method if you want to be called 
    ///  before the first call to SolveInstance.
    /// </summary>
    protected override void BeforeSolveInstance()
    {
        InIteration = true;
        // We're busy now.
        base.BeforeSolveInstance();
    }

    /// <summary>
    /// Override this method if you want to be called 
    ///  after the last call to SolveInstance.
    /// </summary>
    protected override void AfterSolveInstance()
    {
        base.AfterSolveInstance();
        InIteration = false;
    }

    /// <summary>
    /// Clean up events
    /// </summary>
    public abstract void CleanUpEvents();


    /// <summary>
    /// Possibly smart to clean up any lingering events.
    /// </summary>
    public abstract void Cleanup();

    /// <inheritdoc />
    /// <summary>
    /// Overrides the MovedBetweenDocuments method and delegates the call to all parameters. 
    /// </summary>
    /// <param name="oldDocument">Document that used to own this object.</param><param name="newDocument">Document that now owns ths object.</param>
    public override
        void MovedBetweenDocuments(GH_Document oldDocument, GH_Document newDocument)
    {
        Cleanup();
        base.MovedBetweenDocuments(oldDocument, newDocument);
    }

    /// <inheritdoc />
    /// <summary>
    /// Overrides the RemovedFromDocument method and delegates the call to all parameters. 
    /// </summary>
    /// <param name="document">Document that now no longer owns this object.</param>
    public override void RemovedFromDocument(GH_Document document)
    {
        Cleanup();
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>

    protected virtual bool ShouldContinue()
    {
        return OnPingDocument() != null
               && !Locked
               && !GH_Document.IsEscapeKeyDown();
    }
    
}