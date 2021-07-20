using System;
using System.Collections.Generic;
using MongoDB.Driver;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Settings;

namespace Rumble.Platform.ChatService.Services
{
	public class SettingsService
	{
		private readonly IMongoCollection<Models.ChatSettings> _collection;

		public SettingsService(SettingsDBSettings settingsDbSettings)
		{
			MongoClient client = new MongoClient(settingsDbSettings.ConnectionString);
			IMongoDatabase db = client.GetDatabase(settingsDbSettings.DatabaseName);
			_collection = db.GetCollection<Models.ChatSettings>(settingsDbSettings.CollectionName);
		}

		public Models.ChatSettings Get(string accountId)
		{
			try
			{
				return _collection.Find(filter: s => s.AccountId == accountId).First();
			}
			catch (InvalidOperationException) // The record does not yet exist.
			{
				Models.ChatSettings output = new ChatSettings(accountId);
				Create(output);
				return output;
			}
		}
		public void Create(ChatSettings chatSettings) => _collection.InsertOne(document: chatSettings);
		public void Update(ChatSettings chatSettings) => _collection.ReplaceOne(filter: s => s.Id == chatSettings.Id, replacement: chatSettings);
		public void Remove(ChatSettings chatSettings) => _collection.DeleteOne(filter: s => s.Id == chatSettings.Id);
		public void Nuke() => _collection.DeleteMany(filter: s => true);
	}
}