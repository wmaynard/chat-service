using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace Rumble.Platform.ChatService.Models
{
	public class Report
	{
		public const int COUNT_MESSAGES_BEFORE_REPORTED = 25;
		public const int COUNT_MESSAGES_AFTER_REPORTED = 5;

		private const string DB_KEY_TIMESTAMP = "ts";
		private const string DB_KEY_REPORTER = "rptr";
		private const string DB_KEY_REASON = "why";
		private const string DB_KEY_MESSAGE_LOG = "log";
		private const string DB_KEY_PLAYERS = "who";
		private const string DB_KEY_STATUS = "st";
		
		public const string FRIENDLY_KEY_TIMESTAMP = "time";
		public const string FRIENDLY_KEY_REPORTER = "reporter";
		public const string FRIENDLY_KEY_REASON = "reason";
		public const string FRIENDLY_KEY_MESSAGE_LOG = "log";
		public const string FRIENDLY_KEY_PLAYERS = "players";
		public const string FRIENDLY_KEY_STATUS = "status";

		public const string STATUS_BENIGN = "ignored";
		public const string STATUS_BANNED = "banned";
		public const string STATUS_UNADDRESSED = "new";
		
		[BsonId, BsonRepresentation(BsonType.ObjectId)]
		public string Id { get; set; }
		[BsonElement(DB_KEY_TIMESTAMP)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_TIMESTAMP)]
		public long Timestamp { get; set; }
		[BsonElement(DB_KEY_REPORTER)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_REPORTER)]
		public PlayerInfo Reporter { get; set; }
		[BsonElement(DB_KEY_REASON), BsonIgnoreIfNull]
		[JsonProperty(PropertyName = FRIENDLY_KEY_REASON, NullValueHandling = NullValueHandling.Ignore)]
		public string Reason { get; set; }
		[BsonElement(DB_KEY_MESSAGE_LOG)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_MESSAGE_LOG)]
		public IEnumerable<Message> Log { get; set; }
		[BsonElement(DB_KEY_PLAYERS)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_PLAYERS)]
		public IEnumerable<PlayerInfo> Players { get; set; }
		[BsonElement(DB_KEY_STATUS)]
		[JsonProperty(FRIENDLY_KEY_STATUS)]
		public string Status { get; set; }
		[BsonIgnore]
		[Newtonsoft.Json.JsonIgnore]
		public object ResponseObject => new { Report = this };

		public Report()
		{
			Timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
			Status = STATUS_UNADDRESSED;
		}
	}
}