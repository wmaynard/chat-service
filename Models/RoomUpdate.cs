using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using System.Linq;

namespace Rumble.Platform.ChatService.Models
{
	/// <summary>
	/// A model for information to be output to clients.  Contains unread messages for a room and member information.
	/// </summary>
	public class RoomUpdate
	{
		public const string KEY_UNREAD_MESSAGES = "unreadMessages";
		
		[BsonElement(Room.KEY_ID)]
		public string Id { get; set; }
		[BsonElement(KEY_UNREAD_MESSAGES)]
		public IEnumerable<Message> UnreadMessages { get; set; }
		[BsonElement(Room.KEY_MEMBERS)]
		public IEnumerable<PlayerInfo> Members { get; set; }

		public static RoomUpdate FromRoom(Room room, long lastRead = 0)
		{
			return new RoomUpdate()
			{
				Id = room.Id,
				UnreadMessages = room.MessagesSince(lastRead),
				Members = room.Members
			};
		}

		public static object GenerateResponseFrom(Room room, long lastRead)
		{
			return new
			{
				RoomUpdates = new RoomUpdate[] {FromRoom(room, lastRead)}
			};
		}
		public static object GenerateResponseFrom(IEnumerable<Room> rooms, long lastRead)
		{
			return new
			{
				RoomUpdates = rooms.Select(r => FromRoom(r, lastRead))
			};
		}
	}
}