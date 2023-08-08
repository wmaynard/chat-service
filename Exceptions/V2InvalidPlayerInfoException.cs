using System.Text.Json.Serialization;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Exceptions;

namespace Rumble.Platform.ChatService.Exceptions;

public class V2InvalidPlayerInfoException : PlatformException
{
	[JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string BadFieldName { get; private set; }
	[JsonInclude]
	public string AccountId { get; private set; }
	[JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string Reason { get; private set; }
	
	public V2InvalidPlayerInfoException(string info, string badFieldName = null, string reason = null) : base("Invalid player information.")
	{
		AccountId = info;
		BadFieldName = badFieldName;
		Reason = reason;
	}
}