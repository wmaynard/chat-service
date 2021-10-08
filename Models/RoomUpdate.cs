using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Rumble.Platform.ChatService.Models
{
	/// <summary>
	/// A model for information to be output to clients.  Contains unread messages for a room and member information.
	/// </summary>
	public class RoomUpdate // TODO: Inherit from PlatformDataModel?
	{
		private const string FRIENDLY_KEY_UNREAD_MESSAGES = "unreadMessages";
		
		#region CLIENT
		[JsonProperty(PropertyName = Room.FRIENDLY_KEY_HAS_STICKY)]
		private bool HasSticky { get; set; }
		
		[JsonProperty(PropertyName = Room.FRIENDLY_KEY_ID)]
		private string Id { get; set; }
		
		[JsonProperty(PropertyName = Room.FRIENDLY_KEY_MEMBERS)]
		private IEnumerable<PlayerInfo> Members { get; set; }
		
		[JsonProperty(PropertyName = Room.FRIENDLY_KEY_PREVIOUS_MEMBERS)]
		private IEnumerable<PlayerInfo> PreviousMembers { get; set; }
		
		[JsonProperty(PropertyName = FRIENDLY_KEY_UNREAD_MESSAGES, DefaultValueHandling = DefaultValueHandling.Ignore)]
		private Message[] UnreadMessages { get; set; }
		#endregion CLIENT

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
				HasSticky = room.HasSticky,
				Id = room.Id,
				UnreadMessages = room.MessagesSince(lastRead).ToArray(),
				Members = room.Members,
				PreviousMembers = room.PreviousMembers
			};
		}
		/// <summary>
		/// Standardizes the RoomUpdates for JSON output by returning a new Object with property RoomUpdates.
		/// TODO: This was before PlatformDataModel existed.
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