using System.Collections.Generic;
using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Models;
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