using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using Rumble.Platform.Common.Attributes;
using Rumble.Platform.Common.Web;
using Rumble.Platform.Common.Interop;
using Rumble.Platform.Common.Models;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Models;

public class V2Report : PlatformCollectionDocument
{
	public const int COUNT_MESSAGES_BEFORE_REPORTED = 20;
	public const int COUNT_MESSAGES_AFTER_REPORTED = 10;

	internal const string DB_KEY_MESSAGE_ID = "mid";
	internal const string DB_KEY_MESSAGE_LOG = "log";
	internal const string DB_KEY_REASON = "why";
	internal const string DB_KEY_REPORTED = "rptd";
	internal const string DB_KEY_REPORTER = "rptr";
	internal const string DB_KEY_PLAYERS = "who";
	internal const string DB_KEY_STATUS = "st";
	internal const string DB_KEY_TIMESTAMP = "ts";
	
	public const string FRIENDLY_KEY_MESSAGE_ID = "messageId";
	public const string FRIENDLY_KEY_MESSAGE_LOG = "log";
	public const string FRIENDLY_KEY_PLAYERS = "players";
	public const string FRIENDLY_KEY_REASON = "reason";
	public const string FRIENDLY_KEY_REPORTED = "reported";
	public const string FRIENDLY_KEY_REPORTER = "reporters";
	public const string FRIENDLY_KEY_STATUS = "status";
	public const string FRIENDLY_KEY_TIMESTAMP = "time";

#region MONGO
	[BsonElement(DB_KEY_MESSAGE_LOG)]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_MESSAGE_LOG)]
	public IEnumerable<V2Message> Log { get; set; }
	
	[SimpleIndex]
	[BsonElement(DB_KEY_MESSAGE_ID)]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_MESSAGE_ID)]
	public string MessageId { get; set; }

	[BsonElement(DB_KEY_PLAYERS)]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_PLAYERS)]
	public IEnumerable<string> Players { get; set; }
	
	[BsonElement(DB_KEY_REASON), BsonIgnoreIfNull]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_REASON), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string Reason { get; set; }
	
	[BsonElement(DB_KEY_REPORTED)]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_REPORTED)]
	public string ReportedPlayer { get; set; }

	[BsonElement(DB_KEY_REPORTER)]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_REPORTER)]
	public HashSet<string> Reporters { get; private set; }
	
	public enum V2ReportStatus
	{
		New,
		Banned,
		Ignored
	}
	[BsonElement(DB_KEY_STATUS)]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_STATUS)]
	public V2ReportStatus Status { get; set; }
	
	[BsonElement(DB_KEY_TIMESTAMP)]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_TIMESTAMP)]
	public long Timestamp { get; set; }
	#endregion MONGO
	
	#region INTERNAL
	[BsonIgnore]
	[JsonIgnore]
	public V2Message ReportedMessage => Log.FirstOrDefault(m => m is V2ReportedMessage);
	
	[BsonIgnore]
	[JsonIgnore]
	public SlackMessage SlackMessage
	{
		get
		{
			List<SlackBlock> headers = new List<SlackBlock>()
			{
				new(SlackBlock.BlockType.HEADER, $"{PlatformEnvironment.Deployment} | {(Reporters.Count > 1 ? "Updated" : "New")} Report | {DateTime.Now:yyyy.MM.dd HH:mm}"),
				new($"Reported Player: {ReportedPlayer}\nReporter{(Reporters.Count > 1 ? "s" : "")}: {string.Join(", ", Reporters)}"),
				new("_The message flagged by the user is indicated by a *!* and special formatting._"),
				new(SlackBlock.BlockType.DIVIDER)
			};
			List<SlackBlock> blocks = new List<SlackBlock>();

			string author = null;
			string aid = null;
			string entries = "";
			DateTime lastDate = DateTime.UnixEpoch;
			foreach (V2Message m in Log)
			{
				if (m.AccountId != aid)
				{
					if (aid != null)
					{
						author = Players.First(p => p == aid);
						blocks.Add(new ($"`{lastDate:HH:mm}` *{author}*\n{entries}"));
					}
					aid = m.AccountId;
					lastDate = m.Date;
					entries = "";
				}

				string text = m.Text.Replace('\n', ' ').Replace('`', '\'');
				entries += m is V2ReportedMessage
					? $":exclamation:`{text}`\n"
					: $"{text}\n";
			}

			author = Players.First(p => p == aid);
			blocks.Add(new ($"`{lastDate:HH:mm}` *{author}*\n{entries}"));

			return new SlackMessage(
				blocks: headers, 
				attachments: new SlackAttachment("#2eb886", blocks));
		}
	}
	#endregion INTERNAL

	public V2Report()
	{
		Timestamp = Rumble.Platform.Common.Utilities.Timestamp.UnixTime;
		Status = V2ReportStatus.New;
		Reporters = new HashSet<string>();
	}

	public bool AddReporter(string reporter)
	{
		if (Reporters.Any(r => r == reporter))
		{
			return false;
		}

		Reporters.Add(reporter);
		return true;
	}
}