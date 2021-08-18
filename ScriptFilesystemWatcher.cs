using System;
using System.IO;

namespace StudioAvw.Gh.Parasites
{


    public class ScriptFilesystemWatcher : IDisposable
    {
        public ScriptFilesystemWatcher(string folderToWatch, string file)
        {
            File = file;
            Directory = folderToWatch;
            
            Debouncer = DebounceHelper.Debounce(SendUpdate, 300);

            Watcher = new FileSystemWatcher(folderToWatch)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime
            };
            Watcher.Renamed += HandleRenameEvent;
            Watcher.Deleted += HandleEvent;
            Watcher.Changed += HandleEvent;
            Watcher.Created += HandleEvent;
            Watcher.EnableRaisingEvents = true;
        }

        public bool IsWriting { get; set; }

        public string Directory { get; set; }

        private void SendUpdate()
        {
            if (IsWriting)
            {
                return;
            }
            if (System.IO.File.Exists(Path.Combine(Directory, File)))
            {
                FileUpdated?.Invoke(this, EventArgs.Empty);
            }
            // otherwise, the file just got deleted, or moved around.
        }

        public Action Debouncer { get; set; }

        public string File { get; set; }
        
        public event EventHandler<EventArgs> FileUpdated;
        private void HandleEvent(object sender, FileSystemEventArgs e)
        {
            if (IsWriting)
            {
                return;
            }
            if (e.Name == File)
            {
                Debouncer();
            }
        }

        private void HandleRenameEvent(object sender, RenamedEventArgs e)
        {
            if (IsWriting)
            {
                return;
            }
            if (e.Name == File || e.OldName == File)
            {
                Debouncer();
            }
        }

        public FileSystemWatcher Watcher { get; set; }

        public void Dispose()
        {
            if (Watcher != null)
            {
                Watcher.Renamed -= HandleRenameEvent;
                Watcher.Deleted -= HandleEvent;
                Watcher.Changed -= HandleEvent;
                Watcher.Created -= HandleEvent;
            }

            Watcher?.Dispose();
        }
    }
}
