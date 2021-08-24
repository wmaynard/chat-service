using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RestSharp;
using Rumble.Platform.ChatService.Utilities;

namespace Rumble.Platform.ChatService.Models
{
	
	public class SlackHelper
	{
		public static readonly string POST_MESSAGE = RumbleEnvironment.Variable("SLACK_ENDPOINT_POST_MESSAGE");
		// public static readonly string TOKEN = "Bearer " + Environment.GetEnvironmentVariable("CHAT_SLACK_TOKEN");
		public static readonly string TOKEN = "Bearer " + RumbleEnvironment.Variable("SLACK_CHAT_TOKEN");
		
		public string JsonifyMessage(DateTime date, PlayerInfo author, string messages)
		{
			return JsonConvert.SerializeObject(new
			{
				type = "section",
				text = new
				{
					type = "mrkdwn",
					txt = $"`{date:HH:mm}` *{User(author)}*\n{messages}"
				}
			});
		}

		public static Dictionary<string, object> Send(string endpoint, string body)
		{
			Uri baseUrl = new Uri(endpoint);
			IRestClient client = new RestClient(baseUrl);
			IRestRequest request = new RestRequest(Method.POST);
			request.AddHeader("Authorization", TOKEN);
			request.AddJsonBody(body);

			IRestResponse<Dictionary<string, object>> response = client.Execute<Dictionary<string, object>>(request);
			if (!response.IsSuccessful)
				throw new Exception(response.ErrorMessage + $" JSON string: ({body})");
			var ok = response.Data["ok"];
			if (ok.ToString().ToLower() != "true")
			{
				throw new Exception("Response came back as " + ok.ToString() + ". Error: " + response.Data["error"].ToString());
			}
			return response.Data;
		}

		public static void SendReport(PlayerInfo reporter, IEnumerable<Message> messages, IEnumerable<PlayerInfo> players)
		{
			Send(POST_MESSAGE, GenerateReport(reporter, messages, players));
		}

		private static string GenerateReport(PlayerInfo reporter, IEnumerable<Message> messages, IEnumerable<PlayerInfo> players)
		{
			Message reported = messages.First(m => m.Reported == true);
			PlayerInfo defendant = players.First(p => p.AccountId == reported.AccountId);
			DateTime now = DateTime.Now;
			List<SlackBlock> headers = new List<SlackBlock>()
			{
				new(SlackBlock.BlockType.HEADER, $"New Report | {now:yyyy.MM.dd HH:mm}"),
				new(SlackBlock.BlockType.MARKDOWN, $"Reported Player: {User(defendant)}\nReporter: {User(reporter)}"),
				new(SlackBlock.BlockType.MARKDOWN, "_The message flagged by the user is indicated by a *!* and special formatting._"),
				new(SlackBlock.BlockType.DIVIDER)
			};
			List<SlackBlock> blox = new List<SlackBlock>();
			string aid = null;
			DateTime lastDate = DateTime.UnixEpoch;
			PlayerInfo author = null;
			string entries = "";
			foreach (Message m in messages)
			{
				string text = "";
				if (m.AccountId != aid)
				{
					if (aid != null) // We've seen at least one user
					{
						author = players.First(p => p.AccountId == aid);
						blox.Add(new (SlackBlock.BlockType.MARKDOWN, $"`{lastDate:HH:mm}` *{User(author)}*\n{entries}"));
					}
					aid = m.AccountId;
					lastDate = m.Date;
					entries = "";
				}

				string txt = m.Text.Replace('\n', ' ').Replace('`', '\'');
				entries += m.Reported == true
					? ":exclamation:`" + txt + "`\n"
					: txt + "\n";
			}
			author = players.First(p => p.AccountId == aid);
			blox.Add(new (SlackBlock.BlockType.MARKDOWN, $"`{lastDate:HH:mm}` *{User(author)}*\n{entries}"));
			return JsonConvert.SerializeObject(
				new SlackReport(headers, blox), 
				new JsonSerializerSettings(){ContractResolver = new CamelCasePropertyNamesContractResolver()}
			);
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