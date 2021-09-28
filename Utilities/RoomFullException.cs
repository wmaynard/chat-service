using Newtonsoft.Json;
using Rumble.Platform.Common.Exceptions;
using Rumble.Platform.ChatService.Models;

namespace Rumble.Platform.ChatService.Utilities
{
	public class RoomFullException : RoomException
	{
		[JsonProperty(NullValueHandling = NullValueHandling.Include)]
		public PlayerInfo Player { get; set; }
		public RoomFullException(Room room, PlayerInfo player) : base(room, $"Room is at capacity.")
		{
			Player = player;
		}
	}
}