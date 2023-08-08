using System.Collections.Generic;
using System.Text.Json.Serialization;
using Rumble.Platform.Common.Interop;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Models;

public class V2SlackReport : PlatformDataModel
{
	public const string FRIENDLY_KEY_ATTACHMENTS = "attachments";
	public const string FRIENDLY_KEY_BLOCKS = "blocks";
	public const string FRIENDLY_KEY_CHANNEL = "channel";
	
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_ATTACHMENTS), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public object[] Attachments { get; set; }
	
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_BLOCKS)]
	public List<SlackBlock> Blocks { get; set; }

	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_CHANNEL)]
	public string DestinationChannel => PlatformEnvironment.Require<string>("reportsChannel");

	public V2SlackReport(List<SlackBlock> blocks, List<SlackBlock> attachments)
	{
		// DestinationChannel = PlatformEnvironment.Require<string>("reportsChannel");
		Blocks = blocks;
		Attachments = new object[]{ new
		{
			Color = "#2eb886",
			Blocks = attachments
		}};
	}
}