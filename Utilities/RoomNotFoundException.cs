using Newtonsoft.Json;
using Rumble.Platform.Common.Exceptions;

namespace Rumble.Platform.ChatService.Utilities
{
	public class RoomNotFoundException : RumbleException
	{
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string Language { get; private set; }
		[JsonProperty(NullValueHandling = NullValueHandling.Include)]
		public string RoomId { get; private set; }

		public RoomNotFoundException(string roomId, string language = null) : base($"Room {roomId} not found.")
		{
			Language = language;
			RoomId = roomId;
		}
	}
}