using System;
using Grasshopper.Kernel;
using ScriptComponents;
using StudioAvw.Gh.Parasites.Component;
using StudioAvw.Gh.Parasites.Helper;

namespace StudioAvw.Gh.Parasites.Watcher
{
    public class ScriptComponentWatcher : IDisposable
    {
        public ScriptComponentWatcher(Component_CSNET_Script document)
        {
            Document = document;
            CurrentScript = GetScript();
            Debouncer = DebounceHelper.Debounce(SendUpdate, 500);

            document.SolutionExpired += SolutionExpired;
            document.ObjectChanged += ObjectChanged;
            document.AttributesChanged += DocumentOnAttributesChanged;
        }

        private void SendUpdate()
        {
            ScriptUpdated?.Invoke(this, new EventArgs());
        }

        private Action Debouncer { get; set; }

        private void DocumentOnAttributesChanged(IGH_DocumentObject sender, GH_AttributesChangedEventArgs e)
        {
            if (IsUpdating)
            {
                return;
            }
            Debouncer?.Invoke();
        }

        private void ObjectChanged(IGH_DocumentObject sender, GH_ObjectChangedEventArgs e)
        {
            if (IsUpdating)
            {
                return;
            }
            Debouncer?.Invoke();
        }

        public event EventHandler<EventArgs> ScriptUpdated;

        public string CurrentScript { get; set; }

        public string GetScript()
        {
            return $"{Document.ScriptSource.AdditionalCode}" +
                   $"{Document.ScriptSource.ScriptCode}" +
                   $"{Document.ScriptSource.UsingCode}" +
                   $"{ ScriptParasiteComponent.ExtractScriptParameters(Document) }";
        }

        private void SolutionExpired(IGH_DocumentObject sender, GH_SolutionExpiredEventArgs e)
        {
            if (IsUpdating)
            {
                return;
            }
            if (CurrentScript != GetScript())
            {
                CurrentScript = GetScript();
                Debouncer?.Invoke();
            }
        }
        
        public Component_CSNET_Script Document { get; set; }
        public bool IsUpdating { get; set; }

        public void Dispose()
        {
            if (Document == null)
            {
                return;
            }

            Document.SolutionExpired -= SolutionExpired;
            Document.ObjectChanged -= ObjectChanged;
            Document.AttributesChanged -= DocumentOnAttributesChanged;

        }
    }
}
