using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RestSharp;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Utilities;

namespace Rumble.Platform.ChatService.Models
{
	
	public class SlackHelper
	{
		public static readonly SlackMessageClient Client = new SlackMessageClient(
			channel: RumbleEnvironment.Variable("SLACK_SANDBOX_CHANNEL"),
			token: RumbleEnvironment.Variable("SLACK_CHAT_TOKEN")
		);
		public static Dictionary<string, object> Send(string endpoint, string body)
		{
			return Client.Send(body);
		}

		public static string User(PlayerInfo player)
		{
			return Link(RumbleEnvironment.Variable("RUMBLE_PUBLISHING_PLAYER_URL") + $"?gukey={player.AccountId}", player.UniqueScreenname);
		}

		private static string Link(string url, string text)
		{
			return $"<{url}|{text}>";
		}
	}
}