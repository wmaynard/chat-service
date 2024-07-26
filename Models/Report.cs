using System;
using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Extensions;
using Rumble.Platform.Common.Models;
using Rumble.Platform.Common.Utilities.JsonTools;
using StackExchange.Redis;

namespace Rumble.Platform.ChatService.Models;

public class Report : PlatformCollectionDocument
{
    [BsonElement("note"), BsonIgnoreIfNull]
    [JsonPropertyName("resolutionNote")]
    public string AdminNote { get; set; }
    
    [BsonElement("reporter")]
    [JsonPropertyName("reporterId")]
    public string FirstReporterId { get; set; }
    
    [BsonElement("log")]
    [JsonPropertyName("messageLog")]
    public Message[] MessageLog { get; set; }
    
    [BsonElement("message")]
    [JsonPropertyName("messageId")]
    public string OffendingMessageId { get; set; }
    
    [BsonElement("count")]
    [JsonPropertyName("timesReported")]
    public int ReportedCount { get; set; }
    
    [BsonElement("others")]
    [JsonPropertyName("otherReporterIds")]
    public string[] ReporterIds { get; set; }
    
    [BsonElement("editor"), BsonIgnoreIfDefault]
    [JsonPropertyName("reviewer"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TokenInfo Reviewer { get; set; }
    
    [BsonElement("room")]
    [JsonPropertyName("roomId")]
    public string RoomId { get; set; }
    
    [BsonElement("status")]
    [JsonPropertyName("status")]
    public ReportStatus Status { get; set; }

    [BsonIgnore]
    [JsonPropertyName("availableStatuses")]
    public RumbleJson AvailableStatuses
    {
        get
        {
            RumbleJson output = new();
            foreach (ReportStatus status in Enum.GetValues(typeof(ReportStatus)))
                output[status.GetDisplayName()] = (int) status;
            return output;
        }
    }
}