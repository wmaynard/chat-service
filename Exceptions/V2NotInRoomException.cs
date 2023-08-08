using System.Text.Json.Serialization;
using Rumble.Platform.ChatService.Models;

namespace Rumble.Platform.ChatService.Exceptions;

public class V2NotInRoomException : V2RoomException
{
	[JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string AccountId { get; private set; }

	public V2NotInRoomException(V2Room room, string accountId) : base(room, $"Player is not a member of the requested room.")
	{
		AccountId = accountId;
	}
}