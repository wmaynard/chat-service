using System.Text.Json.Serialization;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Exceptions;
using Rumble.Platform.Common.Models;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Exceptions;

public class V2UserBannedException : PlatformException
{
	[JsonInclude]
	public TokenInfo Token { get; set; }
	[JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public V2Message AttemptedMessage { get; set; }
	[JsonInclude]
	public Ban Ban { get; set; }

	public V2UserBannedException(TokenInfo tokenInfo, V2Message message, Ban ban) : base("You are banned.")
	{
		Token = tokenInfo;
		AttemptedMessage = message;
		ban?.PurgeSnapshot();
		Ban = ban;
	}
}