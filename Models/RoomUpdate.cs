using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using System.Linq;
using Newtonsoft.Json;

namespace Rumble.Platform.ChatService.Models
{
	/// <summary>
	/// A model for information to be output to clients.  Contains unread messages for a room and member information.
	/// </summary>
	public class RoomUpdate
	{
		private const string KEY_UNREAD_MESSAGES = "unreadMessages";
		
		[JsonProperty, BsonElement(Room.KEY_ID)]
		private string Id { get; set; }
		[JsonProperty, BsonElement(KEY_UNREAD_MESSAGES)]
		private Message[] UnreadMessages { get; set; }
		[JsonProperty, BsonElement(Room.KEY_MEMBERS)]
		private IEnumerable<PlayerInfo> Members { get; set; }
		[JsonProperty, BsonElement(Room.KEY_PREVIOUS_MEMBERS)]
		private IEnumerable<PlayerInfo> PreviousMembers { get; set; }

		/// <summary>
		/// Creates a RoomUpdate from a given room and timestamp.
		/// </summary>
		/// <param name="room">The room to retrieve unread messages from.</param>
		/// <param name="lastRead">The timestamp from the last read message.</param>
		/// <returns>A new RoomUpdate object.</returns>
		private static RoomUpdate FromRoom(Room room, long lastRead = 0)
		{
			return new RoomUpdate()
			{
				Id = room.Id,
				UnreadMessages = room.MessagesSince(lastRead).ToArray(),
				Members = room.Members,
				PreviousMembers = room.PreviousMembers
			};
		}
		/// <summary>
		/// Standardizes the RoomUpdates for JSON output by returning a new Object with property RoomUpdates.
		/// </summary>
		/// <param name="room">The Room to retrieve updates from.</param>
		/// <param name="lastRead">The timestamp from the last read message.</param>
		/// <returns>A new Object with a RoomUpdates property.</returns>
		public static object GenerateResponseFrom(Room room, long lastRead)
		{
			return new
			{
				RoomUpdates = new RoomUpdate[] { FromRoom(room, lastRead) }
			};
		}
		/// <summary>
		/// Standardizes the RoomUpdates for JSON output by returning a new Object with property RoomUpdates.
		/// </summary>
		/// <param name="rooms">The Rooms to retrieve updates from.</param>
		/// <param name="lastRead">The timestamp from the last read message.</param>
		/// <returns>A new Object with a RoomUpdates property.</returns>
		public static object GenerateResponseFrom(IEnumerable<Room> rooms, long lastRead)
		{
			return new
			{
				RoomUpdates = rooms.Select(r => FromRoom(r, lastRead))
			};
		}
	}
}