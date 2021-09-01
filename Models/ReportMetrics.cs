using System;
using System.Collections.Generic;
using System.Linq;

namespace Rumble.Platform.ChatService.Models
{
	
	public class ReportMetrics
	{
		private const int WEIGHT_UNIQUE_REPORTS = 100;
		private const int WEIGHT_UNIQUE_REPORTERS = 10;
		private const int WEIGHT_EXPONENTIAL_REPEATED_MESSAGES = 2;
		public string AccountId { get; private set; }
		public int RepeatedMessages { get; private set; }
		public int TotalNewReports { get; private set; }
		public int TotalNewReporters { get; private set; }
		public int TotalIgnoredReports { get; private set; }
		public string[] MostReportedMessages { get; private set; }

		public long Severity =>
			TotalNewReports * WEIGHT_UNIQUE_REPORTS
			+ TotalNewReporters * WEIGHT_UNIQUE_REPORTERS
			+ (long)Math.Pow(WEIGHT_EXPONENTIAL_REPEATED_MESSAGES, RepeatedMessages);

		public const int MENTION_THRESHOLD = 5;
		public ReportMetrics(IGrouping<string, Report> group)
		{
			Report[] _new = group.Where(r => r.Status == Report.STATUS_UNADDRESSED).ToArray();
			Report[] _ignored = group.Where(r => r.Status == Report.STATUS_BENIGN).ToArray();
			Message[] messages = _new
				.SelectMany(r => r.Log)
				.ToArray();
		
			AccountId = group.Key;
			RepeatedMessages = messages.Length - messages.Select(m => m.Text).Distinct().Count();

			MostReportedMessages = messages
				.Where(message => message.Reported == true)
				.GroupBy(message => message.Id)
				.Select(grouping => new Tuple<string, int>(grouping.First().Text, grouping.Count()))
				.Where(tuple => tuple.Item2 > MENTION_THRESHOLD)
				.OrderByDescending(tuple => tuple.Item2)
				.Select(tuple => $"({tuple.Item2}) \"{tuple.Item1}\"")
				.ToArray();
			
			TotalNewReporters = group.Sum(r => r.Reporters.Count);
			TotalNewReports = _new.Length;
			TotalIgnoredReports = _ignored.Length;
		}
		
		public static ReportMetrics[] Generate(ref List<Report> reports)
		{
			return reports
				.GroupBy(report => report.Reported.AccountId)
				.Select(grouping => new ReportMetrics(grouping))
				.OrderByDescending(metrics => metrics.Severity)
				.ToArray();
		}
	}
}