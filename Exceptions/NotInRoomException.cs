using Newtonsoft.Json;
using Rumble.Platform.ChatService.Models;

namespace Rumble.Platform.ChatService.Exceptions
{
	public class NotInRoomException : RoomException
	{
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string AccountId { get; private set; }

		public NotInRoomException(Room room, string accountId) : base(room, $"Player is not a member of the requested room.")
		{
			AccountId = accountId;
		}
	}
}