using Rumble.Platform.Common.Exceptions;

namespace Rumble.Platform.ChatService.Utilities
{
	public class RoomNotFoundException : RumbleException
	{
		public RoomNotFoundException() : base("Room not found."){}
		public RoomNotFoundException(string roomId) : base($"Room {roomId} not found."){}
		public RoomNotFoundException(string roomId, string language) : base($"Room {roomId} ({language}) not found."){}
	}
}