using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Models
{
	public class Ban : PlatformCollectionDocument
	{
		internal const string DB_KEY_ACCOUNT_ID = "aid";
		internal const string DB_KEY_EXPIRATION = "exp";
		internal const string DB_KEY_ISSUED = "iss";
		internal const string DB_KEY_REASON = "why";
		internal const string DB_KEY_SNAPSHOT = "snap";

		public const string FRIENDLY_KEY_ACCOUNT_ID = "accountId";
		public const string FRIENDLY_KEY_EXPIRATION = "expiration";
		public const string FRIENDLY_KEY_ISSUED = "issued";
		public const string FRIENDLY_KEY_REASON = "reason";
		public const string FRIENDLY_KEY_SNAPSHOT = "snapshot";
		public const string FRIENDLY_KEY_TIME_REMAINING = "timeRemaining";
		
		#region MONGO
		[BsonElement(DB_KEY_ACCOUNT_ID)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_ACCOUNT_ID)]
		public string AccountId { get; private set; }
		
		[BsonElement(DB_KEY_EXPIRATION), BsonIgnoreIfNull]
		[JsonProperty(PropertyName = FRIENDLY_KEY_EXPIRATION, NullValueHandling = NullValueHandling.Ignore)]
		private long? Expiration { get; set; }
		
		[BsonElement(DB_KEY_ISSUED)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_ISSUED)]
		private long IssuedOn { get; set; }
		
		[BsonElement(DB_KEY_REASON), BsonIgnoreIfNull]
		[JsonProperty(PropertyName = FRIENDLY_KEY_REASON)]
		private string Reason { get; set; }
		
		[BsonElement(DB_KEY_SNAPSHOT)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_SNAPSHOT, NullValueHandling = NullValueHandling.Ignore)]
		private Room[] Snapshot { get; set; }
		#endregion MONGO

		#region CLIENT
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
		#endregion
		
		#region INTERNAL
		[BsonIgnore]
		[JsonIgnore]
		public DateTime ExpirationDate => Expiration == null ? DateTime.MaxValue : DateTime.UnixEpoch.AddSeconds((double)Expiration);
		
		[BsonIgnore]
		[JsonIgnore]
		public bool IsExpired => ExpirationDate.Subtract(DateTime.UtcNow).TotalMilliseconds <= 0;
		
		[BsonIgnore]
		[JsonIgnore]
		private DateTime IssuedOnDate => DateTime.UnixEpoch.AddSeconds((double)IssuedOn);
		#endregion INTERNAL

		public Ban(string accountId, string reason, long? expiration, IEnumerable<Room> rooms)
		{
			AccountId = accountId;
			Reason = reason;
			IssuedOn = UnixTime;
			Expiration = expiration;
			Snapshot = rooms.ToArray();
		}

		public void PurgeSnapshot()
		{
			Snapshot = null;
		}
	}
}