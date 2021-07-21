using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Newtonsoft.Json;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Settings;

namespace Rumble.Platform.ChatService.Services
{
	// TODO: RumbleMongoService
	// TODO: RumbleMongoModel
	// TODO: Revoke token when banned to force client to update
	
	public class BanService
	{
		private readonly IMongoCollection<Ban> _collection;

		public BanService(BanDBSettings settings)
		{
			MongoClient client = new MongoClient(settings.ConnectionString);
			IMongoDatabase db = client.GetDatabase(settings.DatabaseName);
			_collection = db.GetCollection<Ban>(settings.CollectionName);
		}

		public IEnumerable<Ban> GetBansForUser(string accountId) => _collection.Find(b => b.AccountId == accountId).ToList();
		public void Create(Ban ban) => _collection.InsertOne(document: ban);
		public void Update(Ban ban) => _collection.ReplaceOne(filter: b => b.Id == ban.Id, replacement: ban);
		public void Remove(string id) => _collection.DeleteOne(filter: b => b.Id == id);
	}

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