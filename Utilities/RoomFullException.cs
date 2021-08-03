using System;
using System.Runtime.Serialization;
using Rumble.Platform.Common.Exceptions;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Utilities
{
	public class RoomFullException : RumbleException
	{
		public RoomFullException() : base("Room is at capacity."){}
		public RoomFullException(string roomId) : base($"Room {roomId} is at capacity."){}
	}
}