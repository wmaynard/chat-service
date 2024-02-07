using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using RCL.Logging;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Extensions;
using Rumble.Platform.Common.Services;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Services;

public class JanitorService : QueueService<JanitorService.CleanupTask>
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
        List<CleanupTask> list = new();
        list.AddRange(CreateTasks_Assignment());
        list.AddRange(CreateTasks_GlobalMessageCleanup());
        list.Add(new CleanupTask { Type = CleanupType.DeleteExpiredMessages });
        list.Add(new CleanupTask { Type = CleanupType.ClearOldReports });
        list.Add(new CleanupTask { Type = CleanupType.DeleteInactiveDmRooms });
        list.Add(new CleanupTask { Type = CleanupType.DeleteEmptyPrivateRooms });
        list.Add(new CleanupTask { Type = CleanupType.FindMessageSpam });
        
        CreateUntrackedTasks(list.Where(task => task != null).ToArray());
        
        foreach (IGrouping<CleanupType, CleanupTask> group in list.GroupBy(task => task.Type))
            Log.Local(Owner.Will, $"Created tasks: {group.Count()}x {group.Key.GetDisplayName()}");
    }

    private CleanupTask[] CreateTasks_Assignment()
    {
        List<CleanupTask> output = new();
        
        int page = 0;
        long remaining = 0;
        do
        {
            string[] rooms = _messages.GetRoomIdsForUnknownTypes(page, out remaining);
            
            output.AddRange(rooms.Select(roomId => new CleanupTask
            {
                Type = CleanupType.AssignMessageType,
                RoomId = roomId
            }));
        } while (remaining > 0);

        return output.ToArray();
    }
    private CleanupTask[] CreateTasks_GlobalMessageCleanup()
    {
        List<string> roomIds = new();

        int page = 0;
        long remaining = 0;
        do
        {
            roomIds.AddRange(_rooms
                .ListRoomsByType(RoomType.Global, page, out remaining)
                .Select(room => room.Id)
            );
        } while (remaining > 0);
        
        return roomIds
            .Select(roomId => new CleanupTask
            {
                Type = CleanupType.DeleteOldGlobalMessages,
                RoomId = roomId
            }).ToArray();
    }

    protected override void ProcessTask(CleanupTask task)
    {
        Log.Local(Owner.Will, $"Processing task type: {task.Type.GetDisplayName()}");
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
            case CleanupType.DeleteInactiveDmRooms:
                _rooms.DeleteInactiveDmRooms(out long deletedRooms, out long deletedDms);
                if (deletedDms + deletedRooms > 0)
                    Log.Info(Owner.Will, "Deleted inactive DMs", data: new
                    {
                        RoomCount = deletedRooms,
                        MessageCount = deletedDms
                    });
                break;
            case CleanupType.DeleteEmptyPrivateRooms:
                long deletedPrivateRooms = _rooms.DeleteEmptyPrivateRooms();
                if (deletedPrivateRooms > 0)
                    Log.Info(Owner.Will, "Deleted empty private rooms", data: new
                    {
                        RoomCount = deletedPrivateRooms
                    });
                break;
            case CleanupType.FindMessageSpam:
                // TODO: Waiting on PLATF-6517 for necessary diagnostics
                break;
            default:
                throw new NotImplementedException();
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

    // TODO: Review names
    public enum CleanupType
    {
        AssignMessageType,
        DeleteOldGlobalMessages,
        DeleteExpiredMessages,   // Covers all messages except announcements
        ClearOldReports,
        DeleteInactiveDmRooms,
        DeleteEmptyPrivateRooms,
        FindMessageSpam
    }
}