using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Rumble.Platform.ChatService.Models;

/// <summary>
/// A model for information to be output to clients.  Contains unread messages for a room and member information.
/// </summary>
public class RoomUpdate // TODO: Inherit from PlatformDataModel?
{
	private const string FRIENDLY_KEY_UNREAD_MESSAGES = "unreadMessages";
	
	#region CLIENT
	[JsonInclude, JsonPropertyName(Room.FRIENDLY_KEY_HAS_STICKY)]
	public bool HasSticky { get; private set; }
	
	[JsonInclude, JsonPropertyName(Room.FRIENDLY_KEY_ID)]
	public string Id { get; private set; }
	
	[JsonInclude, JsonPropertyName(Room.FRIENDLY_KEY_MEMBERS)]
	public IEnumerable<PlayerInfo> Members { get; private set; }
	
	[JsonInclude, JsonPropertyName(Room.FRIENDLY_KEY_PREVIOUS_MEMBERS)]
	public IEnumerable<PlayerInfo> PreviousMembers { get; private set; }
	
	[JsonInclude, JsonPropertyName(Room.FRIENDLY_KEY_ALL_MEMBERS)]
	public string[] Participants { get; private set; }
	
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_UNREAD_MESSAGES), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public Message[] UnreadMessages { get; private set; }
	#endregion CLIENT

	/// <summary>
	/// Creates a RoomUpdate from a given room and timestamp.
	/// </summary>
	/// <param name="room">The room to retrieve unread messages from.</param>
	/// <param name="lastRead">The timestamp from the last read message.</param>
	/// <returns>A new RoomUpdate object.</returns>
	private static RoomUpdate FromRoom(Room room, long lastRead = 0)
	{
		Message[] unread = room.MessagesSince(lastRead).ToArray();
		
		return new RoomUpdate
		{
			HasSticky = room.HasSticky,
			Id = room.Id,
			UnreadMessages = unread,
			Members = room.Members,
			PreviousMembers = room.PreviousMembers,
			Participants = unread.Select(message => message.AccountId).ToArray()
		};
	}
	/// <summary>
	/// Standardizes the RoomUpdates for JSON output by returning a new Object with property RoomUpdates.
	/// TODO: This was before PlatformDataModel existed.
	/// </summary>
	/// <param name="room">The Room to retrieve updates from.</param>
	/// <param name="lastRead">The timestamp from the last read message.</param>
	/// <returns>A new Object with a RoomUpdates property.</returns>
	public static object GenerateResponseFrom(Room room, long lastRead) => new
	{
		RoomUpdates = new RoomUpdate[] { FromRoom(room, lastRead) }
	};
	/// <summary>
	/// Standardizes the RoomUpdates for JSON output by returning a new Object with property RoomUpdates.
	/// </summary>
	/// <param name="rooms">The Rooms to retrieve updates from.</param>
	/// <param name="lastRead">The timestamp from the last read message.</param>
	/// <returns>A new Object with a RoomUpdates property.</returns>
	public static object GenerateResponseFrom(IEnumerable<Room> rooms, long lastRead) => new
	{
		RoomUpdates = rooms?.Where(room => room != null).Select(room => FromRoom(room, lastRead))
	};
}