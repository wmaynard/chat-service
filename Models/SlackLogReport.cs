using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Rumble.Platform.ChatService.Utilities;

namespace Rumble.Platform.ChatService.Models
{
	/// <summary>
	/// Container for chat messages just prior to serializing in a web request.  Use the JSON property to get the
	/// serialized request body.
	/// </summary>
	public struct SlackLogReport // TODO: Probably makes sense to have this inherit from a SlackMessage class
	{
		[JsonIgnore]
		public static readonly string SLACK_MONITOR_CHANNEL = RumbleEnvironment.Variable("SLACK_MONITOR_CHANNEL");
		[JsonProperty(PropertyName = "channel")]
		public string DestinationChannel { get; set; }
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public object[] Attachments { get; set; }
		[JsonProperty]
		public List<SlackBlock> Blocks { get; set; }
		
		[JsonIgnore]
		public string JSON => JsonConvert.SerializeObject(
			this, 
			new JsonSerializerSettings(){ContractResolver = new CamelCasePropertyNamesContractResolver()}
		);

		public SlackLogReport (IEnumerable<SlackLog> logs)
		{
			DestinationChannel = SLACK_MONITOR_CHANNEL;
			Blocks = new List<SlackBlock>() { };
			Attachments = logs.Select(l => new
			{
				Color = l.Color,
				Blocks = l.Content
			}).ToArray();
		}

		public void Send()
		{
			SlackHelper.Send(RumbleEnvironment.Variable("SLACK_ENDPOINT_POST_MESSAGE"), JSON);
		}
	}
}