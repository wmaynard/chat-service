using System.Collections.Generic;
using Newtonsoft.Json;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.CSharp.Common.Interop;

namespace Rumble.Platform.ChatService.Models
{
	public struct SlackReport // TODO: PlatformDataModel?
	{
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public object[] Attachments { get; set; }
		
		[JsonProperty]
		public List<SlackBlock> Blocks { get; set; }
		
		[JsonProperty(PropertyName = "channel")]
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