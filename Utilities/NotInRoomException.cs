using System;
using System.Runtime.Serialization;

namespace Rumble.Platform.ChatService.Utilities
{
	public class NotInRoomException : Exception
	{
		public NotInRoomException() : this("AccountID is not a member of the room."){}
		public NotInRoomException(SerializationInfo info, StreamingContext context) : base(info, context){}
		public NotInRoomException(string message) : base(message){}
		public NotInRoomException(string message, Exception inner) : base(message, inner) {}
	}
}