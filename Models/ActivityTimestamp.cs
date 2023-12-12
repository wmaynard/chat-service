using System;
using System.Linq;
using RCL.Logging;
using Rumble.Platform.Common.Minq;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Models;

public class ActivityTimestamp : PlatformCollectionDocument
{
    public string AccountId { get; set; }
    public bool IsActive { get; set; }
    public long LastActive { get; set; } // Do we need a MarkedInactive timestamp?
}

public class ActivityService : MinqTimerService<ActivityTimestamp>
{
    private readonly RoomService _rooms;
    private readonly RumbleJson _activePlayerBuffer = new(); 

    public ActivityService(RoomService rooms) : base("activity")
        => _rooms = rooms;

    protected override void OnElapsed()
    {
        FlushActivityBuffer();
        RemoveInactivePlayersFromGlobalRooms();
    }

    public void MarkAsActive(string accountId)
    {
        _activePlayerBuffer[accountId] = Timestamp.Now;
        
        if (_activePlayerBuffer.Count > 100)
            FlushActivityBuffer();
    }

    private void FlushActivityBuffer()
    {
        ActivityTimestamp[] input = _activePlayerBuffer
            .Select(pair => new ActivityTimestamp
            {
                AccountId = pair.Key,
                IsActive = true,
                LastActive = (long)pair.Value
            })
            .ToArray();

        mongo
            .WithTransaction(out Transaction transaction)
            .Where(query => query.ContainedIn(ts => ts.AccountId, input.Select(activity => activity.AccountId)))
            .Delete();
        
        mongo
            .WithTransaction(transaction)
            .Insert(input);
        
        _activePlayerBuffer.Clear();
        Commit(transaction);
    }

    /// <summary>
    /// Inactive players need to be culled from rooms so that the global room they were in frees up an open spot.
    /// </summary>
    private void RemoveInactivePlayersFromGlobalRooms()
    {
        long affected = 0;
        mongo
            .WithTransaction(out Transaction transaction)
            .Where(query => query
                .EqualTo(ts => ts.IsActive, true)
                .LessThanOrEqualTo(ts => ts.LastActive, Timestamp.ThirtyMinutesAgo)
            )
            .Process(batchSize: 1_000, batch =>
            {
                string[] accountIds = batch.Results.Select(ts => ts.AccountId).ToArray();
                affected += _rooms.RemoveFromGlobalRooms(transaction, accountIds);
                mongo
                    .Where(query => query.ContainedIn(ts => ts.AccountId, accountIds))
                    .Update(update => update.Set(ts => ts.IsActive, false));
            });
        try
        {
            Commit(transaction);
            if (affected > 0)
                Log.Info(Owner.Will, "Marked accounts as inactive", data: new
                {
                    Affected = affected
                });
        }
        catch
        {
            Abort(transaction);
            throw;
        }
    }
}