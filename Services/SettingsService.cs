using System;
using System.Collections.Generic;
using MongoDB.Driver;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;
using Rumble.Platform.CSharp.Common.Interop;

namespace Rumble.Platform.ChatService.Services
{
	public class SettingsService : PlatformMongoService<ChatSettings>
	{
		// private new readonly IMongoCollection<ChatSettings> _collection;
		// protected sealed override string CollectionName => "settings";

		public SettingsService() : base("settings")
		{
			// Log.Verbose(Owner.Will, "Creating SettingsService");
			// _collection = _database.GetCollection<ChatSettings>(CollectionName);
		}

		public override ChatSettings Get(string accountId)
		{
			ChatSettings output = _collection.Find(s => s.AccountId == accountId).FirstOrDefault();
			if (output != null)
				return output;
			output = new ChatSettings(accountId);
			Create(output);
			return output;
			// try
			// {
			// 	return _collection.Find(filter: s => s.AccountId == accountId).First();
			// }
			// catch (InvalidOperationException) // The record does not yet exist.
			// {
			// 	ChatSettings output = new ChatSettings(accountId);
			// 	Create(output);
			// 	return output;
			// }
		}

		// public IEnumerable<ChatSettings> List()
		// {
		// 	return _collection.Find(filter: s => true).ToList();
		// }
		// public void Create(ChatSettings chatSettings) => _collection.InsertOne(document: chatSettings);
		// public void Update(ChatSettings chatSettings) => _collection.ReplaceOne(filter: s => s.Id == chatSettings.Id, replacement: chatSettings);
		// public void Remove(ChatSettings chatSettings) => _collection.DeleteOne(filter: s => s.Id == chatSettings.Id);
		// public void Nuke() => _collection.DeleteMany(filter: s => true);
	}
}