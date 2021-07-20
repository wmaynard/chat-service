using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Rumble.Platform.ChatService.Models
{
	public class Report
	{
		public const int COUNT_MESSAGES_FOR_REPORT = 25;
		public const int COUNT_MESSAGES_AFTER_REPORTED = 5;
		
		public const string KEY_ID = "id";
		public const string KEY_TIMESTAMP = "time";
		public const string KEY_REPORTER = "reporter";
		public const string KEY_REASON = "reason";
		public const string KEY_MESSAGE_LOG = "log";
		public const string KEY_PLAYERS = "players";
		public const string KEY_STATUS = "status";

		public const string STATUS_BENIGN = "ignored";
		public const string STATUS_BANNED = "banned";
		public const string STATUS_UNADDRESSED = "new";
		
		[BsonId, BsonRepresentation(BsonType.ObjectId)]
		public string Id { get; set; }
		[BsonElement(KEY_TIMESTAMP)]
		public long Timestamp { get; set; }
		[BsonElement(KEY_REPORTER)]
		public PlayerInfo Reporter { get; set; }
		[BsonElement(KEY_REASON), BsonIgnoreIfNull]
		public string Reason { get; set; }
		[BsonElement(KEY_MESSAGE_LOG)]
		public IEnumerable<Message> Log { get; set; }
		[BsonElement(KEY_PLAYERS)]
		public IEnumerable<PlayerInfo> Players { get; set; }
		[BsonElement(KEY_STATUS)]
		public string Status { get; set; }

		public Report()
		{
			Timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
			Status = STATUS_UNADDRESSED;
		}
	}
}