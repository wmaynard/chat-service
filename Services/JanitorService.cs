using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using RCL.Logging;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Services;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Services;

public class JanitorService : QueueService<CleanupTask>
{
    private readonly RoomService _rooms;
    private readonly MessageService _messages;
    private readonly ReportService _reports;
    
    public JanitorService(MessageService messages, ReportService reports, RoomService rooms) : base("cleanup", Common.Utilities.IntervalMs.ThirtyMinutes, 10, preferOffCluster: true)
    {
        _messages = messages;
        _rooms = rooms;
        _reports = reports;
    }

    protected override void OnTasksCompleted(CleanupTask[] data) { }

    protected override void PrimaryNodeWork()
    {
        int page = 0;
        long remaining = 0;

        List<CleanupTask> newTasks = new();

        // First, we need to run through all of our messages that don't have a message type associated with them.
        // We'll create a task to assign them a type so they can be effectively sorted when returning high traffic chat.
        List<string> roomIdsToLookup = new();
        do
        {
            string[] rooms = _messages.GetRoomIdsForUnknownTypes(page, out remaining);
            
            roomIdsToLookup.AddRange(rooms);
        } while (remaining > 0);
        
        newTasks.AddRange(roomIdsToLookup
            .Select(roomId => new CleanupTask
            {
                Type = CleanupType.AssignMessageType,
                RoomId = roomId
            })
        );
        
        // Next, we need to delete messages from global rooms that are no longer relevant.  Global rooms are incredibly
        // high traffic, so we'll want to clean those out quickly.
        string[] globalRoomIds = _rooms.ListGlobalRooms();
        newTasks.AddRange(globalRoomIds
            .Select(roomId => new CleanupTask
            {
                Type = CleanupType.DeleteOldGlobalMessages,
                RoomId = roomId
            })
        );
        
        // Finally, we'll want to create a cleanup task to simply delete expired messages.
        newTasks.Add(new CleanupTask
        {
            Type = CleanupType.DeleteExpiredMessages
        });

        CreateUntrackedTasks(newTasks.ToArray());
    }

    protected override void ProcessTask(CleanupTask task)
    {
        switch (task.Type)
        {
            case CleanupType.AssignMessageType:
                RoomType roomType = _rooms.GetRoomType(task.RoomId);

                if (roomType == RoomType.PossibleHack)
                {
                    long hacked = _messages.DeleteMessages(task.RoomId);
                    if (hacked > 0)
                        Log.Warn(Owner.Will, "Message(s) were found that do not match any known room.  Possibly submitted from a hacked client.", data: new
                        {
                            RoomId = task.RoomId,
                            DeletedCount = hacked
                        });
                }
                else
                {
                    MessageType targetType = roomType == RoomType.Global
                        ? MessageType.Global
                        : MessageType.Private;

                    long typeAssigned = _messages.AssignType(task.RoomId, targetType);
                    if (typeAssigned > 0)
                        Log.Local(Owner.Will, $"Assigned a type to {typeAssigned} messages.");
                }
                
                break;
            case CleanupType.DeleteExpiredMessages:
                long expired = _messages.DeleteExpiredMessages();
                if (expired > 0)
                    Log.Local(Owner.Will, $"Deleted {expired} expired messages.");
                break;
            case CleanupType.DeleteOldGlobalMessages:
                _messages.DeleteMessages(task.RoomId, 200);
                break;
            case CleanupType.ClearOldReports:
                long deleted = _reports.DeleteUnnecessaryReports();
                if (deleted > 0)
                    Log.Info(Owner.Will, "Deleted old, unneeded reports", data: new
                    {
                        Count = deleted
                    });
                break;
            default:
                throw new NotImplementedException();
        }
    }
}

public class CleanupTask : PlatformCollectionDocument
{
    [BsonElement("type")]
    [JsonIgnore]
    public CleanupType Type { get; set; }
    
    [BsonElement("roomId")]
    [JsonIgnore]
    public string RoomId { get; set; }
}

public enum CleanupType
{
    AssignMessageType,
    DeleteOldGlobalMessages,
    DeleteExpiredMessages,
    ClearOldReports
}

// TODO: Cleanup Inactive DMs
// TODO: Delete Empty DM rooms