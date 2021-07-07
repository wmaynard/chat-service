using System;
using System.Runtime.Serialization;

namespace Rumble.Platform.ChatService.Utilities
{
	public class RoomNotFoundException : Exception
	{
		public RoomNotFoundException() : this("Room not found."){}
		public RoomNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context){}
		public RoomNotFoundException(string message) : base(message){}
		public RoomNotFoundException(string message, Exception inner) : base(message, inner) {}
	}
}