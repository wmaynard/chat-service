using Rumble.Platform.Common.Exceptions;

namespace Rumble.Platform.ChatService.Utilities
{
	public class NotInRoomException : RumbleException
	{
		public NotInRoomException() : base("AccountID is not a member of the room."){}

		public NotInRoomException(string userId, string roomId)
			: base($"User {userId} is not a member of room {roomId}"){}
	}
}