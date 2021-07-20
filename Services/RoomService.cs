using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MongoDB.Bson;
using MongoDB.Driver;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Settings;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Services
{
	public class RoomService
	{
		private readonly IMongoCollection<Room> _collection;

		public RoomService(ChatDBSettings settings)
		{
			Log.Write("Creating RoomService...");
			MongoClient client = new MongoClient(settings.ConnectionString);
			IMongoDatabase db = client.GetDatabase(settings.DatabaseName);
			_collection = db.GetCollection<Room>(settings.CollectionName);
			Log.Write("Done.");
		}

		// basic CRUD operations
		public List<Room> List()
		{
			return _collection.Find(filter: r => true).ToList();
		}

		public Room Get(string id)
		{
			Room output = _collection.Find(filter: r => r.Id == id).FirstOrDefault();
			if (output == null)
				throw new RoomNotFoundException();
			return output;
		}

		public Room GetSticky()
		{
			Room output = _collection.Find(filter: r => r.Type == Room.TYPE_STICKY).FirstOrDefault();
			if (output == null)
				throw new RoomNotFoundException();
			return output;
		}
		public List<Room> GetGlobals(string language) => _collection.Find(filter: r => r.Language == language).ToList();

		public List<Room> GetRoomsForUser(string aid)
		{
			// CLI equivalents:
			// db.rooms.find({members: {$elemMatch: {aid: "deadbeefdeadbeefdeadbeef"} } })
			// db.rooms.find({"members.aid": "deadbeefdeadbeefdeadbeef"})
			FilterDefinition<Room> filter = Builders<Room>.Filter.Eq("members.aid", aid);
			List<Room> output = _collection.Find(filter).ToList();

			return output;
		}
		public void Create(Room room) => _collection.InsertOne(document: room);
		public void Update(Room room) => _collection.ReplaceOne(filter: r => r.Id == room.Id, replacement: room);
		public void Remove(Room room) => _collection.DeleteOne(filter: r => r.Id == room.Id);
	}
}