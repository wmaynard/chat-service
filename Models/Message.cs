using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Rumble.Platform.ChatService.Models
{
	public class Message
	{
		public const string TYPE_ACTIVITY = "activity";
		public const string TYPE_CHAT = "chat";
		public const string TYPE_ANNOUNCEMENT = "announcement";
		public const string TYPE_UNKNOWN = "unknown";
		public const string TYPE_BROADCAST = "broadcast";

		private const string DB_KEY_ID = "id";
		private const string DB_KEY_TEXT = "txt";
		private const string DB_KEY_TIMESTAMP = "ts";
		private const string DB_KEY_TYPE = "mt";
		private const string DB_KEY_ACCOUNT_ID = "aid";
		private const string DB_KEY_REPORTED = "bad";
		private const string DB_KEY_VISIBLE_FROM = "vf";
		private const string DB_KEY_EXPIRATION = "exp";

		public const string FRIENDLY_KEY_ID = "id";
		public const string FRIENDLY_KEY_TEXT = "text";
		public const string FRIENDLY_KEY_TIMESTAMP = "timestamp";
		public const string FRIENDLY_KEY_TYPE = "type";
		public const string FRIENDLY_KEY_ACCOUNT_ID = "aid";
		public const string FRIENDLY_KEY_REPORTED = "flagged";
		public const string FRIENDLY_KEY_VISIBLE_FROM = "visibleFrom";
		public const string FRIENDLY_KEY_EXPIRATION = "expiration";

		[BsonElement(DB_KEY_ID)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_ID)]
		public string Id { get; set; }
		[BsonElement(DB_KEY_TEXT)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_TEXT)]
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
		public Message()
		{
			Type = TYPE_CHAT;
			Timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
		}

		/// <summary>
		/// Parses a JToken coming from a request to instantiate a new Message.
		/// </summary>
		/// <param name="input">The JToken corresponding to a message object.</param>
		/// <param name="accountId">The account ID for the author.  This should always come from the Rumble.Controller.TokenInfo object.</param>
		/// <returns>A new Message object.</returns>
		internal static Message FromJToken(JToken input, string accountId)
		{
			return new Message()
			{
				Id = Guid.NewGuid().ToString(),
				Text = input[FRIENDLY_KEY_TEXT]?.ToObject<string>(),
				Timestamp = input[FRIENDLY_KEY_TIMESTAMP]?.ToObject<long>() ?? DateTimeOffset.Now.ToUnixTimeSeconds(),
				Type = input[FRIENDLY_KEY_TYPE]?.ToObject<string>() ?? TYPE_CHAT,
				VisibleFrom = input[FRIENDLY_KEY_VISIBLE_FROM]?.ToObject<long?>(),
				Expiration = input[FRIENDLY_KEY_EXPIRATION]?.ToObject<long?>(),
				AccountId = accountId
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