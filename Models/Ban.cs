using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace Rumble.Platform.ChatService.Models
{
	public class Ban
	{
		[BsonId, BsonRepresentation(BsonType.ObjectId)]
		public string Id { get; set; }
		
		public string AccountId { get; private set; }
		[BsonElement("reason")]
		private string Reason { get; set; }
		[BsonElement("expiration")]
		private long? Expiration { get; set; }

		public DateTime ExpirationDate => Expiration == null ? DateTime.MaxValue : DateTime.UnixEpoch.AddSeconds((double)Expiration);
		[BsonElement("issued")]
		private long IssuedOn { get; set; }
		private DateTime IssuedOnDate => DateTime.UnixEpoch.AddSeconds((double)IssuedOn);
		[BsonElement("snapshot")]
		private Room[] Snapshot { get; set; }

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

		public bool IsExpired => ExpirationDate.Subtract(DateTime.UtcNow).TotalMilliseconds <= 0;
		
		[JsonIgnore]
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