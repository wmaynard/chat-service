using System;
using System.Runtime.Serialization;

namespace chat_service.Utilities
{
	public class RoomFullException : Exception
	{
		public RoomFullException() : this("Room is at capacity."){}
		public RoomFullException(SerializationInfo info, StreamingContext context) : base(info, context){}
		public RoomFullException(string message) : base(message){}
		public RoomFullException(string message, Exception inner) : base(message, inner) {}
	}
}