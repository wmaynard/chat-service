using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Models;

/// <summary>
/// A model for information to be output to clients.  Contains unread messages for a room and member information.
/// </summary>
public class V2RoomUpdate : PlatformDataModel
{
	private const string FRIENDLY_KEY_UNREAD_MESSAGES = "unreadMessages";
	private const string FRIENDLY_KEY_ID              = "id";
	
	#region CLIENT
	[JsonInclude, JsonPropertyName(V2Room.FRIENDLY_KEY_HAS_STICKY)]
	public bool HasSticky { get; private set; }
	
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_ID)]
	public string Id { get; private set; }
	
	[JsonInclude, JsonPropertyName(V2Room.FRIENDLY_KEY_MEMBERS)]
	public IEnumerable<string> Members { get; private set; }

	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_UNREAD_MESSAGES), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public V2Message[] UnreadMessages { get; private set; }
	#endregion CLIENT

	/// <summary>
	/// Creates a RoomUpdate from a given room and timestamp.
	/// </summary>
	/// <param name="room">The room to retrieve unread messages from.</param>
	/// <param name="lastRead">The timestamp from the last read message.</param>
	/// <returns>A new RoomUpdate object.</returns>
	private static V2RoomUpdate FromRoom(V2Room room, long lastRead = 0)
	{
		V2Message[] unread = room.MessagesSince(lastRead).ToArray();
		
		return new V2RoomUpdate
		{
			HasSticky = room.HasSticky,
			Id = room.Id,
			UnreadMessages = unread,
			Members = room.Members
		};
	}
	
	/// <summary>
	/// Standardizes the RoomUpdates for JSON output by returning a new Object with property RoomUpdates.
	/// </summary>
	/// <param name="rooms">The Rooms to retrieve updates from.</param>
	/// <param name="lastRead">The timestamp from the last read message.</param>
	/// <returns>A new Object with a RoomUpdates property.</returns>
	public static object GenerateResponseFrom(IEnumerable<V2Room> rooms, long lastRead) => new
	                                                                                       {
		                                                                                       RoomUpdates = rooms?.Where(room => room != null).Select(room => FromRoom(room, lastRead))
	                                                                                       };
}