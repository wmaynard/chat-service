using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Models;

public class Report : PlatformCollectionDocument
{
    [BsonElement("message")]
    [JsonPropertyName("messageId")]
    public string OffendingMessageId { get; set; }
    
    [BsonElement("reporter")]
    [JsonPropertyName("reporterId")]
    public string FirstReporterId { get; set; }
    
    [BsonElement("others")]
    [JsonPropertyName("otherReporterIds")]
    public string[] ReporterIds { get; set; }
    
    [BsonElement("count")]
    [JsonPropertyName("timesReported")]
    public int ReportedCount { get; set; }
    
    [BsonElement("log")]
    [JsonPropertyName("messageLog")]
    public Message[] Context { get; set; }
    
    [BsonElement("room")]
    [JsonPropertyName("roomId")]
    public string RoomId { get; set; }
    
    [BsonElement("status")]
    [JsonPropertyName("status")]
    public ReportStatus Status { get; set; }
    
    [BsonElement("note"), BsonIgnoreIfNull]
    [JsonPropertyName("resolutionNote")]
    public string AdminNote { get; set; }
}