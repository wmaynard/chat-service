using Newtonsoft.Json;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Exceptions;

namespace Rumble.Platform.ChatService.Utilities
{
	public class InvalidPlayerInfoException : RumbleException
	{
		[JsonProperty(NullValueHandling = NullValueHandling.Include)]
		public PlayerInfo Player { get; private set; }
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string Reason { get; private set; }
		
		private InvalidPlayerInfoException(string message) : base(message) { }
		public InvalidPlayerInfoException(PlayerInfo info) : this("Invalid player information.")
		{
			Player = info;
		}

		public InvalidPlayerInfoException(PlayerInfo info, string badFieldName) : this($"Invalid player information. (Erroneous field name: {badFieldName}")
		{
			Player = info;
		}

		public InvalidPlayerInfoException(PlayerInfo info, string badFieldName, string reason) : this(info, badFieldName)
		{
			Reason = reason;
		}
	}
}