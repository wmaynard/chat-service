using System.Text.Json.Serialization;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Exceptions;

namespace Rumble.Platform.ChatService.Exceptions
{
	public class InvalidPlayerInfoException : PlatformException
	{
		[JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string BadFieldName { get; private set; }
		[JsonInclude]
		public PlayerInfo Player { get; private set; }
		[JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string Reason { get; private set; }
		
		public InvalidPlayerInfoException(PlayerInfo info, string badFieldName = null, string reason = null) : base("Invalid player information.")
		{
			Player = info;
			BadFieldName = badFieldName;
			Reason = reason;
		}
	}
}