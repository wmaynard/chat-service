using System;
using System.Runtime.Serialization;
using platform_CSharp_library.Web;

namespace Rumble.Platform.ChatService.Utilities
{
	public class NotInRoomException : RumbleException
	{
		public NotInRoomException() : base("AccountID is not a member of the room."){}

		public NotInRoomException(string userId, string roomId)
			: base($"User {userId} is not a member of room {roomId}"){}
	}
}