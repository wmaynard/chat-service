using Newtonsoft.Json;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Exceptions;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Utilities
{
	public class UserBannedException : RumbleException
	{
		[JsonProperty(NullValueHandling = NullValueHandling.Include)]
		public TokenInfo Token { get; set; }
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public Message AttemptedMessage { get; set; }
		[JsonProperty(NullValueHandling = NullValueHandling.Include)]
		public Ban Ban { get; set; }

		public UserBannedException(TokenInfo tokenInfo, Message message, Ban ban) : base($"You are banned. (Time Remaining: {ban?.TimeRemaining ?? "permanent"})")
		{
			Token = tokenInfo;
			AttemptedMessage = message;
			ban?.PurgeSnapshot();
			Ban = ban;
		}
	}
}