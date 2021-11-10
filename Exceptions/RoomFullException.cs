using System.Text.Json.Serialization;
using Rumble.Platform.ChatService.Models;

namespace Rumble.Platform.ChatService.Exceptions
{
	public class RoomFullException : RoomException
	{
		[JsonInclude]
		public PlayerInfo Player { get; set; }
		public RoomFullException(Room room, PlayerInfo player) : base(room, "Room is at capacity.")
		{
			Player = player;
		}
	}
}