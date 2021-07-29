using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using Newtonsoft.Json;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Settings;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Services
{
	// TODO: RumbleMongoService
	// TODO: RumbleMongoModel
	// TODO: Revoke token when banned to force client to update
	
	public class BanService : RumbleMongoService
	{
		private new readonly IMongoCollection<Ban> _collection;

		// public override bool IsHealthy => IsConnected || Open();

		public BanService(BanDBSettings settings) : base(settings)
		{
			_collection = _database.GetCollection<Ban>(settings.CollectionName);
		}

		public IEnumerable<Ban> List() => _collection.Find((b => true)).ToList();
		public IEnumerable<Ban> GetBansForUser(string accountId) => _collection.Find(b => b.AccountId == accountId).ToList();
		public void Create(Ban ban) => _collection.InsertOne(document: ban);
		public void Update(Ban ban) => _collection.ReplaceOne(filter: b => b.Id == ban.Id, replacement: ban);
		public void Remove(string id) => _collection.DeleteOne(filter: b => b.Id == id);
	}
}