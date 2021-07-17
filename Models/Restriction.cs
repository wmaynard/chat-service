using System;
using System.Collections;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Rumble.Platform.ChatService.Models
{
	public class Report
	{
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
	public class Restriction
	{
		public const string KEY_ACCOUNT_ID = "accountID";
		public const string KEY_MUTED_BY = "mutedBy";
		// public const string KEY_RATE_LIMIT = "rateLimit";
		public const string KEY_SQUELCH = "squelch";

		[BsonId, BsonRepresentation(BsonType.ObjectId)]
		public string Id { get; set; }

		[BsonElement(KEY_ACCOUNT_ID)]
		public string AccountId { get; set; }
		
		[BsonElement(KEY_MUTED_BY)]
		public string[] MutedBy { get; set; }
		
		[BsonElement(KEY_SQUELCH)]
		public Squelch Squelch { get; set; }
	}

	public class Squelch
	{
		// user/squelch
		// user/ban
		// user/mute
		// user/report
		// chat/launch?
		
		public const string KEY_REASON = "reason";
		public const string KEY_EXPIRATION = "expiration";
		public const string KEY_ISSUED = "issued";
		public const string KEY_SNAPSHOT = "snapshot";
		
		[BsonElement(KEY_REASON)]
		public string Reason { get; set; }
		[BsonElement(KEY_EXPIRATION)]
		public long Expiration { get; set; }
		[BsonElement(KEY_ISSUED)]
		public long Issued { get; set; }
		[BsonElement(KEY_SNAPSHOT)]
		public Room[] Snapshot { get; set; }

		public Squelch()
		{
			Issued = DateTimeOffset.Now.ToUnixTimeSeconds();
		}
	}
}