using System.Linq;
using RCL.Logging;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Minq;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Services;

public class ActivityService : MinqTimerService<Activity>
{
    private readonly RoomService _rooms;
    private readonly RumbleJson _activePlayerBuffer = new(); 

    public ActivityService(RoomService rooms) : base("activity", 5_000)//IntervalMs.FiveMinutes)
        => _rooms = rooms;

    protected override void OnElapsed() // TODO: make this a Janitor task?
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
        Activity[] input = _activePlayerBuffer
            .Select(pair => new Activity
            {
                AccountId = pair.Key,
                IsActive = true,
                LastActive = (long)pair.Value
            })
            .ToArray();

        if (!input.Any())
            return;
        

        // TODO: Without a batch update, the only efficient way to do this is to wipe the records and re-insert them.
        // Consequently, our Activity.CreatedOn will never be accurate.
        mongo
            .WithTransaction(out Transaction transaction)
            .Where(query => query.ContainedIn(ts => ts.AccountId, input.Select(activity => activity.AccountId)))
            .Delete();
        
        mongo
            .WithTransaction(transaction)
            .Insert(input);
        
        _activePlayerBuffer.Clear();
        Commit(transaction);
        
        Log.Local(Owner.Will, "Flushed activity buffer.");
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
                .EqualTo(player => player.IsActive, true)
                .LessThanOrEqualTo(player => player.LastActive, Timestamp.ThirtyMinutesAgo)
            )
            .Process(batchSize: 1_000, batch =>
            {
                string[] accountIds = batch.Results.Select(ts => ts.AccountId).ToArray();
                affected += _rooms.RemoveFromGlobalRooms(transaction, accountIds);
                mongo
                    .WithTransaction(transaction)
                    .Where(query => query.ContainedIn(player => player.AccountId, accountIds))
                    .Update(update => update.Set(player => player.IsActive, false));
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