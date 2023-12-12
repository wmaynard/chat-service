using System.Linq;
using Rumble.Platform.Common.Minq;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Models;

public class Message : PlatformCollectionDocument
{
    public string AccountId { get; set; }
    public string Body { get; set; }
    public RumbleJson Data { get; set; }
    public long Expiration { get; set; }
    public string RoomId { get; set; }
    public MessageType Type { get; set; }
}

public enum MessageType
{
    Unassigned = 0,
    Administrator,
    Global,
    Private,
}

public class MessageService : MinqService<Message>
{
    public const int MESSAGE_LIMIT = 200;
    
    public MessageService() : base("messages") { }

    public Message[] GetGlobalMessages(string roomId, long timestamp = 0) => mongo
        .Where(query => query
            .EqualTo(message => message.RoomId, roomId)
            .GreaterThan(message => message.CreatedOn, timestamp)
            .GreaterThanOrEqualTo(message => message.Expiration, Timestamp.Now)
        )
        .Sort(sort => sort.OrderBy(message => message.CreatedOn))
        .Limit(MESSAGE_LIMIT)
        .ToArray();
    
    public Message[] GetAllMessages(string[] roomIds, long timestamp = 0) => mongo
        .Where(query => query
            .ContainedIn(message => message.RoomId, roomIds)
            .GreaterThan(message => message.CreatedOn, timestamp)
            .GreaterThanOrEqualTo(message => message.Expiration, Timestamp.Now)
        )
        .Sort(sort => sort.OrderBy(message => message.CreatedOn))
        .Limit(MESSAGE_LIMIT * roomIds.Length)
        .ToArray();

    public string GetLastGlobalRoomId(string accountId, params string[] roomIds) => mongo
        .Where(query => query
            .EqualTo(message => message.AccountId, accountId)
            .ContainedIn(message => message.RoomId, roomIds)
        )
        .Sort(sort => sort.OrderByDescending(message => message.CreatedOn))
        .Limit(1)
        .FirstOrDefault()
        ?.RoomId;

    public string[] GetRoomIdsForUnknownTypes(int pageNumber, out long remaining) => mongo
        .Where(query => query.EqualTo(message => message.Type, MessageType.Unassigned))
        .Limit(1_000)
        .Page(1_000, pageNumber, out remaining)
        .Select(message => message.RoomId)
        .Distinct()
        .ToArray();

    public long DeleteMessages(string roomId, int countToKeep = 0)
    {
        if (countToKeep <= 0)
            return mongo
                .Where(query => query.EqualTo(message => message.RoomId, roomId))
                .Delete();
        
        long timestamp = mongo
            .Where(query => query.EqualTo(message => message.RoomId, roomId))
            .Sort(sort => sort.OrderByDescending(message => message.CreatedOn))
            .Limit(countToKeep)
            .Project(message => message.CreatedOn)
            .LastOrDefault();

        if (timestamp <= 0)
            return 0;

        return mongo
            .Where(query => query
                .EqualTo(message => message.RoomId, roomId)
                .LessThan(message => message.CreatedOn, timestamp)
            )
            .Delete();
    }

    public long AssignType(string roomId, MessageType type) => mongo
        .Where(query => query
            .EqualTo(message => message.RoomId, roomId)
            .EqualTo(message => message.Type, MessageType.Unassigned)
        )
        .Update(update => update.Set(message => message.Type, type));

    public long DeleteExpiredMessages() => mongo
        .Where(query => query.LessThan(message => message.Expiration, Timestamp.Now))
        .Delete();
}