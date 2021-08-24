using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Timers;
using MongoDB.Bson.Serialization.Conventions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using RestSharp;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Exceptions;
using Rumble.Platform.Common.Utilities;

namespace Rumble.Platform.ChatService.Models
{
	public struct SlackReport
	{
		[JsonProperty(PropertyName = "channel")]
		public string DestinationChannel { get; set; }
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public object[] Attachments { get; set; }
		[JsonProperty]
		public List<SlackBlock> Blocks { get; set; }

		public SlackReport(List<SlackBlock> blocks, List<SlackBlock> attachments)
		{
			DestinationChannel = RumbleEnvironment.Variable("SLACK_REPORTS_CHANNEL");
			Blocks = blocks;
			Attachments = new object[]{ new
			{
				Color = "#2eb886",
				Blocks = attachments
			}};
		}
	}
}