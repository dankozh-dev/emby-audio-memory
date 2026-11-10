using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;

namespace Emby.AudioMemory
{
    public sealed class ReconcileTask : IScheduledTask
    {
        public string Name => "Apply audio choices and studio priorities";
        public string Key => "AudioMemoryReconcile";
        public string Description => "Applies manual season choices and studio priorities to inactive movies and episodes; preserves independent choices.";
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
