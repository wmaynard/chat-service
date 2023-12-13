using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using Rumble.Platform.Common.Exceptions;
using Rumble.Platform.Common.Minq;
using Rumble.Platform.Common.Models;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Models;

public class Message : PlatformCollectionDocument
{
    [BsonElement(TokenInfo.DB_KEY_ACCOUNT_ID)]
    [JsonPropertyName(TokenInfo.FRIENDLY_KEY_ACCOUNT_ID)]
    public string AccountId { get; set; }
    
    [BsonElement("body")]
    [JsonPropertyName("text")]
    public string Body { get; set; }
    
    [BsonElement("data"), BsonIgnoreIfNull]
    [JsonPropertyName("context"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RumbleJson Data { get; set; }
    
    [BsonElement("exp"), BsonIgnoreIfDefault]
    [JsonIgnore]
    public long Expiration { get; set; }
    
    [BsonElement("room")]
    [JsonPropertyName("roomId"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string RoomId { get; set; }
    
    [BsonElement("type"), BsonIgnoreIfDefault]
    [JsonIgnore]
    public MessageType Type { get; set; }

    protected override void Validate(out List<string> errors)
    {
        errors = new List<string>();
        
        if (string.IsNullOrWhiteSpace(Body))
            errors.Add("Messages must have content."); // TODO: Add key to this
        if (Type != MessageType.Unassigned)
            errors.Add("Sending a message with an explicit message type is not allowed, it is server-authoritative.");
    }

    /// <summary>
    /// Removes unnecessary data; useful for when returning information to the game client or when creating reports
    /// that don't need certain datapoints.
    /// </summary>
    /// <returns></returns>
    public Message Prune()
    {
        RoomId = null;
        Type = MessageType.Unassigned;
        Expiration = 0;
        
        return this;
    }
}

public enum MessageType
{
    Unassigned = 0,
    Global = 1,
    Private = 10,
    Administrator = 1000,
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
        .Or(or => or.EqualTo(message => message.Type, MessageType.Administrator))
        .Sort(sort => sort
            .OrderByDescending(message => message.Type)
            .OrderBy(message => message.CreatedOn)
        )
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

    public Message[] GetContextAround(string messageId)
    {
        Message target = mongo.ExactId(messageId).FirstOrDefault();

        if (target == null)
            throw new PlatformException("Unable to fetch context");

        Message[] before = mongo
            .Where(query => query
                .EqualTo(message => message.RoomId, target.RoomId)
                .LessThanOrEqualTo(message => message.CreatedOn, target.CreatedOn)
            )
            .Sort(sort => sort.OrderByDescending(message => message.CreatedOn))
            .Limit(25)
            .ToArray();
        
        Message[] after = mongo
            .Where(query => query
                .EqualTo(message => message.RoomId, target.RoomId)
                .GreaterThanOrEqualTo(message => message.CreatedOn, target.CreatedOn)
            )
            .Sort(sort => sort.OrderBy(message => message.CreatedOn))
            .Limit(25)
            .ToArray();

        return before
            .Union(after)
            .DistinctBy(message => message.Id)
            .OrderBy(message => message.CreatedOn)
            .ToArray();
    }
}