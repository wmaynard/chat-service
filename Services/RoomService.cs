using System.Collections.Generic;
using chat_service.Models;
using chat_service.Settings;
using MongoDB.Driver;

namespace chat_service.Services
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
		public Room Get(string id) => _collection.Find(filter: r => r.Id == id).FirstOrDefault();
		public List<Room> GetGlobals(string language) => _collection.Find(filter: r => r.Language == language).ToList();
		public void Create(Room room) => _collection.InsertOne(document: room);
		public void Update(Room room) => _collection.ReplaceOne(filter: r => r.Id == room.Id, replacement: room);
		public void Remove(Room room) => _collection.DeleteOne(filter: r => r.Id == room.Id);
	}
}