using System.Linq;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Exceptions;
using Rumble.Platform.Common.Minq;
using Rumble.Platform.Common.Models;
using Rumble.Platform.Common.Utilities;

namespace Rumble.Platform.ChatService.Services;

public class ReportService : MinqService<Report>
{
    private readonly MessageService _messages;

    public ReportService(MessageService messages) : base("reports")
        => _messages = messages;

    public Report Submit(string reporterId, string messageId)
    {
        Message[] context = _messages.GetContextAround(messageId);
        string roomId = context
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message.RoomId))
            ?.RoomId;
        context = context
            .Select(message => message.Prune())
            .ToArray();

        if (context.FirstOrDefault(message => message.Id == messageId)?.AccountId == reporterId)
            throw new PlatformException("You can't report yourself.");
        
        Report existing = mongo
            .Where(query => query.EqualTo(report => report.OffendingMessageId, messageId))
            .FirstOrDefault();

        if (existing == null)
            return mongo
                .Where(query => query.EqualTo(report => report.OffendingMessageId, messageId))
                .Upsert(update => update
                    .AddItems(report => report.ReporterIds, limitToKeep: 50, reporterId)
                    .Increment(report => report.ReportedCount)
                    .Set(report => report.Context, context)
                    .SetOnInsert(report => report.FirstReporterId, reporterId)
                    .SetOnInsert(report => report.RoomId, roomId)
                );

        Message[] merged = context
            .UnionBy(existing.Context, message => message.Id)
            .OrderBy(message => message.CreatedOn)
            .ToArray();

        return mongo
            .Where(query => query.EqualTo(report => report.OffendingMessageId, messageId))
            .UpdateAndReturnOne(update => update
                .AddItems(report => report.ReporterIds, limitToKeep: 50, reporterId)
                .Set(report => report.Context, merged)
                .Increment(report => report.ReportedCount)
            );
    }

    public Report[] ListReports(string reportId, string accountId, int page, out long remaining)
    {
        remaining = 0;

        if (!string.IsNullOrWhiteSpace(reportId))
            return mongo.ExactId(reportId).ToArray();

        return string.IsNullOrWhiteSpace(accountId)
            ? mongo
                .All()
                .Sort(sort => sort
                    .OrderByDescending(report => report.Status)
                    .OrderByDescending(report => report.ReportedCount)
                    .OrderByDescending(report => report.CreatedOn)
                )
                .Page(10, page, out remaining)
            : mongo
                .Where(query => query.EqualTo(report => report.FirstReporterId, accountId))
                .Or(query => query.Contains(report => report.ReporterIds, accountId))
                .Or(query => query.Where(report => report.Context, innerQuery => innerQuery.EqualTo(message => message.AccountId, accountId)))
                .Sort(sort => sort
                    .OrderByDescending(report => report.Status)
                    .OrderByDescending(report => report.ReportedCount)
                    .OrderByDescending(report => report.CreatedOn)
                )
                .Page(10, page, out remaining);
    }

    public Report UpdateStatus(string reportId, ReportStatus status, TokenInfo admin) => mongo
        .ExactId(reportId)
        .UpdateAndReturnOne(update => update
            .Set(report => report.Status, status)
            .Set(report => report.Reviewer, admin)
        );

    public long DeleteUnnecessaryReports()
    {
        long affected = mongo
            .Where(query => query
                .EqualTo(report => report.Status, ReportStatus.Severe)
                .LessThan(report => report.CreatedOn, Timestamp.TwoWeeksAgo)
            )
            .Delete();
        affected += mongo
            .Where(query => query
                .EqualTo(report => report.Status, ReportStatus.Mild)
                .LessThan(report => report.CreatedOn, Timestamp.ThreeMonthsAgo)
            )
            .Delete();
        affected += mongo
            .Where(query => query
                .EqualTo(report => report.Status, ReportStatus.New)
                .LessThan(report => report.CreatedOn, Timestamp.SixMonthsAgo)
            )
            .Delete();

        return affected;
    }
}