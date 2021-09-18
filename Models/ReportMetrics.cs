using System;
using System.Collections.Generic;
using System.Linq;
using Rumble.Platform.CSharp.Common.Interop;

namespace Rumble.Platform.ChatService.Models
{
	
	public class ReportMetrics
	{
		private const int WEIGHT_UNIQUE_REPORTS = 10;
		private const int WEIGHT_UNIQUE_REPORTERS = 100;
		private const int WEIGHT_EXPONENTIAL_REPEATED_MESSAGES = 2;
		// public string AccountId { get; private set; }
		public int RepeatedMessageCount { get; private set; }
		public PlayerInfo ReportedPlayer { get; private set; }
		public int NewReportCount { get; private set; }
		public int UniqueReporterCount { get; private set; }
		public int IgnoredReportCount { get; private set; }
		public string[] MostReportedMessages { get; private set; }

		public long Severity =>
			NewReportCount * WEIGHT_UNIQUE_REPORTS
			+ UniqueReporterCount * WEIGHT_UNIQUE_REPORTERS
			+ (long)Math.Pow(RepeatedMessageCount, WEIGHT_EXPONENTIAL_REPEATED_MESSAGES);

		public const int MENTION_THRESHOLD = 5;
		public ReportMetrics(IGrouping<string, Report> group)
		{
			ReportedPlayer = group.First().ReportedPlayer;
			Report[] _new = group.Where(r => r.Status == Report.STATUS_UNADDRESSED).ToArray();
			Report[] _ignored = group.Where(r => r.Status == Report.STATUS_BENIGN).ToArray();
			Message[] messages = _new
				.SelectMany(r => r.Log)
				.Where(message => message.Type == Message.TYPE_CHAT && message.AccountId == ReportedPlayer.AccountId)
				.GroupBy(message => message.Id)
				.Select(grouping => grouping.First())
				.ToArray();
		
			RepeatedMessageCount = messages.Length - messages.Select(m => m.Text).Distinct().Count();
			
			MostReportedMessages = group
				.ToDictionary(
					keySelector: report => report.ReportedMessage.Text, 
					elementSelector: report => report.Reporters.Count + messages.Count(m => m.Text == report.ReportedMessage.Text)
				).Where(kvp => kvp.Value >= MENTION_THRESHOLD)
				.OrderByDescending(kvp => kvp.Value)
				.ThenBy(kvp => kvp.Key)
				.Select(kvp => $"({kvp.Value}) {kvp.Key}")
				.ToArray();
			
			UniqueReporterCount = group
				.SelectMany(report => report.Reporters
					.Select(reporter => reporter.AccountId)
				).Distinct()
				.Count();
			NewReportCount = _new.Length;
			IgnoredReportCount = _ignored.Length;
		}
		
		public static ReportMetrics[] Generate(ref List<Report> reports)
		{
			return reports
				.GroupBy(report => report.ReportedPlayer.AccountId)
				.Select(grouping => new ReportMetrics(grouping))
				.OrderByDescending(metrics => metrics.Severity)
				.ToArray();
		}
	}
}