using System.Text.Json.Serialization;
using Rumble.Platform.Common.Exceptions;

namespace Rumble.Platform.ChatService.Exceptions;

public class V2RoomNotFoundException : PlatformException
{
	[JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string Language { get; private set; }
	[JsonInclude]
	public string RoomId { get; private set; }

	public V2RoomNotFoundException(string roomId, string language = null) : base("Room not found")
	{
		Language = language;
		RoomId = roomId;
	}
}