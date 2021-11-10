using System.Text.Json.Serialization;
using Rumble.Platform.ChatService.Models;

namespace Rumble.Platform.ChatService.Exceptions
{
	public class AlreadyInRoomException : RoomException
	{
		[JsonInclude]
		public PlayerInfo Player { get; private set; }

		public AlreadyInRoomException(Room room, PlayerInfo player) : base (room, $"Player is already in requested room.")
		{
			Player = player;
		}
	}
}