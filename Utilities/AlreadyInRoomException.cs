using Rumble.Platform.Common.Exceptions;

namespace Rumble.Platform.ChatService.Utilities
{
	public class AlreadyInRoomException : RumbleException
	{
		public AlreadyInRoomException() : base("Already in room."){}
		public AlreadyInRoomException(string userId, string roomId) : base($"User {userId} is already in room {roomId}."){}
	}
}