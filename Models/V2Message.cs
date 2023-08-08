using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using Rumble.Platform.Data;
using Rumble.Platform.Data.Utilities;

namespace Rumble.Platform.ChatService.Models;

public class V2Message : PlatformCollectionDocument
{
	internal const string DB_KEY_ACCOUNT_ID   = "aid";
	internal const string DB_KEY_DATA         = "d";
	internal const string DB_KEY_TEXT         = "txt";
	internal const string DB_KEY_TIMESTAMP    = "ts";
	internal const string DB_KEY_TYPE         = "mt";
	internal const string DB_KEY_EXPIRATION   = "exp";
	internal const string DB_KEY_VISIBLE_FROM = "vf";

	public const string FRIENDLY_KEY_ACCOUNT_ID          = "aid";
	public const string FRIENDLY_KEY_DATA                = "data";
	public const string FRIENDLY_KEY_TEXT                = "text";
	public const string FRIENDLY_KEY_TIMESTAMP           = "timestamp";
	public const string FRIENDLY_KEY_TYPE                = "type";
	public const string FRIENDLY_KEY_EXPIRATION          = "expiration";
	public const string FRIENDLY_KEY_VISIBLE_FROM        = "visibleFrom";
	public const string FRIENDLY_KEY_DURATION_IN_SECONDS = "durationInSeconds";

	#region MONGO
	[BsonElement(DB_KEY_ACCOUNT_ID)]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_ACCOUNT_ID)]
	public string AccountId { get; set; }
	
	[BsonElement(DB_KEY_DATA), BsonIgnoreIfNull]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_DATA), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public RumbleJson Data { get; set; }

	[BsonElement(DB_KEY_TEXT)]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_TEXT), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string Text { get; set; }
	
	[BsonElement(DB_KEY_TIMESTAMP)]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_TIMESTAMP)]
	public long Timestamp { get; set; }
	
	[BsonElement(DB_KEY_EXPIRATION), BsonIgnoreIfNull]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_EXPIRATION), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public long? Expiration { get; set; }
	
	[BsonElement(DB_KEY_VISIBLE_FROM), BsonIgnoreIfNull]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_VISIBLE_FROM), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public long? VisibleFrom { get; set; }
	
	[BsonIgnore]
	[JsonIgnore]
	public bool IsExpired => Expiration != null && Rumble.Platform.Common.Utilities.Timestamp.UnixTime > Expiration;

	public enum V2MessageType
	{
		Chat,
		Broadcast,
		Activity,
		Announcement,
		Notification,
		Sticky,
		StickyArchived,
		PvpChallenge,
		Unknown
	}
	[BsonElement(DB_KEY_TYPE)]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_TYPE)]
	public V2MessageType Type { get; set; }
	#endregion MONGO
	
	#region INTERNAL
	[BsonIgnore]
	[JsonIgnore]
	public DateTime Date => DateTime.UnixEpoch.AddSeconds(Timestamp);	
	#endregion INTERNAL

	public V2Message()
	{
		Type = V2MessageType.Chat;
		Timestamp = Rumble.Platform.Common.Utilities.Timestamp.UnixTime;
	}

	internal static V2Message FromGeneric(RumbleJson input, string accountId)
	{
		long? startTime = input.Optional<long?>(FRIENDLY_KEY_VISIBLE_FROM);
		long? duration = input.Optional<long?>(FRIENDLY_KEY_DURATION_IN_SECONDS);
		long? expiration = duration != null
			                   ? (startTime ?? Rumble.Platform.Common.Utilities.Timestamp.UnixTime) + duration
			                   : input.Optional<long?>(FRIENDLY_KEY_EXPIRATION);

		return new V2Message
		{
			Id = Guid.NewGuid().ToString(),
			Text = input.Optional<string>(FRIENDLY_KEY_TEXT),
			Timestamp = Rumble.Platform.Common.Utilities.Timestamp.UnixTime,
			Type = V2MessageType.Chat,
			AccountId = accountId,
			Data = input.Optional<RumbleJson>(FRIENDLY_KEY_DATA)
		};
	}

	/// <summary>
	/// Parses a JToken coming from a request to instantiate a new Message.
	/// </summary>
	/// <param name="input">The JToken corresponding to a message object.</param>
	/// <param name="accountId">The account ID for the author.  This should always come from the TokenInfo object.</param>
	/// <returns>A new Message object.</returns>
	internal static V2Message FromJsonElement(JsonElement input, string accountId)
	{
		long? startTime = JsonHelper.Optional<long?>(input, FRIENDLY_KEY_VISIBLE_FROM);
		long? duration = JsonHelper.Optional<long?>(input, FRIENDLY_KEY_DURATION_IN_SECONDS);
		long? expiration = duration != null
			                   ? (startTime ?? Rumble.Platform.Common.Utilities.Timestamp.UnixTime) + duration
			                   : JsonHelper.Optional<long?>(input, FRIENDLY_KEY_EXPIRATION);
		
		return new V2Message
		{
			Id = Guid.NewGuid().ToString(),
			Text = JsonHelper.Optional<string>(input, FRIENDLY_KEY_TEXT),
			Timestamp = Rumble.Platform.Common.Utilities.Timestamp.UnixTime,
			Type = V2MessageType.Chat,
			AccountId = accountId
		};
	}

	internal V2Message Validate()
	{
		if (Text == null)
		{
			throw new ArgumentNullException("Text", $"'{FRIENDLY_KEY_TEXT}' cannot be null.");
		}

		if (AccountId == null)
		{
			throw new ArgumentNullException("UserInfo", $"'{FRIENDLY_KEY_ACCOUNT_ID}' cannot be null");
		}

		return this;
	}
	
	public static object GenerateStickyResponseFrom(IEnumerable<V2Message> stickies) => new { Stickies = stickies };
}