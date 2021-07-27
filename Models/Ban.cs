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
		private string Reason { get; set; }
		private long? Expiration { get; set; }
		private long IssuedOn { get; set; }
		private Room[] Snapshot { get; set; }
		
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
		}
	}
}