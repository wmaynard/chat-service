using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Settings;
using Rumble.Platform.Common.Exceptions;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;
using Rumble.Platform.CSharp.Common.Interop;
using Foo = System.Timers.Timer;

namespace Rumble.Platform.ChatService.Services
{
	public class ReportService : RumbleMongoService
	{
		private const int SUMMARY_INTERVAL_MS = 21_600_000; // six hours
		
		private Timer SummaryTimer { get; set; }
		private new readonly IMongoCollection<Report> _collection;
		private readonly SlackMessageClient SlackReportChannel = new SlackMessageClient(
			channel: RumbleEnvironment.Variable("SLACK_REPORTS_CHANNEL"), 
			token: RumbleEnvironment.Variable("SLACK_CHAT_TOKEN"
		));

		public ReportService(ReportDBSettings settings) : base(settings)
		{
			Log.Verbose(Owner.Will, "Creating ReportService");
			_collection = _database.GetCollection<Report>(settings.CollectionName);
			SummaryTimer = new Timer(SUMMARY_INTERVAL_MS);
			SummaryTimer.Elapsed += SendSummaryReport;
			SummaryTimer.Start();
			SendSummaryReport(null, null);
		}

		private void SendSummaryReport(object sender, ElapsedEventArgs args)
		{
			SummaryTimer.Stop();
			try
			{
				List<Report> reports = _collection.Find(r => r.Status != Report.STATUS_BANNED).ToList();
				ReportMetrics[] metrics = ReportMetrics.Generate(ref reports)
					.Take(100)
					.ToArray();

				string title = $"{RumbleEnvironment.Variable("RUMBLE_DEPLOYMENT")} Reports Summary";
				
				
				int pad1 = metrics.Max(m => m.ReportedPlayer.UniqueScreenname.Length);
				
				string col1 = "Username".PadRight(pad1, ' ');
				string col2 = "Count   ";
				string col3 = "Users";
				string col4 = "Spam";
				string col5 = "Most Reported Messages";

				int pad2 = col2.Length;
				int pad3 = col3.Length;
				int pad4 = col4.Length;

				string header = $"```{col1} | {col2} | {col3} | {col4} | {col5}```";
				List<SlackBlock> blocks = new List<SlackBlock>()
				{
					new SlackBlock(SlackBlock.BlockType.HEADER, title),
					new SlackBlock(SlackBlock.BlockType.DIVIDER),
					new SlackBlock("*KEY*"),
					new SlackBlock($"`{col2.Trim()}`: The number of reports against a user.  If the user has had reports ignored before, they appear in parentheses."),
					new SlackBlock($"`{col3}`: The number of _unique_ players who have filed reports against the user."),
					new SlackBlock($"`{col4}`: The number of times any message has been repeated in the report logs for the player.  This could be innocent (like \"lol\"), so use your judgment."),
					new SlackBlock($"`{col5}`: These are messages that were the most reported.  The number is _uniqueReporters + timesRepeated_.  Only messages with at least one report and a number above {ReportMetrics.MENTION_THRESHOLD} are listed."),
					new SlackBlock(SlackBlock.BlockType.DIVIDER),
					new SlackBlock($"Here are the top {metrics.Length} offenders:"),
					new SlackBlock(header)
				};
				foreach (ReportMetrics rm in metrics.OrderByDescending(m => m.Severity))
				{
					string c1 = rm.ReportedPlayer.UniqueScreenname.PadRight(pad1, ' ');
					string c2 = rm.NewReportCount.ToString();
					if (rm.IgnoredReportCount > 0)
						c2 += $" {rm.IgnoredReportCount}";
					c2 = c2.PadRight(pad2, ' ');
					
					string c3 = rm.UniqueReporterCount.ToString().PadRight(pad3, ' ');
					string c4 = rm.RepeatedMessageCount.ToString().PadRight(pad4, ' ');
					string c5 = string.Join(", ", rm.MostReportedMessages);
					blocks.Add(new SlackBlock($"```{c1} | {c2} | {c3} | {c4} | {c5}```"));
				}
				Log.Info(Owner.Will, "ReportMetrics calculated.", data: new
				{
					ReportMetrics = metrics
				});
				blocks.AddRange(new []
				{
					new SlackBlock(SlackBlock.BlockType.DIVIDER),
					new SlackBlock("*Publishing App Links*"),
					new SlackBlock(string.Join(", ", metrics.Select(rm => rm.ReportedPlayer.SlackLink)))
				});
				
				SlackMessage message = new SlackMessage(blocks);
				SlackReportChannel.Send(message);
			}
			catch (Exception ex)
			{
				Log.Error(Owner.Will, "Unable to send the Reports Summary.", exception: ex);
			}

			SummaryTimer.Start();
		}

		public Report FindByPlayerAndMessage(string aid, string messageId)
		{
			return _collection.Find(filter: report => report.ReportedPlayer.AccountId == aid && report.MessageId == messageId).FirstOrDefault();
		}
		
		public Report Get(string id)
		{
			Report output = _collection.Find(filter: r => r.Id == id).FirstOrDefault();
			if (output == null)
				throw new RumbleException("Report not found!"); // TODO: ReportNotFoundException
			return output;
		}
		public List<Report> List() => _collection.Find(filter: r => true).ToList();
		public void Create(Report report) => _collection.InsertOne(document: report);

		public void UpdateOrCreate(Report report)
		{
			if (report.Id == null)
				Create(report);
			else
				Update(report);
		}

		public void Update(Report report) =>
			_collection.ReplaceOne(filter: r => r.Id == report.Id, replacement: report);

		public void Remove(Report report) => _collection.DeleteOne(filter: r => r.Id == report.Id);

		public object SendToSlack(Report report)
		{
			bool success = false;
			try
			{
				SlackReportChannel.Send(report.SlackMessage);
				success = true;
			}
			catch { }
			return new { SlackSuccess = success };
		}
	}
}