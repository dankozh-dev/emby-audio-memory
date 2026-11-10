using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;

namespace Emby.AudioMemory
{
    public sealed class ReconcileTask : IScheduledTask
    {
        public string Name => "Apply remembered season audio";
        public string Key => "AudioMemoryReconcile";
        public string Description => "Applies saved audio choices to new or newly scanned episodes; preserves later independent choices.";
        public string Category => "Audio Memory";
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => new[]
        {
            new TaskTriggerInfo { Type = "IntervalTrigger", IntervalTicks = TimeSpan.FromMinutes(15).Ticks }
        };
        public Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Plugin.Instance?.Configuration.Enable == true) Plugin.Instance.Reconcile(false);
            progress.Report(100);
            return Task.CompletedTask;
        }
    }
}
