using System;
using System.Collections.Generic;
using MongoDB.Driver;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Settings;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Services
{
	public class SettingsService : RumbleMongoService
	{
		private new readonly IMongoCollection<ChatSettings> _collection;

		public SettingsService(SettingsDBSettings settings) : base(settings)
		{
			_collection = _database.GetCollection<ChatSettings>(settings.CollectionName);
		}

		public ChatSettings Get(string accountId)
		{
			try
			{
				return _collection.Find(filter: s => s.AccountId == accountId).First();
			}
			catch (InvalidOperationException) // The record does not yet exist.
			{
				ChatSettings output = new ChatSettings(accountId);
				Create(output);
				return output;
			}
		}
		public void Create(ChatSettings chatSettings) => _collection.InsertOne(document: chatSettings);
		public void Update(ChatSettings chatSettings) => _collection.ReplaceOne(filter: s => s.Id == chatSettings.Id, replacement: chatSettings);
		public void Remove(ChatSettings chatSettings) => _collection.DeleteOne(filter: s => s.Id == chatSettings.Id);
		public void Nuke() => _collection.DeleteMany(filter: s => true);	// TODO: Yikes!
	}
}