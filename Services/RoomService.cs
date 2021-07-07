using System;
using System.Collections.Generic;
using MongoDB.Driver;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Settings;
using Rumble.Platform.ChatService.Utilities;

namespace Rumble.Platform.ChatService.Services
{
	public class RoomService
	{
		private readonly IMongoCollection<Room> _collection;

		public RoomService(IChatDBSettings settings)
		{
			MongoClient client = new MongoClient(settings.ConnectionString);
			IMongoDatabase db = client.GetDatabase(settings.DatabaseName);
			_collection = db.GetCollection<Room>(settings.CollectionName);
		}

		// basic CRUD operations
		public List<Room> List()
		{
			var foo = _collection.Find(filter: r => true);
			return _collection.Find(filter: r => true).ToList();
		}

		public Room Get(string id)
		{
			Room output = _collection.Find(filter: r => r.Id == id).FirstOrDefault();
			if (output == null)
				throw new RoomNotFoundException();
			return output;
		}
		public List<Room> GetGlobals(string language) => _collection.Find(filter: r => r.Language == language).ToList();
		public void Create(Room room) => _collection.InsertOne(document: room);
		public void Update(Room room) => _collection.ReplaceOne(filter: r => r.Id == room.Id, replacement: room);
		public void Remove(Room room) => _collection.DeleteOne(filter: r => r.Id == room.Id);
	}
}