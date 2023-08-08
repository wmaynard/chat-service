using System.Text.Json.Serialization;
using Rumble.Platform.Common.Exceptions;

namespace Rumble.Platform.ChatService.Exceptions;

public class V2ReportNotFoundException : PlatformException
{
	[JsonInclude]
	public string ReportId { get; private set; }
	public V2ReportNotFoundException(string id) : base("Chat report not found.")
	{
		ReportId = id;
	}
}