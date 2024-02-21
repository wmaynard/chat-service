using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using MongoDB.Bson.Serialization.Attributes;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Exceptions;
using Rumble.Platform.Common.Extensions;
using Rumble.Platform.Common.Interfaces;
using Rumble.Platform.Common.Models;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Data;
using StackExchange.Redis;

namespace Rumble.Platform.ChatService.Models;

public class Message : PlatformCollectionDocument, ISearchable<Message>
{
    public const string FRIENDLY_KEY_DATA = "context";
    public static long StandardMessageExpiration => Timestamp.TwoWeeksFromNow;
    public static long DirectMessageExpiration => Timestamp.OneMonthFromNow;
    public static long PrivateMessageExpiration => Timestamp.ThreeMonthsFromNow;
    
    [BsonElement(TokenInfo.DB_KEY_ACCOUNT_ID)]
    [JsonPropertyName(TokenInfo.FRIENDLY_KEY_ACCOUNT_ID)]
    public string AccountId { get; set; }
    
    [BsonElement("editor"), BsonIgnoreIfNull]
    [JsonIgnore]
    public TokenInfo Administrator { get; set; }
    
    [BsonElement("body")]
    [JsonPropertyName("text")]
    public string Body { get; set; }
    
    [BsonElement("data"), BsonIgnoreIfNull]
    [JsonPropertyName(FRIENDLY_KEY_DATA), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RumbleJson Data { get; set; }
    
    [BsonElement("exp"), BsonIgnoreIfDefault]
    [JsonPropertyName("expiration")]
    public long Expiration { get; set; }
    
    [BsonElement("room")]
    [JsonPropertyName("roomId"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string RoomId { get; set; }
    
    [BsonElement("type")]
    [JsonIgnore]
    public MessageType Type { get; set; }
    
    [BsonIgnore]
    [JsonPropertyName("channel"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public BroadcastChannel Channel { get; set; }

    public Message()
    {
        Expiration = Timestamp.TwoWeeksFromNow;
    }

    protected override void Validate(out List<string> errors)
    {
        errors = new List<string>();
        
        if (string.IsNullOrWhiteSpace(Body) && (Data == null || !Data.Keys.Any()))
            errors.Add($"Messages can be blank, but if this is the case, you must provide '{FRIENDLY_KEY_DATA}'.");
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

    public Message EnforceRoomIdIsValid()
    {
        if (string.IsNullOrWhiteSpace(RoomId) || !RoomId.CanBeMongoId())
            throw new PlatformException("Message must have a valid roomId");
        return this;
    }

    public Dictionary<Expression<Func<Message, object>>, int> DefineSearchWeights()
    {
        return new Dictionary<Expression<Func<Message, object>>, int>()
        {
            { message => message.AccountId, 100 },
            { message => message.Body, 10 },
            { message => message.RoomId, 1 }
        };
    }

    public long SearchWeight { get; set; }
    public double SearchConfidence { get; set; }

    public bool ContentIsEqualTo(object obj) => obj is Message message 
        && Body == message.Body 
        && RoomId == message.RoomId 
        && Expiration == message.Expiration 
        && Data?.ToJson() == message.Data?.ToJson();
}