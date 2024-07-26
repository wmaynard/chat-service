using System.Text.Json.Serialization;
using Amazon.Auth.AccessControlPolicy;
using MongoDB.Bson.Serialization.Attributes;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Extensions;
using Rumble.Platform.Common.Models;
using Rumble.Platform.Common.Utilities.JsonTools;

namespace Rumble.Platform.ChatService.Models;

public class Room : PlatformCollectionDocument
{
    public const int CAPACITY_MEMBERS = 100;
    public const int CAPACITY_OVERFLOW = 150;
    
    [BsonElement("data"), BsonIgnoreIfNull]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RumbleJson Data { get; set; }
    
    [BsonElement("editor"), BsonIgnoreIfNull]
    [JsonIgnore]
    public TokenInfo Editor { get; set; }
    
    [BsonElement("globalId"), BsonIgnoreIfDefault]
    [JsonPropertyName("number"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long FriendlyId { get; set; }
    
    [BsonElement("who")]
    [JsonPropertyName("members")]
    public string[] Members { get; set; }
    
    [BsonElement("updated")]
    [JsonIgnore]
    public long MembershipUpdatedMs { get; set; }
    
    [BsonIgnore]
    [JsonPropertyName("unread")]
    public Message[] Messages { get; set; }
    
    [BsonElement("type")]
    [JsonIgnore]
    public RoomType Type { get; set; }
    
    [BsonElement("channel")]
    [JsonPropertyName("channel")]
    public BroadcastChannel Channel { get; set; }

    [BsonIgnore]
    [JsonPropertyName("type")]
    public string VerboseType => Type.GetDisplayName();

    public Room PruneData()
    {
        Data = null;
        return this;
    }
}