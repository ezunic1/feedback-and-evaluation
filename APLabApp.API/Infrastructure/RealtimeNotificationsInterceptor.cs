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

        private readonly List<Dal.Entities.Feedback> _addedFeedbacks = new();
        private readonly List<Dal.Entities.DeleteRequest> _addedDeleteRequests = new();

        public RealtimeNotificationsInterceptor(IServiceScopeFactory scopeFactory, IHubContext<NotificationsHub> hub)
        {
            _scopeFactory = scopeFactory;
            _hub = hub;
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Capture(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Capture(eventData.Context);
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

        private void Capture(DbContext? ctx)
        {
            if (ctx is not AppDbContext db) return;

            _addedFeedbacks.Clear();
            _addedDeleteRequests.Clear();

            foreach (var e in db.ChangeTracker.Entries<Dal.Entities.Feedback>().Where(x => x.State == EntityState.Added))
                _addedFeedbacks.Add(e.Entity);

            foreach (var e in db.ChangeTracker.Entries<Dal.Entities.DeleteRequest>().Where(x => x.State == EntityState.Added))
                _addedDeleteRequests.Add(e.Entity);
        }

        private async Task DispatchAsync(CancellationToken ct)
        {
            var feedbacks = _addedFeedbacks.ToArray();
            var deleteReqs = _addedDeleteRequests.ToArray();
            _addedFeedbacks.Clear();
            _addedDeleteRequests.Clear();

            try
            {
                if (feedbacks.Length > 0)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var receiverIds = feedbacks.Select(f => f.ReceiverUserId).Distinct().ToArray();

                    var map = await db.Users
                        .AsNoTracking()
                        .Where(u => receiverIds.Contains(u.Id))
                        .Select(u => new { u.Id, Sub = u.KeycloakId == null ? null : u.KeycloakId.ToString() })
                        .ToDictionaryAsync(x => x.Id, x => x.Sub?.Trim()?.ToLowerInvariant(), ct);

                    foreach (var f in feedbacks)
                    {
                        if (!map.TryGetValue(f.ReceiverUserId, out var kc) || string.IsNullOrWhiteSpace(kc)) continue;

                        var payload = new
                        {
                            feedbackId = f.Id,
                            seasonId = f.SeasonId,
                            senderUserId = f.SenderUserId,
                            receiverUserId = f.ReceiverUserId,
                            createdAtUtc = f.CreatedAtUtc
                        };

                        await _hub.Clients.Group($"kc:{kc}").SendAsync(NotificationEvents.NewFeedback, payload, ct);
                    }
                }

                if (deleteReqs.Length > 0)
                {
                    foreach (var dr in deleteReqs)
                    {
                        var payload = new
                        {
                            deleteRequestId = dr.Id,
                            feedbackId = dr.FeedbackId,
                            senderUserId = dr.SenderUserId,
                            reason = dr.Reason,
                            createdAtUtc = dr.CreatedAtUtc
                        };

                        await _hub.Clients.Group("role:admin").SendAsync(NotificationEvents.DeleteRequestCreated, payload, ct);
                    }
                }
            }
            catch
            {
            }
        }
    }
}
