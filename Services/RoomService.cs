using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Timers;
using MongoDB.Driver;
using Rumble.Platform.ChatService.Exceptions;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;
using Rumble.Platform.CSharp.Common.Interop;
using Timer = System.Timers.Timer;

namespace Rumble.Platform.ChatService.Services
{
	public class RoomService : PlatformMongoService<Room>
	{
		// protected sealed override string CollectionName => "rooms";
		internal const string QUERY_ROOM_MEMBER = Room.DB_KEY_MEMBERS + "." + PlayerInfo.DB_KEY_ACCOUNT_ID;
		internal const string QUERY_ROOM_PREVIOUS_MEMBER = Room.DB_KEY_PREVIOUS_MEMBERS + "." + PlayerInfo.DB_KEY_ACCOUNT_ID;
		private readonly RoomMonitor _monitor;
		private readonly SlackMessageClient SlackMonitorChannel = new SlackMessageClient(
			channel: RumbleEnvironment.Variable("SLACK_MONITOR_CHANNEL"), 
			token: RumbleEnvironment.Variable("SLACK_CHAT_TOKEN")
		);

		private Timer _stickyTimer;

		public Room StickyRoom
		{
			get
			{
				Room output;
				try
				{
					return _collection.Find(room => room.Type == Room.TYPE_STICKY).FirstOrDefault() 
						?? throw new RoomNotFoundException("sticky");
					// output = _collection.Find(filter: r => r.Type == Room.TYPE_STICKY).FirstOrDefault();
					// if (output == null)
					// 	throw new RoomNotFoundException("sticky");
					// return output;
				}
				catch (RoomNotFoundException)
				{
					Log.Info(Owner.Will, "Sticky room not found; creating it now.");
					output = new Room()
					{
						Type = Room.TYPE_STICKY
					};
					Create(output);
					return StickyRoom;
				}
			}
		}
		
		// private new readonly IMongoCollection<Room> _collection;

		public RoomService() : base("rooms")
		{
			// Log.Verbose(Owner.Will, "Creating RoomService");
			// _collection = _database.GetCollection<Room>(CollectionName);
			_monitor = new RoomMonitor(SendToSlack);
			_stickyTimer = new Timer(int.Parse(RumbleEnvironment.Variable("STICKY_CHECK_FREQUENCY_SECONDS") ?? "3000") * 1_000)
			{
				AutoReset = true
			};
			_stickyTimer.Elapsed += CheckExpiredStickies;
			_stickyTimer.Start();
		}

		public void DeleteStickies(bool expiredOnly = false)
		{
			List<Room> rooms = GetGlobals();
			rooms.Add(StickyRoom);

			int deleted = 0;
			foreach (Room room in rooms)
			{
				deleted += room.RemoveStickies(expiredOnly);
				Update(room);
			}
			if (deleted > 0)
				Log.Info(Owner.Will, $"Deleted {deleted} stickies from {rooms.Count} rooms.", data: new { Rooms = rooms.Select(r => r.Id) });

			Room sticky = StickyRoom;
			foreach (Message m in sticky.Messages.Where(m => m.Type == Message.TYPE_STICKY))
				m.Type = Message.TYPE_STICKY_ARCHIVED;
			Update(sticky);
		}

		private void CheckExpiredStickies(object sender, ElapsedEventArgs args)
		{
			_stickyTimer.Start();
			try
			{
				Log.Local(Owner.Will, "Attempting to delete expired sticky messages");
				DeleteStickies(true);
			}
			catch (Exception e)
			{
				Log.Error(Owner.Will, "Error encountered when checking for expired stickies.", exception: e);
			}
			_stickyTimer.Start();
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
		// public List<Room> List()
		// {
		// 	return _collection.Find(filter: r => true).ToList();
		// }

		public override Room Get(string id)
		{
			Room output = base.Get(id);
			// Room output = _collection.Find(filter: r => r.Id == id).FirstOrDefault();
			if (output == null)
				throw new RoomNotFoundException(id);
			return output;
		}

		public IEnumerable<Message> GetStickyMessages(bool all = false)
		{
			try
			{
				long timestamp = Room.UnixTime;
				return all
					? StickyRoom.Messages.Where(m => m.Type is Message.TYPE_STICKY or Message.TYPE_STICKY_ARCHIVED)
					: StickyRoom.Messages.Where(m => m.Type == Message.TYPE_STICKY && m.VisibleFrom < timestamp && m.Expiration > timestamp);
			}
			catch (RoomNotFoundException)
			{
				return Array.Empty<Message>();
			}
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

		public Room[] GetSnapshotRooms(string aid)
		{
			return _collection.Find(room => room.Members.Any(p => p.AccountId == aid) || room.PreviousMembers.Any(p => p.AccountId == aid)).ToList().ToArray();
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
					MemberCapacity = Room.GLOBAL_PLAYER_CAPACITY,
					Language = language,
					Type = Room.TYPE_GLOBAL
				};
				foreach (Message sticky in GetStickyMessages())
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
		// public void Create(Room room) => _collection.InsertOne(document: room);
		// public void Update(Room room) => _collection.ReplaceOne(filter: r => r.Id == room.Id, replacement: room);
		// public void Remove(Room room) => _collection.DeleteOne(filter: r => r.Id == room.Id);
	}
}