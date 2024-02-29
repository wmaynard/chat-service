using System;
using System.Linq;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Exceptions;
using Rumble.Platform.Common.Interfaces;
using Rumble.Platform.Common.Minq;
using Rumble.Platform.Common.Utilities;

namespace Rumble.Platform.ChatService.Services;

public class MessageService : MinqService<Message>
{
    public const int MESSAGE_LIMIT = 100;
    public const int HARD_MESSAGE_LIMIT = 1_000;
    
    public MessageService() : base("messages") { }

    public Message[] AdminListMessages(string roomId, string accountId, string messageId, int page, out long remaining)
    {
        remaining = 0;
        
        return !string.IsNullOrWhiteSpace(messageId)
            ? mongo.ExactId(messageId).ToArray()
            : mongo
                .Where(query =>
                {
                    if (!string.IsNullOrWhiteSpace(roomId))
                        query.EqualTo(message => message.RoomId, roomId);
                    if (!string.IsNullOrWhiteSpace(accountId))
                        query.EqualTo(message => message.AccountId, accountId);
                })
                .Sort(sort => sort.OrderByDescending(message => message.Expiration))
                .Page(100, page, out remaining);
    }

    public long AssignType(string roomId, MessageType type) => mongo
        .Where(query => query
            .EqualTo(message => message.RoomId, roomId)
            .EqualTo(message => message.Type, MessageType.Unassigned)
        )
        .Update(update => update.Set(message => message.Type, type));

    public long DeleteAllMessages(string roomId, Transaction transaction) => mongo
        .WithTransaction(transaction)
        .Where(query => query.EqualTo(message => message.RoomId, roomId))
        .Delete();

    public long DeleteExpiredMessages() => mongo
        .Where(query => query
            .LessThan(message => message.Expiration, Timestamp.Now)
            .NotEqualTo(message => message.Type, MessageType.Announcement)
        )
        .Delete();

    public long DeleteMessages(string[] roomIds) => mongo
        .Where(query => query.ContainedIn(message => message.RoomId, roomIds))
        .Delete();

    public long DeleteMessages(string roomId, int countToKeep = 0)
    {
        if (countToKeep <= 0)
            return mongo
                .Where(query => query.EqualTo(message => message.RoomId, roomId))
                .Delete();
        
        long timestamp = mongo
            .Where(query => query.EqualTo(message => message.RoomId, roomId))
            .Sort(sort => sort.OrderByDescending(message => message.CreatedOn))
            .Limit(countToKeep)
            .Project(message => message.CreatedOn)
            .LastOrDefault();

        if (timestamp <= 0)
            return 0;

        return mongo
            .Where(query => query
                .EqualTo(message => message.RoomId, roomId)
                .LessThan(message => message.CreatedOn, timestamp)
            )
            .Delete();
    }

    public long ExpireExcessAnnouncements()
    {
        Message announcement = mongo
            .Where(query => query
                .EqualTo(message => message.Type, MessageType.Announcement)
                .GreaterThanOrEqualTo(message => message.Expiration, Timestamp.Now)
            )
            .Limit(10)
            .Sort(sort => sort.OrderByDescending(message => message.CreatedOn))
            .ToArray()
            .LastOrDefault();

        return announcement == null
            ? 0
            : mongo
                .Where(query => query
                    .EqualTo(message => message.Type, MessageType.Announcement)
                    .GreaterThanOrEqualTo(message => message.Expiration, Timestamp.Now)
                    .LessThan(message => message.CreatedOn, announcement.CreatedOn)
                )
                .Update(update => update.Set(message => message.Expiration, Timestamp.Now));
    }
    
    public Message[] GetAllMessages(string[] roomIds, long timestamp = 0) => mongo
        .Where(query => query
            .ContainedIn(message => message.RoomId, roomIds)
            .GreaterThanOrEqualTo(message => message.Expiration, Timestamp.Now)
            .Or(or => or
                .GreaterThanOrEqualTo(message => message.CreatedOn, timestamp)
                .GreaterThanOrEqualTo(message => message.UpdatedOn, timestamp)
            )
        )
        .Or(or => or.EqualTo(message => message.Type, MessageType.Announcement))
        .Sort(sort => sort.OrderBy(message => message.CreatedOn))
        .Limit(Math.Min(MESSAGE_LIMIT * roomIds.Length, HARD_MESSAGE_LIMIT))
        .ToArray();

    public Message[] GetContextAround(string messageId)
    {
        Message target = mongo.ExactId(messageId).FirstOrDefault();

        if (target == null)
            throw new PlatformException("Unable to fetch context");

        Message[] before = mongo
            .Where(query => query
                .EqualTo(message => message.RoomId, target.RoomId)
                .LessThanOrEqualTo(message => message.CreatedOn, target.CreatedOn)
            )
            .Sort(sort => sort.OrderByDescending(message => message.CreatedOn))
            .Limit(25)
            .ToArray();
        
        Message[] after = mongo
            .Where(query => query
                .EqualTo(message => message.RoomId, target.RoomId)
                .GreaterThanOrEqualTo(message => message.CreatedOn, target.CreatedOn)
            )
            .Sort(sort => sort.OrderBy(message => message.CreatedOn))
            .Limit(25)
            .ToArray();

        return before
            .Union(after)
            .DistinctBy(message => message.Id)
            .OrderBy(message => message.CreatedOn)
            .ToArray();
    }

    public string GetLastGlobalRoomId(string accountId, params string[] roomIds) => mongo
        .Where(query => query
            .EqualTo(message => message.AccountId, accountId)
            .ContainedIn(message => message.RoomId, roomIds)
        )
        .Sort(sort => sort.OrderByDescending(message => message.CreatedOn))
        .Limit(1)
        .FirstOrDefault()
        ?.RoomId;

    public string[] GetRoomIdsForUnknownTypes(int pageNumber, out long remaining) => mongo
        .Where(query => query.EqualTo(message => message.Type, MessageType.Unassigned))
        .Limit(1_000)
        .Page(1_000, pageNumber, out remaining)
        .Select(message => message.RoomId)
        .Distinct()
        .ToArray();

    public Message[] Search(string term) => mongo
        .Where(query => query
            .Or(or => or
                .ContainsSubstring(message => message.AccountId, term)
                .ContainsSubstring(message => message.Body, term)
                .ContainsSubstring(message => message.RoomId, term)
            )
        )
        .Limit(100)
        .ToArray(); // TODO: Rank for relevance

    public void Delete(string id) => mongo
        .ExactId(id)
        .Delete();
}