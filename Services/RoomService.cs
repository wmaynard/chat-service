using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MongoDB.Driver;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Settings;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;
using Rumble.Platform.CSharp.Common.Interop;
using Timer = System.Timers.Timer;

namespace Rumble.Platform.ChatService.Services
{
	public class RoomService : RumbleMongoService
	{
		internal const string QUERY_ROOM_MEMBER = Room.DB_KEY_MEMBERS + "." + PlayerInfo.DB_KEY_ACCOUNT_ID;
		internal const string QUERY_ROOM_PREVIOUS_MEMBER = Room.DB_KEY_PREVIOUS_MEMBERS + "." + PlayerInfo.DB_KEY_ACCOUNT_ID;
		private readonly RoomMonitor _monitor;
		private readonly SlackMessageClient SlackMonitorChannel = new SlackMessageClient(
			channel: RumbleEnvironment.Variable("SLACK_MONITOR_CHANNEL"), 
			token: RumbleEnvironment.Variable("SLACK_CHAT_TOKEN")
		);
		
		private new readonly IMongoCollection<Room> _collection;

		public RoomService(ChatDBSettings settings) : base(settings)
		{
			Log.Verbose(Owner.Will, "Creating RoomService");
			_collection = _database.GetCollection<Room>(settings.CollectionName);
			_monitor = new RoomMonitor(SendToSlack);
		}

		private void SendToSlack(object sender, RoomMonitor.MonitorEventArgs args)
		{
			List<Room> rooms = args.Restarted
				? _collection.Find(room => room.Messages.Any(m => m.Timestamp > args.LastRead)).ToList()
				: _collection.Find(room => args.RoomIds.Contains(room.Id)).ToList();
			if (!rooms.Any())
				return;

			List<SlackBlock> blocks = args.Restarted
				? new List<SlackBlock>()
				{
					new SlackBlock(SlackBlock.BlockType.HEADER, "Service Restarted"),
					new SlackBlock("Flushing all rooms in their entirety")
				}
				: null;

			SlackMessage m = new SlackMessage(
				blocks: blocks,
				attachments: rooms.Select(r => r.ToSlackAttachment(args.LastRead)).ToArray()
			);
			
			SlackMonitorChannel.Send(m);
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

		public List<Room> GetGlobals(string language = null)
		{
			return language == null
				? _collection.Find(filter: room => room.Type == Room.TYPE_GLOBAL).ToList()
				: _collection.Find(filter: room => room.Type == Room.TYPE_GLOBAL && room.Language == language).ToList();
		}

		public List<Room> GetRoomsForUser(string aid)
		{
			// CLI equivalents:
			// db.rooms.find({members: {$elemMatch: {aid: "deadbeefdeadbeefdeadbeef"} } })
			// db.rooms.find({"members.aid": "deadbeefdeadbeefdeadbeef"})
			FilterDefinition<Room> filter = Builders<Room>.Filter.Eq(QUERY_ROOM_MEMBER, aid);
			List<Room> output = _collection.Find(filter).ToList();

			return output;
		}

		public List<Room> GetPastAndPresentRoomsForUser(string aid)
		{
			FilterDefinitionBuilder<Room> builder = Builders<Room>.Filter;
			FilterDefinition<Room> filter = builder.Eq(QUERY_ROOM_MEMBER, aid) | builder.Eq(QUERY_ROOM_PREVIOUS_MEMBER, aid);
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
				Log.Info(Owner.Will, $"Creating new global ({language}) room");
				joined = new Room()
				{
					Capacity = Room.GLOBAL_PLAYER_CAPACITY,
					Language = language,
					Type = Room.TYPE_GLOBAL
				};
				foreach (Message sticky in GetStickyMessages(true))
					joined.AddMessage(sticky);
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