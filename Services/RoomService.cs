using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Settings;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Services
{
	public class RoomService : RumbleMongoService
	{
		private new readonly IMongoCollection<Room> _collection;

		public RoomService(ChatDBSettings settings) : base(settings)
		{
			Log.Write("Creating RoomService");
			_collection = _database.GetCollection<Room>(settings.CollectionName);
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

		public Room GetStickyRoom(bool all = false)
		{
			Room output = _collection.Find(filter: r => r.Type == Room.TYPE_STICKY).FirstOrDefault();
			if (output == null)
				throw new RoomNotFoundException();
			return output;
		}

		public IEnumerable<Message> GetStickyMessages(bool all = false)
		{
			try
			{
				Room sticky = GetStickyRoom();
				long timestamp = Room.UnixTime;
				return all
					? sticky.Messages
					: sticky.Messages.Where(m => m.VisibleFrom < timestamp && m.Expiration > timestamp);
			}
			catch (RoomNotFoundException)
			{
				return Array.Empty<Message>();
			}
			throw new Exception("Couldn't retrieve sticky messages.");
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

		public List<Room> GetPastAndPresentRoomsForUser(string aid)
		{
			FilterDefinitionBuilder<Room> builder = Builders<Room>.Filter;
			FilterDefinition<Room> filter = builder.Eq("members.aid", aid) | builder.Eq("previousMembers.aid", aid);
			return _collection.Find(filter).ToList();
		}

		// TODO: If a user switches languages, they won't leave their old rooms.
		public Room JoinGlobal(PlayerInfo player, string language, string roomId = null)
		{
			IEnumerable<Room> globals = GetGlobals(language);
			Room joined = null;
			try
			{
				joined = roomId != null
					? globals.First(g => g.Id == roomId)
					: globals.First(g => !g.IsFull || g.HasMember(player.AccountId));

				foreach (Room r in globals.Where(g => g.HasMember(player.AccountId) && g.Id != roomId))
				{
					r.RemoveMember(player.AccountId);
					Update(r);
				}

				joined.AddMember(player);
				Update(joined);
			}
			catch (InvalidOperationException) // No global rooms under capacity with user's language found.
			{
				if (roomId != null)
					throw new RoomNotFoundException(roomId, language);
				
				// Auto-scale global chat.  Create a new room and put the user in it.
				Log.Write($"Creating new global ({language}) room");
				joined = new Room()
				{
					Capacity = Room.GLOBAL_PLAYER_CAPACITY,
					Language = language,
					Type = Room.TYPE_GLOBAL
				};
				joined.AddMember(player);
				Create(joined);
			}
			catch (AlreadyInRoomException)
			{
				// Do nothing.
				// The client didn't leave the room properly, but we don't want to send an error to it, either.
			}
			
			return joined;
		}
		public void Create(Room room) => _collection.InsertOne(document: room);
		public void Update(Room room) => _collection.ReplaceOne(filter: r => r.Id == room.Id, replacement: room);
		public void Remove(Room room) => _collection.DeleteOne(filter: r => r.Id == room.Id);
	}
}