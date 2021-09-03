using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Settings;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;
using Rumble.Platform.CSharp.Common.Interop;

namespace Rumble.Platform.ChatService.Services
{
	public class BanService : RumbleMongoService
	{
		private new readonly IMongoCollection<Ban> _collection;

		// public override bool IsHealthy => IsConnected || Open();

		public BanService(BanDBSettings settings) : base(settings)
		{
			Log.Verbose(Owner.Will, "Creating BanService");
			_collection = _database.GetCollection<Ban>(settings.CollectionName);
		}

		public IEnumerable<Ban> List() => _collection.Find((b => true)).ToList();
		public IEnumerable<Ban> GetBansForUser(string accountId) => _collection.Find(b => b.AccountId == accountId).ToList().Where(b => !b.IsExpired);
		public Ban Get(string id) => _collection.Find(b => b.Id == id).FirstOrDefault();
		public void Create(Ban ban) => _collection.InsertOne(document: ban);
		public void Update(Ban ban) => _collection.ReplaceOne(filter: b => b.Id == ban.Id, replacement: ban);
		public void Remove(string id) => _collection.DeleteOne(filter: b => b.Id == id);
	}
}