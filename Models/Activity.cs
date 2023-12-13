using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using Rumble.Platform.Common.Models;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Models;

public class Activity : PlatformCollectionDocument
{
    [BsonElement(TokenInfo.DB_KEY_ACCOUNT_ID)]
    [JsonIgnore]
    public string AccountId { get; set; }
    
    [BsonElement("active")]
    [JsonIgnore]
    public bool IsActive { get; set; }
    
    [BsonElement("updated")]
    [JsonIgnore]
    public long LastActive { get; set; } // Do we need a MarkedInactive timestamp?
}