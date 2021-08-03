using System;
using System.Runtime.Serialization;
using Rumble.Platform.Common.Exceptions;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Utilities
{
	public class AlreadyInRoomException : RumbleException
	{
		public AlreadyInRoomException() : base("Already in room."){}
		public AlreadyInRoomException(string userId, string roomId) : base($"User {userId} is already in room {roomId}."){}
	}
}