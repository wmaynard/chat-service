using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using MongoDB.Driver;
using RCL.Logging;
using Rumble.Platform.ChatService.Exceptions;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;
using Rumble.Platform.Common.Interop;
using Rumble.Platform.Common.Services;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Services;

public class ReportService : PlatformMongoService<Report>
{
	private const    int           SUMMARY_INTERVAL_MS = 21_600_000; // six hours
	private readonly DynamicConfig _config;
	private readonly ApiService    _apiService;

	private readonly SlackMessageClient SlackReportChannel;
	
	private Timer SummaryTimer { get; set; }
	
	private ReportMetrics[] PreviousMetrics { get; set; }
	private bool _initialized = false;

	public ReportService(DynamicConfig dynamicConfig) : base("reports")
	{
		try
		{
			_config = dynamicConfig;
			SlackReportChannel = new SlackMessageClient(
				channel: _config.Require<string>("reportsChannel") ?? PlatformEnvironment.SlackLogChannel,
				token: PlatformEnvironment.SlackLogBotToken
			);
			SummaryTimer = new Timer(SUMMARY_INTERVAL_MS);
			SummaryTimer.Elapsed += SendSummaryReport;
			SummaryTimer.Start();
			SendSummaryReport(null, null);
			_initialized = true;
		}
		catch (Exception e)
		{
			Log.Error(Owner.Will, "Could not initialize ReportService; no reports can be sent to Slack");
		}
	}
	
	#region CRUD
	public override Report Get(string id) => base.Get(id) ?? throw new ReportNotFoundException(id);

	public Report[] GetReportsForPlayer(string aid) => _collection.Find(report => report.ReportedPlayer.AccountId == aid).ToList().ToArray();
	
	public Report FindByPlayerAndMessage(string aid, string messageId) => _collection
		.Find(filter: report => report.ReportedPlayer.AccountId == aid && report.MessageId == messageId)
		.FirstOrDefault();
	
	// TODO: Worth adding to PlatformMongoService
	public void UpdateOrCreate(Report report)
	{
		if (report.Id == null)
			Create(report);
		else
			Update(report);
	}
	#endregion CRUD
	
	private void SendSummaryReport(object sender, ElapsedEventArgs args)
	{
		if (!_initialized)
			return;
		List<Report> reports = _collection.Find(r => r.Status != Report.STATUS_BANNED).ToList();
		if (!reports.Any())
		{
			Log.Info(Owner.Will, "Tried to send a report summary, but there are no reports.", localIfNotDeployed: true);
			return;
		}
			
		SummaryTimer?.Stop();
		try
		{
			ReportMetrics[] metrics = ReportMetrics.Generate(ref reports)
				.Take(100)
				.OrderByDescending(m => m.Severity)
				.ToArray();
			if (PreviousMetrics != null && metrics.Length == PreviousMetrics.Length && !metrics
				.Select(m => m.Equals(PreviousMetrics[Array.IndexOf(metrics, m)]))
				.Any(b => b == false))
			{
				Log.Verbose(Owner.Will, "Report metrics have been calculated, but are unchanged from last time.  No Slack message will be sent.");
				SummaryTimer?.Start();
				return;
			}
			PreviousMetrics = metrics;

			string title = $"{PlatformEnvironment.Deployment} Reports Summary";

			int pad1 = 19; // The length of a default name like "Playerc8bc9805#4223"
			try
			{
				pad1 = metrics.Max(m => m.ReportedPlayer.UniqueScreenname.Length);
			}
			catch (Exception e)
			{
				Log.Warn(Owner.Will, $"Unable to calculate padding for summary report's username field.  Using default value of {pad1} instead.", data: metrics, exception: e);
			}
			
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
			foreach (ReportMetrics rm in metrics)
			{
				string c1 = rm.ReportedPlayer.UniqueScreenname.PadRight(pad1, ' ');
				string c2 = rm.NewReportCount.ToString();
				if (rm.IgnoredReportCount > 0)
					c2 += $" ({rm.IgnoredReportCount})";
				c2 = c2.PadRight(pad2, ' ');
				
				string c3 = rm.UniqueReporterCount.ToString().PadRight(pad3, ' ');
				string c4 = rm.RepeatedMessageCount.ToString().PadRight(pad4, ' ');
				string c5 = string.Join(", ", rm.MostReportedMessages);
				blocks.Add(new SlackBlock($"```{c1} | {c2} | {c3} | {c4} | {c5}```"));
			}
			Log.Verbose(Owner.Will, "ReportMetrics calculated.", data: new
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
			Graphite.Track("report-severity", metrics.Sum(metric => metric.Severity), type: Graphite.Metrics.Type.AVERAGE);
		}
		catch (Exception ex)
		{
			Log.Error(Owner.Will, "Unable to send the Reports Summary.", exception: ex);
			
			_apiService.Alert(
				title: "Unable to send the Reports Summary.",
				message: "Unable to send the Reports Summary.",
				countRequired: 1,
				timeframe: 300,
				data: new RumbleJson
				    {
				        { "Exception", ex }
				    } 
			);

		}

		SummaryTimer?.Start();
	}
	
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