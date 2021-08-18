using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace Rumble.Platform.ChatService.Models
{
	public class Ban
	{
		private const string DB_KEY_ACCOUNT_ID = "aid";
		private const string DB_KEY_REASON = "why";
		private const string DB_KEY_EXPIRATION = "exp";
		private const string DB_KEY_ISSUED = "iss";
		private const string DB_KEY_SNAPSHOT = "snap";

		public const string FRIENDLY_KEY_ACCOUNT_ID = "accountId";
		public const string FRIENDLY_KEY_REASON = "reason";
		public const string FRIENDLY_KEY_ISSUED = "issued";
		public const string FRIENDLY_KEY_EXPIRATION = "expiration";
		public const string FRIENDLY_KEY_SNAPSHOT = "snapshot";
		public const string FRIENDLY_KEY_TIME_REMAINING = "timeRemaining";
		
		
		[BsonId, BsonRepresentation(BsonType.ObjectId)]
		public string Id { get; set; }
		[BsonElement(DB_KEY_ACCOUNT_ID)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_ACCOUNT_ID)]
		public string AccountId { get; private set; }
		[BsonElement(DB_KEY_REASON), BsonIgnoreIfNull]
		[JsonProperty(PropertyName = FRIENDLY_KEY_REASON)]
		private string Reason { get; set; }
		[BsonElement(DB_KEY_EXPIRATION), BsonIgnoreIfNull]
		[JsonProperty(PropertyName = FRIENDLY_KEY_EXPIRATION)]
		private long? Expiration { get; set; }

		[BsonIgnore]
		[Newtonsoft.Json.JsonIgnore]
		public DateTime ExpirationDate => Expiration == null ? DateTime.MaxValue : DateTime.UnixEpoch.AddSeconds((double)Expiration);
		[BsonElement(DB_KEY_ISSUED)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_ISSUED)]
		private long IssuedOn { get; set; }
		private DateTime IssuedOnDate => DateTime.UnixEpoch.AddSeconds((double)IssuedOn);
		[BsonElement(DB_KEY_SNAPSHOT)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_SNAPSHOT)]
		private Room[] Snapshot { get; set; }

		[BsonIgnore]
		[JsonProperty(PropertyName = FRIENDLY_KEY_TIME_REMAINING)]
		public string TimeRemaining
		{
			get
			{
				DateTime exp = ExpirationDate;
				if (exp.Equals(DateTime.MaxValue))
					return "permanent";
				TimeSpan x = exp.Subtract(DateTime.UtcNow);
				return x.TotalMilliseconds < 0
					? "0h00m00s"
					: $"{x.Hours}h{x.Minutes.ToString().PadLeft(2, '0')}m{x.Seconds.ToString().PadLeft(2, '0')}s";
			}
		}
		
		[BsonIgnore]
		[Newtonsoft.Json.JsonIgnore]
		public bool IsExpired => ExpirationDate.Subtract(DateTime.UtcNow).TotalMilliseconds <= 0;
		
		[BsonIgnore]
		[Newtonsoft.Json.JsonIgnore]
		public object ResponseObject => new { Ban = this };
		

		public Ban(string accountId, string reason, long? expiration, IEnumerable<Room> rooms)
		{
			AccountId = accountId;
			Reason = reason;
			IssuedOn = DateTimeOffset.Now.ToUnixTimeSeconds();
			Expiration = expiration;
			Snapshot = rooms.ToArray();
		}

		public static object GenerateResponseFrom(IEnumerable<Ban> bans)
		{
			return new { Bans = bans };
		} // TODO: update playerinfo
	}
}