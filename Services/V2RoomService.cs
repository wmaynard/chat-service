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

public class V2RoomService : PlatformMongoService<V2Room>
{
	internal const string QUERY_ROOM_MEMBER = V2Room.DB_KEY_MEMBERS + "." + "aid";
	private SlackMessageClient SlackMonitorChannel { get; set; }

	private readonly Timer      _stickyTimer;
	private readonly ApiService _apiService;


	public V2Room StickyRoom
	{
		get
		{
			V2Room output;
			try
			{
				return _collection.Find(room => room.Type == V2Room.V2RoomType.Sticky).FirstOrDefault() 
					?? throw new V2RoomNotFoundException("sticky");
			}
			catch (V2RoomNotFoundException)
			{
				Log.Info(Owner.Will, "Sticky room not found; creating it now.");
				output = new V2Room()
				{
					Type = V2Room.V2RoomType.Sticky
				};
				Create(output);
				return StickyRoom;
			}
		}
	}

	public V2RoomService() : base("rooms")
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
			filter: Builders<V2Room>.Filter.ElemMatch(
			                                          field: room => room.Messages,
			                                          filter: message => message.Data != null && message.Data.Optional<string>("password") == password
			                                         ),
			update: Builders<V2Room>.Update.PullFilter(
			                                           field: room => room.Messages, 
			                                           filter: Builders<V2Message>.Filter.Where(message => message.Data != null 
			                                                                                               && message.Data.Optional<string>("password") == password 
			                                                                                               && message.Type == V2Message.V2MessageType.PvpChallenge
			                                                                                   )
			                                          )
		).ModifiedCount
		: _collection.UpdateMany(
			filter: Builders<V2Room>.Filter.ElemMatch(
			                                          field: room => room.Messages,
			                                          filter: message => message.AccountId == issuerAccountId
			                                         ),
			update: Builders<V2Room>.Update.PullFilter(
			                                           field: room => room.Messages, 
			                                           filter: Builders<V2Message>.Filter.Where(message => message.AccountId == issuerAccountId && message.Type == V2Message.V2MessageType.PvpChallenge)
			                                          )
		).ModifiedCount;

	public void DeleteStickies(bool expiredOnly = false)
	{
		List<V2Room> rooms = GetGlobals();
		rooms.Add(StickyRoom);

		int deleted = 0;
		foreach (V2Room room in rooms)
		{
			deleted += room.RemoveStickies(expiredOnly);
			Update(room);
		}
		if (deleted > 0)
			Log.Info(Owner.Will, $"Deleted {deleted} stickies from {rooms.Count} rooms.", data: new { Rooms = rooms.Select(r => r.Id) });

		V2Room sticky = StickyRoom;
		foreach (V2Message m in sticky.Messages.Where(m => m.Type == V2Message.V2MessageType.Sticky))
		{
			if (expiredOnly && !m.IsExpired)
				continue;
			m.Type = V2Message.V2MessageType.StickyArchived;
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

	public override V2Room Get(string id) => base.Get(id) ?? throw new V2RoomNotFoundException(id);

	public IEnumerable<V2Message> GetStickyMessages(bool all = false)
	{
		try
		{
			long timestamp = Timestamp.UnixTime;
			return all
				? StickyRoom.Messages.Where(m => m.Type == V2Message.V2MessageType.Sticky || m.Type == V2Message.V2MessageType.StickyArchived)
				: StickyRoom.Messages.Where(m => m.Type == V2Message.V2MessageType.Sticky && m.VisibleFrom < timestamp && m.Expiration > timestamp);
		}
		catch (RoomNotFoundException)
		{
			return Array.Empty<V2Message>();
		}
	}

	public List<V2Room> GetGlobals(string language = null)
	{
		List<V2Room> output = language == null
			                      ? _collection.Find(filter: room => room.Type == V2Room.V2RoomType.Global).ToList()
			                      : _collection.Find(filter: room => room.Type == V2Room.V2RoomType.Global && room.Language == language).ToList();

		OnEmptyRoomsFound?.Invoke(this, new EmptyRoomEventArgs(output)); 
		return output;
	}

	public List<V2Room> GetRoomsForUser(string aid)
	{
		// CLI equivalents:
		// db.rooms.find({members: {$elemMatch: {aid: "deadbeefdeadbeefdeadbeef"} } })
		// db.rooms.find({"members.aid": "deadbeefdeadbeefdeadbeef"})
		FilterDefinition<V2Room> filter = Builders<V2Room>.Filter.Eq(QUERY_ROOM_MEMBER, aid);
		List<V2Room> output = _collection.Find(filter).ToList();

		return output;
	}

	public V2Room[] GetSnapshotRooms(string aid) =>_collection
	                                               .Find(room => room.Members.Any(p => p == aid))
	                                               .ToList()
	                                               .ToArray();

	public void UpdateRoom(V2Room room)
	{
		FilterDefinition<V2Room> filter = Builders<V2Room>.Filter.Eq(rm => rm.Id, room.Id);
		UpdateDefinition<V2Room> update = Builders<V2Room>.Update.Set(rm => rm, room);

		_collection.FindOneAndUpdate<V2Room>(
		                                     filter: filter,
		                                     update: update
		                                    );
	}

	// TODO: If a user switches languages, they won't leave their old rooms.
	public V2Room JoinGlobal(string player, string language, string roomId = null)
	{
		IEnumerable<V2Room> globals = GetGlobals(language);
		V2Room joined = null;
		try
		{
			joined = roomId != null
				? globals.First(g => g.Id == roomId)
				: globals.First(g => !g.IsFull || g.HasMember(player));

			foreach (V2Room r in globals.Where(g => g.HasMember(player) && g.Id != roomId))
			{
				r.RemoveMember(player);
				Update(r);
			}

			joined.AddMember(player);
			Update(joined);
		}
		catch (InvalidOperationException) // No global rooms under capacity with user's language found.
		{
			if (roomId != null)
				throw new V2RoomNotFoundException(roomId, language);
			
			// Auto-scale global chat.  Create a new room and put the user in it.
			Log.Info(Owner.Will, $"Creating new global ({language}) room");
			joined = new V2Room()
			{
				MemberCapacity = V2Room.GlobalPlayerCapacity,
				Language = language,
				Type = V2Room.V2RoomType.Global
			};
			foreach (V2Message sticky in GetStickyMessages())
				joined.AddMessage(sticky);
			joined.AddMember(player);
			Create(joined);
		}
		catch (V2AlreadyInRoomException) { } // Do nothing.  The client didn't leave the room properly, but we don't want to send an error to it, either.

		if (joined == null) 
			return null;
		
		double percentChat = 100 * (float)joined.Messages.Count(m => m.Type == V2Message.V2MessageType.Chat) / joined.Messages.Count;
		Graphite.Track("room-members", joined?.Members.Count ?? 0, type: Graphite.Metrics.Type.AVERAGE);
		Graphite.Track("percent-chats", percentChat, type: Graphite.Metrics.Type.AVERAGE);

		return joined;
	}
	
	internal EventHandler<EmptyRoomEventArgs> OnEmptyRoomsFound;

	internal class EmptyRoomEventArgs : EventArgs
	{
		internal readonly string[] roomIds;
		public EmptyRoomEventArgs(IEnumerable<V2Room> rooms)
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

	public V2Room GetGuildChat(string id)
	{
		V2Room output = _collection
		                .Find(Builders<V2Room>.Filter.Eq(room => room.GuildId, id))
		                .Limit(1)
		                .FirstOrDefault();

		if (output != null)
			return output;
		
		output = new V2Room
		{
			GuildId = id,
			MemberCapacity = V2Room.GlobalPlayerCapacity,
			Type = V2Room.V2RoomType.Guild
		};
		
		_collection.InsertOne(output);
		return output;
	}

	public V2MonitorService.Data[] GetMonitorData(long timestamp)
	{
		return _collection
			.Find(filter: _ => true)
			.Project(Builders<V2Room>.Projection.Expression(room => new V2MonitorService.Data
			                                                        {
				                                                        Room = room.Id,
				                                                        // SlackColor = room.SlackColor,
				                                                        ChatMessages = room.Messages
				                                                                           .Where(message => message.Timestamp > timestamp && message.Type == V2Message.V2MessageType.Chat)
				                                                                           .ToArray()
			                                                        }))
			.ToList()
			.Where(data => data.ChatMessages.Any())
			.ToArray();
	}
}