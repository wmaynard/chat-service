using System;
using System.Collections.Generic;
using System.Linq;
using RCL.Logging;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Exceptions;
using Rumble.Platform.Common.Extensions;
using Rumble.Platform.Common.Minq;
using Rumble.Platform.Common.Models;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Services;

public class RoomService : MinqService<Room>
{
    public const int ROOM_LIST_PAGE_SIZE = 10;
    
    private readonly MessageService _messages;

    public RoomService(MessageService messages) : base("rooms")
        => _messages = messages;

    public Room[] AdminListRooms(string roomId, string accountId, int page, out long remaining)
    {
        remaining = 0;

        if (!string.IsNullOrWhiteSpace(roomId))
            return mongo.ExactId(roomId).ToArray();

        return string.IsNullOrWhiteSpace(accountId)
            ? mongo
                .All()
                .Sort(sort => sort
                    .OrderBy(room => room.Type)
                    .OrderByDescending(room => room.FriendlyId)
                    .OrderBy(room => room.CreatedOn)
                )
                .Page(10, page, out remaining)
            : mongo
                .Where(query => query.Contains(room => room.Members, accountId))
                .Sort(sort => sort
                    .OrderBy(room => room.Type)
                    .OrderByDescending(room => room.FriendlyId)
                    .OrderBy(room => room.CreatedOn)
                )
                .Page(10, page, out remaining);
    }

    public Room AdminUpdate(string roomId, string[] roster, RumbleJson data, TokenInfo admin) => mongo
        .ExactId(roomId)
        .UpdateAndReturnOne(update =>
        {
            update
                .Set(room => room.Members, roster)
                .Set(room => room.Editor, admin);

            if (data != null)
                update.Set(room => room.Data, data);
        })
        ?? throw new PlatformException("Room not found, could not update roster.");

    /// <summary>
    /// Places the player into a global room, creating one if necessary.
    /// </summary>
    /// <param name="accountId"></param>
    /// <returns></returns>
    private Room AutoJoinGlobal(string accountId)
    {
        Room output = mongo
            .Where(query => query.Contains(room => room.Members, accountId))
            .Or(or => or
                .EqualTo(room => room.Type, RoomType.Global)
                .LengthLessThanOrEqualTo(room => room.Members, Room.CAPACITY_MEMBERS)
            )
            .Upsert(update => update
                .AddItems(room => room.Members, accountId)
                .Set(room => room.MembershipUpdatedMs, TimestampMs.Now)
                .SetOnInsert(room => room.Type, RoomType.Global)
                .SetOnInsert(room => room.Channel, BroadcastChannel.Global)
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

        output = mongo
            .ExactId(output.Id)
            .UpdateAndReturnOne(update => update.Set(room => room.FriendlyId, ++max));
        
        Log.Info(Owner.Will, "A global room was assigned a friendly ID number.", data: new
        {
            RoomId = output.Id,
            Number = max
        });

        return output;
    }

    public bool Delete(string id, out long messagesDeleted)
    {
        messagesDeleted = 0;
        
        bool output = mongo
            .WithTransaction(out Transaction transaction)
            .ExactId(id)
            .Delete() == 0;

        if (!output)
        {
            Abort(transaction);
            return false;
        }

        try
        {
            messagesDeleted = _messages.DeleteAllMessages(id, transaction);
            Commit(transaction);
            return true;
        }
        catch (Exception e)
        {
            Log.Error(Owner.Will, "Could not delete room and messages", exception: e);
            Abort(transaction);
            throw;
        }
    }

    public long DeleteEmptyPrivateRooms() => mongo
        .Where(query => query
            .EqualTo(room => room.Type, RoomType.Private)
            .LengthLessThan(room => room.Members, 1)
        )
        .Delete();

    public void DeleteInactiveDmRooms(out long deletedRooms, out long deletedMessages)
    {
        string[] toDelete = mongo
            .Where(query => query
                .EqualTo(room => room.Type, RoomType.DirectMessage)
                .Or(or => or
                    .LessThan(room => room.MembershipUpdatedMs, TimestampMs.OneWeekAgo)
                    .LengthLessThan(room => room.Members, 2)
                )
            )
            .Project(room => room.Id);

        deletedMessages = _messages.DeleteMessages(toDelete);
        deletedRooms = mongo
            .Where(query => query.ContainedIn(room => room.Id, toDelete))
            .Delete();
    }

    public string GetDmRoom(params string[] accountIds) => mongo
        .Where(query => query.EqualTo(room => room.Members, accountIds.OrderBy(_ => _).ToArray()))
        .Upsert(update => update
            .Set(room => room.MembershipUpdatedMs, TimestampMs.Now)
            .SetOnInsert(room => room.Type, RoomType.DirectMessage)
            .SetOnInsert(room => room.Channel, BroadcastChannel.None)
        )
        .Id;

    /// <summary>
    /// Returns all of the Rooms a player is participating in.  This method guarantees that the player will only ever
    /// be in one Global Room at a time.
    /// </summary>
    /// <param name="accountId"></param>
    /// <param name="channel"></param>
    /// <returns></returns>
    public Room[] GetMembership(string accountId, BroadcastChannel channel = BroadcastChannel.All)
    {
        List<Room> output = mongo
            .Where(query =>
            {
                query.Contains(room => room.Members, accountId);
                if (channel == BroadcastChannel.All)
                    return;
                
                BroadcastChannel[] flags = channel
                    .GetFlags()
                    .Where(bc => bc != BroadcastChannel.None)
                    .ToArray();
                query.ContainedIn(room => room.Channel, flags);
            })
            .Limit(100)
            .Sort(sort => sort
                .OrderByDescending(room => room.Type)
                .ThenByDescending(room => room.MembershipUpdatedMs)
            )
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
            output.Add(AutoJoinGlobal(accountId));

        return output
            .Where(room => room != null)
            .ToArray();
    }
    
    public RoomType GetRoomType(string id) => mongo
        .ExactId(id)
        .FirstOrDefault()
        ?.Type
        ?? RoomType.PossibleHack;

    public Room JoinGlobal(string accountId, string roomId)
    {
        mongo
            .WithTransaction(out Transaction transaction)
            .Where(query => query.Contains(room => room.Members, accountId))
            .Update(update => update.RemoveItems(room => room.Members, accountId));

        Room output = mongo
            .WithTransaction(transaction)
            .Where(query => query
                .EqualTo(room => room.Id, roomId)
                .EqualTo(room => room.Type, RoomType.Global)
                .LengthLessThanOrEqualTo(room => room.Members, Room.CAPACITY_OVERFLOW)
            )
            .UpdateAndReturnOne(update => update.AddItems(room => room.Members, accountId));

        if (output == null)
        {
            Abort(transaction);
            throw new PlatformException("Join request failed.  Room could not be found or is full.");
        }

        Commit(transaction);
        return output;
    }

    public bool Leave(string accountId, string roomId) => mongo
        .ExactId(roomId)
        .And(query => query.ContainedIn(room => room.Type, new[] { RoomType.DirectMessage }))
        .Update(update => update.RemoveItems(room => room.Members, accountId)) > 0;

    public long RemoveFromGlobalRooms(Transaction transaction, params string[] accountIds) => mongo
        .WithTransaction(transaction)
        .Where(query => query.EqualTo(room => room.Type, RoomType.Global))
        .Update(update => update.RemoveItems(room => room.Members, accountIds));

    private long RemoveMember(string accountId, params string[] roomIds) => mongo
        .Where(query => query.ContainedIn(room => room.Id, roomIds))
        .Update(update => update.RemoveItems(room => room.Members, accountId));
    
    public Room[] ListGlobalRoomsWithCapacity(int page, out long remainingRooms) => mongo
        .Where(query => query
            .EqualTo(room => room.Type, RoomType.Global)
            .LengthLessThan(room => room.Members, Room.CAPACITY_MEMBERS)
        )
        .Sort(sort => sort.OrderBy(room => room.FriendlyId))
        .Page(ROOM_LIST_PAGE_SIZE, page, out remainingRooms);

    public Room[] ListRoomsByType(RoomType type, int page, out long remaining) => mongo
        .Where(query => query.EqualTo(room => room.Type, type))
        .Page(100, page, out remaining);
}