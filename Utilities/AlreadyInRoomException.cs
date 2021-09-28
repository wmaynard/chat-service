using Newtonsoft.Json;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Exceptions;

namespace Rumble.Platform.ChatService.Utilities
{
	public class AlreadyInRoomException : RoomException
	{
		[JsonProperty(NullValueHandling = NullValueHandling.Include)]
		public PlayerInfo Player { get; private set; }
		
		public AlreadyInRoomException(Room room, PlayerInfo player) : base (room, $"Player {player.UniqueScreenname} is already in room {room.Id}")
		{
			Player = player;
		}
	}
}