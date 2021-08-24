using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using MongoDB.Bson.Serialization.Conventions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using RestSharp;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Utilities;

namespace Rumble.Platform.ChatService.Models
{
	// TODO: This is very rough, needs cleanup
	public struct SlackBlock
	{
		public enum BlockType { HEADER, DIVIDER, MARKDOWN }

		[JsonProperty]
		public string Type { get; set; }
		
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public object Text { get; set; }
		
		public SlackBlock(BlockType type, string text = null)
		{
			Type = null;
			Text = null;
			switch (type)
			{
				case BlockType.HEADER:
					Type = "header";
					Text = new
					{
						Type = "plain_text",
						Text = text,
						Emoji = true
					};
					break;
				case BlockType.DIVIDER:
					Type = "divider";
					break;
				case BlockType.MARKDOWN:
					Type = "section";
					Text = new
					{
						Type = "mrkdwn",
						Text = text
					};
					break;
			}
		}
	}

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

		private static Dictionary<string, object> Send(string endpoint, string body)
		{
			Uri baseUrl = new Uri("https://slack.com/api/" + endpoint);
			IRestClient client = new RestClient(baseUrl);
			IRestRequest request = new RestRequest(Method.POST);
			request.AddHeader("Authorization", TOKEN);
			request.AddJsonBody(body);
			//
			// if (authorization != null)
			// 	request.AddHeader("Authorization", authorization);

			IRestResponse<Dictionary<string, object>> response = client.Execute<Dictionary<string, object>>(request);
			if (!response.IsSuccessful)
				throw new Exception(response.ErrorMessage);
			
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

		public SlackHelper()
		{
		}

		public static string User(PlayerInfo player)
		{
			return Link($"https://publishing-j8.cdrentertainment.com/cs/player/show?gukey={player.AccountId}", player.UniqueScreenname);
		}
		public static string Link(string url, string text)
		{
			return $"<{url}|{text}>";
		}
	}
}