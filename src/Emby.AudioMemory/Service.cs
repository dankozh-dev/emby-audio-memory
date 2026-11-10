using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;
using System;

namespace Emby.AudioMemory
{
    [Authenticated(Roles = "Admin")]
    [Route("/AudioMemory/Status", "GET")]
    public sealed class GetStatus : IReturn<object> { }

    [Authenticated(Roles = "Admin")]
    [Route("/AudioMemory/Seed", "POST")]
    public sealed class SeedRequest : IReturn<object>
    {
        public string UserId { get; set; } = "";
        public long EpisodeId { get; set; }
        public int AudioStreamIndex { get; set; }
        public bool DryRun { get; set; } = true;
    }

    [Authenticated(Roles = "Admin")]
    [Route("/AudioMemory/Reconcile", "POST")]
    public sealed class ReconcileRequest : IReturn<object> { public bool DryRun { get; set; } = true; }

    [Authenticated(Roles = "Admin")]
    [Route("/AudioMemory/Rollback", "POST")]
    public sealed class RollbackRequest : IReturn<object>
    {
        public string UserId { get; set; } = "";
        public long SeasonId { get; set; }
        public bool DryRun { get; set; } = true;
    }

    public sealed class Service : IService
    {
        private static Plugin Current => Plugin.Instance ?? throw new InvalidOperationException("Audio Memory is not running.");
        public object Get(GetStatus request) => Current.Status();
        public object Post(SeedRequest request) => Current.Seed(request.UserId, request.EpisodeId, request.AudioStreamIndex, request.DryRun);
        public object Post(ReconcileRequest request) => Current.Reconcile(request.DryRun);
        public object Post(RollbackRequest request) => Current.Rollback(request.UserId, request.SeasonId, request.DryRun);
    }
}
