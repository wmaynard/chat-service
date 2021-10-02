using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Models
{
	public class Message : PlatformDataModel
	{
		public const string TYPE_ACTIVITY = "activity";
		public const string TYPE_CHAT = "chat";
		public const string TYPE_NOTIFICATION = "notification";
		public const string TYPE_ANNOUNCEMENT = "announcement";
		public const string TYPE_STICKY_ARCHIVED = "archived";
		public const string TYPE_BAN_ANNOUNCEMENT = "banAnnouncement";
		public const string TYPE_UNBAN_ANNOUNCEMENT = "unbanAnnouncement";
		public const string TYPE_UNKNOWN = "unknown";
		public const string TYPE_BROADCAST = "broadcast";
		public const string TYPE_STICKY = "sticky";

		internal const string DB_KEY_ID = "id";
		internal const string DB_KEY_DATA = "d";
		internal const string DB_KEY_TEXT = "txt";
		internal const string DB_KEY_TIMESTAMP = "ts";
		internal const string DB_KEY_TYPE = "mt";
		internal const string DB_KEY_ACCOUNT_ID = "aid";
		internal const string DB_KEY_REPORTED = "bad";
		internal const string DB_KEY_VISIBLE_FROM = "vf";
		internal const string DB_KEY_EXPIRATION = "exp";

		public const string FRIENDLY_KEY_ID = "id";
		public const string FRIENDLY_KEY_DATA = "data";
		public const string FRIENDLY_KEY_TEXT = "text";
		public const string FRIENDLY_KEY_TIMESTAMP = "timestamp";
		public const string FRIENDLY_KEY_TYPE = "type";
		public const string FRIENDLY_KEY_ACCOUNT_ID = "aid";
		public const string FRIENDLY_KEY_REPORTED = "flagged";
		public const string FRIENDLY_KEY_VISIBLE_FROM = "visibleFrom";
		public const string FRIENDLY_KEY_EXPIRATION = "expiration";
		public const string FRIENDLY_KEY_DURATION_IN_SECONDS = "durationInSeconds";

		[BsonElement(DB_KEY_ID)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_ID)]
		public string Id { get; set; }
		[BsonElement(DB_KEY_DATA), BsonIgnoreIfNull]
		[JsonProperty(PropertyName = FRIENDLY_KEY_DATA, NullValueHandling = NullValueHandling.Ignore)]
		// TODO: When this gets serialized going to Mongo, there's a bunch of garbage that screws up future retrieval.
		public dynamic Data { get; set; }
		[BsonElement(DB_KEY_TEXT)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_TEXT, NullValueHandling = NullValueHandling.Ignore)]
		public string Text { get; set; }
		[BsonElement(DB_KEY_TIMESTAMP)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_TIMESTAMP)]
		public long Timestamp { get; set; }
		[BsonElement(DB_KEY_TYPE)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_TYPE)]
		public string Type { get; set; }
		[BsonElement(DB_KEY_ACCOUNT_ID)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_ACCOUNT_ID)]
		public string AccountId { get; set; }
		[BsonElement(DB_KEY_REPORTED), BsonIgnoreIfNull]
		[JsonProperty(PropertyName = FRIENDLY_KEY_REPORTED, NullValueHandling = NullValueHandling.Ignore)]
		public bool? Reported { get; set; }
		[BsonElement(DB_KEY_VISIBLE_FROM), BsonIgnoreIfNull]
		[JsonProperty(PropertyName = FRIENDLY_KEY_VISIBLE_FROM, NullValueHandling = NullValueHandling.Ignore)]
		public long? VisibleFrom { get; set; }
		[BsonElement(DB_KEY_EXPIRATION), BsonIgnoreIfNull]
		[JsonProperty(PropertyName = FRIENDLY_KEY_EXPIRATION, NullValueHandling = NullValueHandling.Ignore)]
		public long? Expiration { get; set; }
		[BsonIgnore]
		[JsonIgnore]
		public DateTime Date => DateTime.UnixEpoch.AddSeconds(Timestamp);

		[BsonIgnore]
		[JsonIgnore]
		public bool IsSticky => Type == Message.TYPE_STICKY;

		[BsonIgnore]
		[JsonIgnore]
		public bool IsExpired => Expiration != null && UnixTime > Expiration;
		public Message()
		{
			Id = Guid.NewGuid().ToString();
			Type = TYPE_CHAT;
			Timestamp = UnixTime;
		}

		/// <summary>
		/// Parses a JToken coming from a request to instantiate a new Message.
		/// </summary>
		/// <param name="input">The JToken corresponding to a message object.</param>
		/// <param name="accountId">The account ID for the author.  This should always come from the Rumble.Controller.TokenInfo object.</param>
		/// <returns>A new Message object.</returns>
		internal static Message FromJToken(JToken input, string accountId)
		{
			long? startTime = input[FRIENDLY_KEY_VISIBLE_FROM]?.ToObject<long?>();
			long? durationInSeconds = input[FRIENDLY_KEY_DURATION_IN_SECONDS]?.ToObject<long?>();
			long? expiration = durationInSeconds != null
				? (startTime ?? UnixTime) + durationInSeconds
				: input[FRIENDLY_KEY_EXPIRATION]?.ToObject<long?>();
			JToken data = input[FRIENDLY_KEY_DATA];
			
			return new Message()
			{
				Id = Guid.NewGuid().ToString(),
				// Data = BsonDocument.Parse(input[FRIENDLY_KEY_DATA].ToString()),
				// Data = input[FRIENDLY_KEY_DATA]?.ToObject<object>(),
				Text = input[FRIENDLY_KEY_TEXT]?.ToObject<string>(),
				Timestamp = UnixTime,
				Type = TYPE_CHAT,
				VisibleFrom = startTime,
				Expiration = expiration,
				AccountId = accountId
				// Data = data
			};
		}

		internal Message Validate()
		{
			if (Text == null)
				throw new ArgumentNullException("Text", $"'{FRIENDLY_KEY_TEXT}' cannot be null.");
			if (AccountId == null)
				throw new ArgumentNullException("UserInfo", $"'{FRIENDLY_KEY_ACCOUNT_ID}' cannot be null");
			return this;
		}

		public static object GenerateStickyResponseFrom(IEnumerable<Message> stickies)
		{
			return new { Stickies = stickies };
		}
	}
}