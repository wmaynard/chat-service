using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using RCL.Logging;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Enums;
using Rumble.Platform.Common.Exceptions;
using Rumble.Platform.Common.Minq;
using Rumble.Platform.Common.Services;
using Rumble.Platform.Common.Utilities;

namespace Rumble.Platform.ChatService.Services;

public class ActivityService : MinqTimerService<Activity>
{
    private struct Data
    {
        public long LastActive;
        public int ActivityCount;
        public long CreatedOn;
    }
    
    private readonly RoomService _rooms;
    private readonly ConcurrentDictionary<string, Data> _buffer = new();

    public ActivityService(RoomService rooms) : base("activity", IntervalMs.FiveMinutes)
        => _rooms = rooms;

    protected override void OnElapsed() // TODO: make this a Janitor task?
    {
        FlushActivityBuffer();
        RemoveInactivePlayersFromGlobalRooms();
    }

    private void FlushActivityBuffer()
    {
        if (!_buffer.Any())
            return;

        // TODO: Without a batch update, the only efficient way to do this is to wipe the records and re-insert them.
        // Consequently, our Activity.CreatedOn will never be accurate.
        Activity[] activities = mongo
            .WithTransaction(out Transaction transaction)
            .Where(query => query.ContainedIn(ts => ts.AccountId, _buffer.Select(pair => pair.Key)))
            .ToArray()
            .UnionBy(_buffer.Select(pair => new Activity
            {
                AccountId = pair.Key,
                Counts = new Queue<int>(),
                CreatedOn = pair.Value.CreatedOn
            }), activity => activity.AccountId)
            .ToArray();

        foreach (Activity activity in activities)
        {
            activity.IsActive = _buffer.TryGetValue(activity.AccountId, out Data data);
            
            if (!activity.IsActive)
                continue;

            activity.LastActive = data.LastActive;
            activity.Counts.Enqueue(data.ActivityCount);

            while (activity.Counts.Count > 25)
                activity.Counts.Dequeue();
        }
        
        mongo.WithTransaction(transaction)
            .Where(query => query.ContainedIn(ts => ts.AccountId, _buffer.Select(pair => pair.Key)))
            .Delete();
        
        mongo
            .WithTransaction(transaction)
            .Insert(activities);
        
        _buffer.Clear();
        Commit(transaction);
        
        Log.Local(Owner.Will, "Flushed activity buffer.");
    }

    /// <summary>
    /// Marks an account as active in memory.  Memory is flushed to the database at regular intervals, but stored this way
    /// to reduce DB strain.
    /// </summary>
    /// <param name="accountId"></param>
    public void MarkAsActive(string accountId)
    {
        if (_buffer.TryGetValue(accountId, out Data data))
        {
            data.LastActive = Timestamp.Now;
            data.ActivityCount++;
            _buffer[accountId] = data;
        }
        else
            _buffer[accountId] = new Data
            {
                LastActive = Timestamp.Now,
                ActivityCount = 1,
                CreatedOn = Timestamp.Now
            };
        
        if (_buffer.Count > 100)
            FlushActivityBuffer();
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
                .LessThanOrEqualTo(player => player.LastActive, Timestamp.TenMinutesAgo)
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