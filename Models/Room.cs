using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using RCL.Logging;
using Rumble.Platform.Common.Minq;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Models;

public class Room : PlatformCollectionDocument
{
    public string[] Members { get; set; }
    
    public RoomType Type { get; set; }
    
    [BsonIgnore]
    public Message[] Messages { get; set; }
    
    public long MembershipUpdatedMs { get; set; }
    
    [BsonIgnoreIfDefault]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long FriendlyId { get; set; }

    public Room Prune()
    {
        Members = null;
        return this;
    }
}

public enum RoomType
{
    Global,
    DirectMessage,
    Private,
    PossibleHack
}

public class RoomService : MinqService<Room>
{
    private readonly MessageService _messages;

    public RoomService(MessageService messages) : base("rooms")
        => _messages = messages;

    /// <summary>
    /// Returns all of the Rooms a player is participating in.  This method guarantees that the player will only ever
    /// be in one Global Room at a time.
    /// </summary>
    /// <param name="accountId"></param>
    /// <returns></returns>
    public Room[] GetMembership(string accountId)
    {
        List<Room> output = mongo
            .Where(query => query.Contains(room => room.Members, accountId))
            .ToList();
        
        // Check to make sure the player is not in more than one global room.  If they are, we need to remove them from
        // all but one.
        if (output.Count(room => room.Type == RoomType.Global) > 1)
        {
            string[] globalIds = output
                .Where(room => room.Type == RoomType.Global)
                .Select(room => room.Id)
                .ToArray();
                
            // Find the room they've most recently posted in.  If there are no messages, they'll be fully removed from
            // all global rooms.
            string toKeep = _messages.GetLastGlobalRoomId(accountId, globalIds);

            string[] toRemove = string.IsNullOrWhiteSpace(toKeep)
                ? globalIds
                : globalIds.Where(id => id != toKeep).ToArray();

            RemoveMember(accountId, toRemove);
            output.RemoveAll(room => toRemove.Contains(room.Id));
        }
        
        if (output.All(room => room.Type != RoomType.Global))
            output.Add(JoinGlobal(accountId));

        return output
            .Where(room => room != null)
            .ToArray();
    }

    private Room JoinGlobal(string accountId)
    {
        Room output = mongo
            .Where(query => query.Contains(room => room.Members, accountId))
            .Or(or => or
                .EqualTo(room => room.Type, RoomType.Global)
                .LengthLessThanOrEqualTo(room => room.Members, 200)
            )
            .Upsert(update => update
                .AddItems(room => room.Members, accountId)
                .Set(room => room.MembershipUpdatedMs, TimestampMs.Now)
                .SetOnInsert(room => room.Type, RoomType.Global)
            );

        if (output.FriendlyId != default)
            return output;
        
        // The global room we're joining doesn't yet have its ID number.  Let's give it one.
        // TODO: We may need to account for a very difficult-to-produce edge case where two global rooms are created at the same time
        // and have a collision on the FriendlyId.
        long max = mongo
            .Where(query => query.EqualTo(room => room.Type, RoomType.Global))
            .Sort(sort => sort.OrderByDescending(room => room.FriendlyId))
            .Limit(1)
            .FirstOrDefault()
            ?.FriendlyId
            ?? 0;

        mongo
            .ExactId(output.Id)
            .Update(update => update.Set(room => room.FriendlyId, ++max));
        
        Log.Info(Owner.Will, "A global room was assigned a friendly ID number.", data: new
        {
            RoomId = output.Id,
            Number = max
        });

        return output;
    }

    public long RemoveFromGlobalRooms(Transaction transaction, params string[] accountIds) => mongo
        .WithTransaction(transaction)
        .Where(query => query.EqualTo(room => room.Type, RoomType.Global))
        .Update(update => update.RemoveItems(room => room.Members, accountIds));

    private long RemoveMember(string accountId, params string[] roomIds) => mongo
        .Where(query => query.ContainedIn(room => room.Id, roomIds))
        .Update(update => update.RemoveItems(room => room.Members, accountId));

    public bool TryRemoveMembers(Transaction transaction, string roomId, params string[] accountIds) => mongo
        .WithTransaction(transaction)
        .ExactId(roomId)
        .Update(update => update.RemoveItems(room => room.Members, accountIds)) > 0;

    public string[] ListGlobalRooms() => mongo
        .Where(query => query.EqualTo(room => room.Type, RoomType.Global))
        .Project(room => room.Id);
    
    public RoomType GetRoomType(string id) => mongo
        .ExactId(id)
        .FirstOrDefault()
        ?.Type
        ?? RoomType.PossibleHack;
}