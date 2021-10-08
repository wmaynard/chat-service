using Newtonsoft.Json;
using Rumble.Platform.ChatService.Models;

namespace Rumble.Platform.ChatService.Exceptions
{
	public class AlreadyInRoomException : RoomException
	{
		[JsonProperty(NullValueHandling = NullValueHandling.Include)]
		public PlayerInfo Player { get; private set; }

		public AlreadyInRoomException(Room room, PlayerInfo player) : base (room, $"Player is already in requested room.")
		{
			Player = player;
		}
	}
}