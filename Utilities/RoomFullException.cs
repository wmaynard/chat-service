using Rumble.Platform.Common.Exceptions;

namespace Rumble.Platform.ChatService.Utilities
{
	public class RoomFullException : RumbleException
	{
		public RoomFullException() : base("Room is at capacity."){}
		public RoomFullException(string roomId) : base($"Room {roomId} is at capacity."){}
	}
}