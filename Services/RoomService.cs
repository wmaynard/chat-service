using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using MongoDB.Driver;
using RCL.Logging;
using Rumble.Platform.ChatService.Exceptions;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;
using Rumble.Platform.Common.Interop;
using Rumble.Platform.Common.Services;
using Rumble.Platform.Data;
using Timer = System.Timers.Timer;

namespace Rumble.Platform.ChatService.Services;

public class RoomService : PlatformMongoService<Room>
{
	internal const string QUERY_ROOM_MEMBER = Room.DB_KEY_MEMBERS + "." + PlayerInfo.DB_KEY_ACCOUNT_ID;
	internal const string QUERY_ROOM_PREVIOUS_MEMBER = Room.DB_KEY_PREVIOUS_MEMBERS + "." + PlayerInfo.DB_KEY_ACCOUNT_ID;
	private SlackMessageClient SlackMonitorChannel { get; set; }

	private readonly Timer      _stickyTimer;
	private readonly ApiService _apiService;


	public Room StickyRoom
	{
		get
		{
			Room output;
			try
			{
				return _collection.Find(room => room.Type == Room.TYPE_STICKY).FirstOrDefault() 
					?? throw new RoomNotFoundException("sticky");
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

	public RoomService() : base("rooms")
	{
		_stickyTimer = new Timer((PlatformEnvironment.Optional<int?>("stickyCheck") ?? 3_000) * 1_000)
		{
			AutoReset = true
		};

		_stickyTimer.Elapsed += CheckExpiredStickies;
		_stickyTimer.Start();
	}

	public long DeletePvpChallenge(string password, string issuerAccountId) => password != null
		? _collection.UpdateMany(
			filter: Builders<Room>.Filter.ElemMatch(
				field: room => room.Messages,
				filter: message => message.Data != null && message.Data.Optional<string>("password") == password
			),
			update: Builders<Room>.Update.PullFilter(
				field: room => room.Messages, 
				filter: Builders<Message>.Filter.Where(message => message.Data != null 
					&& message.Data.Optional<string>("password") == password 
					&& message.Type == Message.TYPE_CHALLENGE
				)
			)
		).ModifiedCount
		: _collection.UpdateMany(
			filter: Builders<Room>.Filter.ElemMatch(
				field: room => room.Messages,
				filter: message => message.AccountId == issuerAccountId
			),
			update: Builders<Room>.Update.PullFilter(
				field: room => room.Messages, 
				filter: Builders<Message>.Filter.Where(message => message.AccountId == issuerAccountId && message.Type == Message.TYPE_CHALLENGE)
			)
		).ModifiedCount;

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
		{
			if (expiredOnly && !m.IsExpired)
				continue;
			m.Type = Message.TYPE_STICKY_ARCHIVED;
		}

		Update(sticky);
	}

	private void CheckExpiredStickies(object sender, ElapsedEventArgs args)
	{
		_stickyTimer.Start();
		try
		{
			DeleteStickies(true);
		}
		catch (Exception e)
		{
			Log.Error(Owner.Will, "Error encountered when checking for expired stickies.", exception: e);
			
			_apiService.Alert(
				title: "Error encountered when checking for expired stickies.",
				message: "Error encountered when checking for expired stickies.",
				countRequired: 1,
				timeframe: 300,
				data: new RumbleJson
				    {
				        { "Exception", e }
				    } 
			);
		}
		_stickyTimer.Start();
	}

	public override Room Get(string id) => base.Get(id) ?? throw new RoomNotFoundException(id);

	public IEnumerable<Message> GetStickyMessages(bool all = false)
	{
		try
		{
			long timestamp = Timestamp.UnixTime;
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
		List<Room> output = language == null
			? _collection.Find(filter: room => room.Type == Room.TYPE_GLOBAL).ToList()
			: _collection.Find(filter: room => room.Type == Room.TYPE_GLOBAL && room.Language == language).ToList();

		OnEmptyRoomsFound?.Invoke(this, new EmptyRoomEventArgs(output)); 
		return output;
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

	public Room[] GetSnapshotRooms(string aid) =>_collection
		.Find(room => room.Members.Any(p => p.AccountId == aid) || room.PreviousMembers.Any(p => p.AccountId == aid))
		.ToList()
		.ToArray();

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
				MemberCapacity = Room.GlobalPlayerCapacity,
				Language = language,
				Type = Room.TYPE_GLOBAL
			};
			foreach (Message sticky in GetStickyMessages())
				joined.AddMessage(sticky);
			joined.AddMember(player);
			Create(joined);
		}
		catch (AlreadyInRoomException) { } // Do nothing.  The client didn't leave the room properly, but we don't want to send an error to it, either.

		if (joined == null) 
			return null;
		
		double percentChat = 100 * (float)joined.Messages.Count(m => m.Type == Message.TYPE_CHAT) / joined.Messages.Count;
		Graphite.Track("room-members", joined?.Members.Count ?? 0, type: Graphite.Metrics.Type.AVERAGE);
		Graphite.Track("room-offline-members", joined?.PreviousMembers.Count ?? 0, type: Graphite.Metrics.Type.AVERAGE);
		Graphite.Track("percent-chats", percentChat, type: Graphite.Metrics.Type.AVERAGE);

		return joined;
	}
	
	internal EventHandler<EmptyRoomEventArgs> OnEmptyRoomsFound;

	internal class EmptyRoomEventArgs : EventArgs
	{
		internal readonly string[] roomIds;
		public EmptyRoomEventArgs(IEnumerable<Room> rooms)
		{
			// Skip 1 because we don't want to delete the last room.
			// This generally shouldn't be an issue, though, because this means that no players are logging into the game.
			// Still, it could happen if for any reason the game couldn't communicate with Chat but the service was still running,
			// e.g. a change in the routing.
			
			roomIds = rooms
				.Skip(1)
				.Where(room => !room.Members.Any())
				.Select(room => room.Id).ToArray();
		}
	}

	public Room GetGuildChat(string id)
	{
		Room output = _collection
			.Find(Builders<Room>.Filter.Eq(room => room.GuildId, id))
			.Limit(1)
			.FirstOrDefault();

		if (output != null)
			return output;
		
		output = new Room
		{
			GuildId = id,
			MemberCapacity = Room.GlobalPlayerCapacity,
			Type = Room.TYPE_GUILD
		};
		
		_collection.InsertOne(output);
		return output;
	}

	public MonitorService.Data[] GetMonitorData(long timestamp)
	{
		return _collection
			.Find(filter: _ => true)
			.Project(Builders<Room>.Projection.Expression(room => new MonitorService.Data
			{
				Room = room.Id,
				// SlackColor = room.SlackColor,
				ChatMessages = room.Messages
					.Where(message => message.Timestamp > timestamp && message.Type == Message.TYPE_CHAT)
					.ToArray()
			}))
			.ToList()
			.Where(data => data.ChatMessages.Any())
			.ToArray();
	}
}