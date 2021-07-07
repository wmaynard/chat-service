using System;
using System.Runtime.Serialization;

namespace chat_service.Utilities
{
	public class AlreadyInRoomException : Exception
	{
		public AlreadyInRoomException() : this("Already in room."){}
		public AlreadyInRoomException(SerializationInfo info, StreamingContext context) : base(info, context){}
		public AlreadyInRoomException(string message) : base(message){}
		public AlreadyInRoomException(string message, Exception inner) : base(message, inner) {}
	}
}