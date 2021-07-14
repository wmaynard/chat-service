using System;
using System.Runtime.Serialization;
using platform_CSharp_library.Web;

namespace Rumble.Platform.ChatService.Utilities
{
	public class RoomNotFoundException : RumbleException
	{
		public RoomNotFoundException() : base("Room not found."){}
		public RoomNotFoundException(string roomId) : base($"Room {roomId} not found."){}
	}
}