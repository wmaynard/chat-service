using Newtonsoft.Json;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Exceptions;

namespace Rumble.Platform.ChatService.Exceptions
{
	public class InvalidPlayerInfoException : PlatformException
	{
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string BadFieldName { get; private set; }
		[JsonProperty(NullValueHandling = NullValueHandling.Include)]
		public PlayerInfo Player { get; private set; }
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string Reason { get; private set; }
		
		public InvalidPlayerInfoException(PlayerInfo info, string badFieldName = null, string reason = null) : base("Invalid player information.")
		{
			Player = info;
			BadFieldName = badFieldName;
			Reason = reason;
		}
	}
}