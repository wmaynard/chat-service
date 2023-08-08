using System.Text.Json.Serialization;
using Rumble.Platform.ChatService.Models;

namespace Rumble.Platform.ChatService.Exceptions;

public class V2RoomFullException : V2RoomException
{
	[JsonInclude]
	public string AccountId { get; set; }
	public V2RoomFullException(V2Room room, string accountId) : base(room, "Room is at capacity.")
	{
		AccountId = accountId;
	}
}