using System.Collections.Generic;
using System.Text.Json.Serialization;
using Rumble.Platform.Common.Interop;
using Rumble.Platform.Common.Utilities;

namespace Rumble.Platform.ChatService.Models
{
	public struct SlackReport // TODO: PlatformDataModel?
	{
		public const string FRIENDLY_KEY_ATTACHMENTS = "Attachments";
		public const string FRIENDLY_KEY_BLOCKS = "Blocks";
		public const string FRIENDLY_KEY_CHANNEL = "Channel";
		
		[JsonInclude, JsonPropertyName(FRIENDLY_KEY_ATTACHMENTS), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public object[] Attachments { get; set; }
		
		[JsonInclude, JsonPropertyName(FRIENDLY_KEY_BLOCKS)]
		public List<SlackBlock> Blocks { get; set; }
		
		[JsonInclude, JsonPropertyName(FRIENDLY_KEY_CHANNEL)]
		public string DestinationChannel { get; set; }

		public SlackReport(List<SlackBlock> blocks, List<SlackBlock> attachments)
		{
			DestinationChannel = PlatformEnvironment.Variable("SLACK_REPORTS_CHANNEL");
			Blocks = blocks;
			Attachments = new object[]{ new
			{
				Color = "#2eb886",
				Blocks = attachments
			}};
		}
	}
}