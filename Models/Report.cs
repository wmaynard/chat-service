using System.Linq;
using Rumble.Platform.Common.Exceptions;
using Rumble.Platform.Common.Minq;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Models;

public class Report : PlatformCollectionDocument
{
    public string OffendingMessageId { get; set; }
    public string FirstReporterId { get; set; }
    public string[] ReporterIds { get; set; }
    public int ReportedCount { get; set; }
    public Message[] Context { get; set; }
}

public class ReportService : MinqService<Report>
{
    private readonly MessageService _messages;

    public ReportService(MessageService messages) : base("reports")
        => _messages = messages;

    public Report Submit(string reporterId, string messageId)
    {
        Message[] context = _messages.GetContextAround(messageId);

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
                );

        Message[] merged = context
            .UnionBy(existing.Context, message => message.Id)
            .ToArray();

        return mongo
            .Where(query => query.EqualTo(report => report.OffendingMessageId, messageId))
            .UpdateAndReturnOne(update => update
                .AddItems(report => report.ReporterIds, limitToKeep: 50, reporterId)
                .Set(report => report.Context, merged)
                .Increment(report => report.ReportedCount)
            );
    }
}