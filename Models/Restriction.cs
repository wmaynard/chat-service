using System;
using System.Collections;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Rumble.Platform.ChatService.Models
{
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