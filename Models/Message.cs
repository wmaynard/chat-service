using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json.Linq;

namespace Rumble.Platform.ChatService.Models
{
	public class Message
	{
		// TODO: Expires tag for sticky?
		// TODO: Don't remove sticky
		// TODO: Type should probably be enforced client-side
		public const string TYPE_ACTIVITY = "activity";
		public const string TYPE_CHAT = "chat";
		public const string TYPE_ANNOUNCEMENT = "announcement";
		public const string TYPE_UNKNOWN = "unknown";

		public const string KEY_ID = "id";
		public const string KEY_IS_STICKY = "isSticky";
		public const string KEY_TEXT = "text";
		public const string KEY_TIMESTAMP = "timestamp";
		public const string KEY_TYPE = "type";
		public const string KEY_AID = "aid";
		
		[BsonElement(KEY_ID)]
		public string Id { get; set; }
		[BsonElement(KEY_IS_STICKY), BsonIgnoreIfNull]
		public bool IsSticky { get; set; }
		[BsonElement(KEY_TEXT)]
		public string Text { get; set; }
		[BsonElement(KEY_TIMESTAMP)]
		public long Timestamp { get; set; }
		[BsonElement(KEY_TYPE)]
		public string Type { get; set; }
		[BsonElement(KEY_AID)]
		public string AccountId { get; set; }

		public Message()
		{
			Type = TYPE_CHAT;
			Timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
			IsSticky = false;
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
				IsSticky = input[KEY_IS_STICKY]?.ToObject<bool>() ?? false,
				Text = input[KEY_TEXT]?.ToObject<string>(),
				Timestamp = input[KEY_TIMESTAMP]?.ToObject<long>() ?? DateTimeOffset.Now.ToUnixTimeSeconds(),
				Type = input[KEY_TYPE]?.ToObject<string>() ?? TYPE_CHAT,
				AccountId = accountId
			};
		}

		internal Message Validate()
		{
			if (Text == null)
				throw new ArgumentNullException("Text", $"'{KEY_TEXT}' cannot be null.");
			if (AccountId == null)
				throw new ArgumentNullException("UserInfo", $"'{KEY_AID}' cannot be null");
			if (AccountId == null)
				throw new ArgumentNullException("UserInfo.AccountId", $"'{KEY_AID}.{PlayerInfo.KEY_ACCOUNT_ID}' cannot be null.");
			return this;
		}
	}
}