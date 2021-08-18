using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Grasshopper.Kernel;

namespace StudioAvw.Gh.Parasites
{
    public class GrasshopperDocumentWatcher : IDisposable
    {
        public GrasshopperDocumentWatcher(GH_Document document)
        {
            Document = document;
            document.SolutionStart += DocumentOnSolutionStart;
            document.SolutionEnd += DocumentOnSolutionEnd;
            State = document.SolutionState;
        }

        public GH_ProcessStep State { get; set; }

        private void DocumentOnSolutionEnd(object sender, GH_SolutionEventArgs e)
        {
            State = e.Document.SolutionState;
        }
        private void DocumentOnSolutionStart(object sender, GH_SolutionEventArgs e)
        {
            State = e.Document.SolutionState;
        }

        public delegate void ScheduleCallback();

        public async Task WaitForSolutionEnd(int timeout)
        {
            if (State == GH_ProcessStep.PostProcess || State == GH_ProcessStep.PreProcess)
            {
                return;
            }
            var sw = new Stopwatch();
            sw.Start();
            while (true)
            {
                if (sw.ElapsedMilliseconds > timeout)
                {
                    throw new TimeoutException();
                }

                if (State == GH_ProcessStep.PostProcess || State == GH_ProcessStep.PreProcess)
                {
                    return;
                }
                await Task.Delay(10);
            }
        }

        public GH_Document Document { get; set; }

        public void Dispose()
        {
            if (Document == null) return;

            Document.SolutionStart -= DocumentOnSolutionStart;
            Document.SolutionEnd -= DocumentOnSolutionEnd;
        }
    }
}
