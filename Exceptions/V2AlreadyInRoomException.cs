using System.Text.Json.Serialization;
using Rumble.Platform.ChatService.Models;

namespace Rumble.Platform.ChatService.Exceptions;

public class V2AlreadyInRoomException : V2RoomException
{
	[JsonInclude]
	public string AccountId { get; private set; }

	public V2AlreadyInRoomException(V2Room room, string accountId) : base (room, $"Player is already in requested room.")
	{
		AccountId = accountId;
	}
}