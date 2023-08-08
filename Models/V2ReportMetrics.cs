using System;
using System.Collections.Generic;
using System.Linq;

namespace Rumble.Platform.ChatService.Models;

// TODO: Comments
public class V2ReportMetrics
{
	public const  int      MENTION_THRESHOLD                    = 5;
	private const int      WEIGHT_UNIQUE_REPORTS                = 10;
	private const int      WEIGHT_UNIQUE_REPORTERS              = 100;
	private const int      WEIGHT_EXPONENTIAL_REPEATED_MESSAGES = 2;
	public        int      RepeatedMessageCount { get; private set; }
	public        string   ReportedPlayer       { get; private set; }
	public        int      NewReportCount       { get; private set; }
	public        int      UniqueReporterCount  { get; private set; }
	public        int      IgnoredReportCount   { get; private set; }
	public        string[] MostReportedMessages { get; private set; }

	public long Severity =>
		NewReportCount * WEIGHT_UNIQUE_REPORTS
		+ UniqueReporterCount * WEIGHT_UNIQUE_REPORTERS
		+ (long)Math.Pow(RepeatedMessageCount, WEIGHT_EXPONENTIAL_REPEATED_MESSAGES);

	public V2ReportMetrics(IGrouping<string, V2Report> group)
	{
		ReportedPlayer = group.First().ReportedPlayer;
		V2Report[] _new = group.Where(r => r.Status == V2Report.V2ReportStatus.New).ToArray();
		V2Report[] _ignored = group.Where(r => r.Status == V2Report.V2ReportStatus.Ignored).ToArray();
		V2Message[] messages = _new
		                       .SelectMany(r => r.Log)
		                       .Where(message => message.Type == V2Message.V2MessageType.Chat && message.AccountId == ReportedPlayer)
		                       .GroupBy(message => message.Id)
		                       .Select(grouping => grouping.First())
		                       .ToArray();
	
		RepeatedMessageCount = messages.Length - messages.Select(m => m.Text).Distinct().Count();
		
		MostReportedMessages = group
			.ToDictionary(
				keySelector: report => report.ReportedMessage.Text, 
				elementSelector: report => report.Reporters.Count + messages.Count(m => m.Text == report.ReportedMessage.Text)
			).Where(pair => pair.Value >= MENTION_THRESHOLD)
			.OrderByDescending(pair => pair.Value)
			.ThenBy(pair => pair.Key)
			.Select(pair => $"({pair.Value}) {pair.Key}")
			.ToArray();
		
		UniqueReporterCount = group
			.SelectMany(report => report.Reporters
				.Select(reporter => reporter)
			).Distinct()
			.Count();
		NewReportCount = _new.Length;
		IgnoredReportCount = _ignored.Length;
	}
	
	public static V2ReportMetrics[] Generate(ref List<V2Report> reports) => reports
	                                                                        .GroupBy(report => report.ReportedPlayer)
	                                                                        .Select(grouping => new V2ReportMetrics(grouping))
	                                                                        .OrderByDescending(metrics => metrics.Severity)
	                                                                        .ToArray();

	public bool Equals(V2ReportMetrics other)
	{
		try
		{
			return ReportedPlayer == other.ReportedPlayer
				&& Severity == other.Severity
				&& IgnoredReportCount == other.IgnoredReportCount
				&& NewReportCount == other.NewReportCount
				&& UniqueReporterCount == other.UniqueReporterCount
				&& RepeatedMessageCount == other.RepeatedMessageCount
				&& MostReportedMessages.SequenceEqual(other.MostReportedMessages);
		}
		catch
		{
			return false;
		}
	}

	public override int GetHashCode() => HashCode.Combine(RepeatedMessageCount, ReportedPlayer, NewReportCount, UniqueReporterCount, IgnoredReportCount, MostReportedMessages);
}