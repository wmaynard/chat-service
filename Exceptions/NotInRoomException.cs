using System.Text.Json;
using System.Text.Json.Serialization;
using Rumble.Platform.ChatService.Models;

namespace Rumble.Platform.ChatService.Exceptions
{
	public class NotInRoomException : RoomException
	{
		[JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string AccountId { get; private set; }

		public NotInRoomException(Room room, string accountId) : base(room, $"Player is not a member of the requested room.")
		{
			AccountId = accountId;
		}
	}
}