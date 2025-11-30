using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using APLabApp.API.Hubs;
using APLabApp.Dal;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace APLabApp.API.Infrastructure
{
    public class RealtimeNotificationsInterceptor : SaveChangesInterceptor
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<NotificationsHub> _hub;
        private readonly List<(int FeedbackId, int SeasonId, Guid ReceiverUserId, DateTime CreatedAtUtc)> _createdFeedbacks = new();
        private readonly List<(int DeleteRequestId, int FeedbackId, string Reason, DateTime CreatedAtUtc)> _createdDeleteRequests = new();

        public RealtimeNotificationsInterceptor(IServiceScopeFactory scopeFactory, IHubContext<NotificationsHub> hub)
        {
            _scopeFactory = scopeFactory;
            _hub = hub;
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            CaptureNewEntities(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            CaptureNewEntities(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            if (result > 0) _ = DispatchAsync(CancellationToken.None);
            return base.SavedChanges(eventData, result);
        }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (result > 0) _ = DispatchAsync(cancellationToken);
            return base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        private void CaptureNewEntities(DbContext? ctx)
        {
            if (ctx is not AppDbContext db) return;

            var addedF = db.ChangeTracker.Entries<Dal.Entities.Feedback>()
                .Where(e => e.State == EntityState.Added)
                .Select(e => (e.Entity.Id, e.Entity.SeasonId, e.Entity.ReceiverUserId, e.Entity.CreatedAtUtc));

            var addedD = db.ChangeTracker.Entries<Dal.Entities.DeleteRequest>()
                .Where(e => e.State == EntityState.Added)
                .Select(e => (e.Entity.Id, e.Entity.FeedbackId, e.Entity.Reason, e.Entity.CreatedAtUtc));

            foreach (var x in addedF)
                _createdFeedbacks.Add((x.Id, x.SeasonId, x.ReceiverUserId, x.CreatedAtUtc));

            foreach (var x in addedD)
                _createdDeleteRequests.Add((x.Id, x.FeedbackId, x.Reason, x.CreatedAtUtc));
        }

        private async Task DispatchAsync(CancellationToken ct)
        {
            try
            {
                if (_createdFeedbacks.Count > 0)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var receiverIds = _createdFeedbacks.Select(f => f.ReceiverUserId).Distinct().ToArray();

                    var map = await db.Users
                        .AsNoTracking()
                        .Where(u => receiverIds.Contains(u.Id))
                        .Select(u => new { u.Id, u.KeycloakId })
                        .ToDictionaryAsync(x => x.Id, x => x.KeycloakId, ct);

                    foreach (var f in _createdFeedbacks)
                    {
                        if (!map.TryGetValue(f.ReceiverUserId, out var kc)) continue;
                        var payload = new { feedbackId = f.FeedbackId, seasonId = f.SeasonId, receiverUserId = f.ReceiverUserId, createdAtUtc = f.CreatedAtUtc };
                        await _hub.Clients.Group($"kc:{kc}").SendAsync("newFeedback", payload, ct);
                    }
                }

                if (_createdDeleteRequests.Count > 0)
                {
                    foreach (var dr in _createdDeleteRequests)
                    {
                        var payload = new { deleteRequestId = dr.DeleteRequestId, feedbackId = dr.FeedbackId, reason = dr.Reason, createdAtUtc = dr.CreatedAtUtc };
                        await _hub.Clients.Group("role:admin").SendAsync("deleteRequestCreated", payload, ct);
                    }
                }
            }
            finally
            {
                _createdFeedbacks.Clear();
                _createdDeleteRequests.Clear();
            }
        }
    }
}
