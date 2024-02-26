using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using RCL.Logging;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Attributes;
using Rumble.Platform.Common.Exceptions;
using Rumble.Platform.Common.Extensions;
using Rumble.Platform.Common.Models;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Controllers;

[Route("chat/admin"), RequireAuth(AuthType.ADMIN_TOKEN)]
public class AdminController : PlatformController
{
    #pragma warning disable
    private readonly MessageService _messages;
    private readonly ReportService _reports;
    private readonly RoomService _rooms;
    #pragma warning restore
    
    
    #region Messages
    [HttpPost, Route("broadcast")]
    public ActionResult Broadcast()
    {
        Message[] messages = Require<Message[]>("messages");
        long expiration = Optional<long>("expiration");
        if (expiration <= 0)
            expiration = Message.StandardMessageExpiration;

        if (messages.Any(message => message == null))
            throw new PlatformException("At least one message is invalid.");

        List<Message> toInsert = new();
        foreach (Message message in messages)
        {
            message.Expiration = expiration;
            message.Administrator = Token;

            if (message.Channel == BroadcastChannel.None)
                message.Type = MessageType.Announcement;
            else if (!string.IsNullOrWhiteSpace(message.RoomId))
                message.Type = MessageType.Administrator;
            else
            {
                message.Type = MessageType.Administrator;
                string[] roomIds = _rooms
                    .GetMembership(message.AccountId, message.Channel)
                    .Select(room => room.Id)
                    .ToArray();
                
                toInsert.AddRange(roomIds.Select(id =>
                {
                    Message output = message.Copy();
                    output.RoomId = id;
                    return output;
                }));
                continue;
            }
            toInsert.Add(message);
        }
        
        _messages.Insert(toInsert.ToArray());
        if (messages.Any(message => message.Type == MessageType.Announcement)) // only allow a maximum of 10 active announcements
        {
            _messages.ExpireExcessAnnouncements();
            Log.Info(Owner.Will, "New announcement(s) posted.", data: new
            {
                Announcement = messages.Where(message => message.Type == MessageType.Announcement)
            });
        }

        return Ok(new RumbleJson
        {
            { "messages", toInsert }
        });
    }

    [HttpDelete, Route("messages/delete")]
    public ActionResult DeleteMessage()
    {
        _messages.Delete(Require<string>("id"));

        return Ok();
    }

    [HttpPut, Route("messages/update")]
    public ActionResult EditMessage()
    {
        Message message = Require<Message>("message");

        if (string.IsNullOrWhiteSpace(message?.Id) || !message.Id.CanBeMongoId())
            throw new PlatformException("Invalid message; cannot replace.");

        Message db = _messages.FromId(message.Id)
            ?? throw new PlatformException("Message does not exist.");
        
        if (message.Expiration == default)
            message.Expiration = db.Expiration;

        if (db.ContentIsEqualTo(message))
            throw new PlatformException("Messages are identical; update not honored.");

        if (!string.IsNullOrWhiteSpace(db.RoomId) && (string.IsNullOrWhiteSpace(message.RoomId) || !message.RoomId.CanBeMongoId()))
            throw new PlatformException("Invalid room ID.");

        message.Administrator = Token;
        message.UpdatedOn = Timestamp.Now;
        _messages.Update(message);
        
        Log.Local(Owner.Will, "Updated");

        return Ok(message);
    }

    [HttpGet, Route("messages")]
    public ActionResult ListMessages()
    {
        string roomId = Optional<string>("roomId");
        string accountId = Optional<string>(TokenInfo.FRIENDLY_KEY_ACCOUNT_ID);
        string messageId = Optional<string>("messageId");
        int page = Optional<int>("page");

        Message[] output = _messages.AdminListMessages(roomId, accountId, messageId, page, out long remaining);
        
        return Ok(new RumbleJson
        {
            { "messages", output },
            { "page", page },
            { "messagesPerPage", 100 },
            { "remainingMessageCount", remaining }
        });
    }

    [HttpGet, Route("messages/search")]
    public ActionResult SearchMessages()
    {
        string term = Require<string>("term");

        Message[] results = _messages.Search(term);

        return Ok(results);
    }
    #endregion Messages
    
    #region Reports

    [HttpGet, Route("reports")]
    public ActionResult ListReports()
    {
        string accountId = Optional<string>(TokenInfo.FRIENDLY_KEY_ACCOUNT_ID);
        string reportId = Optional<string>("reportId");
        int page = Optional<int>("page");

        Report[] output = _reports.ListReports(reportId, accountId, page, out long remaining);
        
        return Ok(new RumbleJson
        {
            { "reports", output },
            { "page", page },
            { "reportsPerPage", 10 },
            { "remainingReportCount", remaining }
        });
    }

    [HttpPatch, Route("reports/update")]
    public ActionResult UpdateReport()
    {
        string reportId = Require<string>("reportId");
        ReportStatus status = Require<ReportStatus>("status"); // TODO: FRIENDLY KEY

        Report output = _reports.UpdateStatus(reportId, status, Token);

        return Ok(output);
    }
    #endregion Reports
    
    #region Rooms

    [HttpGet, Route("rooms")]
    public ActionResult ListRooms()
    {
        string accountId = Optional<string>(TokenInfo.FRIENDLY_KEY_ACCOUNT_ID);
        string roomId = Optional<string>("roomId");
        int page = Optional<int>("page");

        Room[] output = _rooms.AdminListRooms(roomId, accountId, page, out long remaining);
        
        return Ok(new RumbleJson
        {
            { "reports", output },
            { "page", page },
            { "roomsPerPage", RoomService.ROOM_LIST_PAGE_SIZE },
            { "remainingRoomCount", remaining }
        });
    }
    
    [HttpPost, Route("rooms/new")]
    public ActionResult CreatePrivateRoom()
    {
        string[] accounts = Require<string[]>("accountIds");
        RumbleJson data = Optional<RumbleJson>("data");
        BroadcastChannel channel = Require<BroadcastChannel>("channel");

        if (accounts.Any(account => string.IsNullOrWhiteSpace(account) || !account.CanBeMongoId()) || !accounts.Length.Between(1, 50))
            throw new PlatformException("Invalid account ID(s) detected; cannot create private room.");

        Room output = new()
        {
            Members = accounts,
            Data = data,
            Editor = Token,
            Type = RoomType.Private,
            Channel = channel
        };
        _rooms.Insert(output);

        return Ok(output);
    }
    
    [HttpPatch, Route("rooms/update")]
    public ActionResult UpdateRoomMembership()
    {
        string roomId = Require<string>("roomId");
        string[] accounts = Require<string[]>("accountIds");
        RumbleJson data = Optional<RumbleJson>("data");

        if (accounts.Length == 0)
        {
            _rooms.Delete(roomId, out long messagesDeleted);
            return Ok(new RumbleJson
            {
                { "messagesDeleted", messagesDeleted }
            });
        }

        if (accounts.Any(account => string.IsNullOrWhiteSpace(account) || !account.CanBeMongoId()) || !accounts.Length.Between(1, 50))
            throw new PlatformException("Invalid account ID(s) detected; cannot create private room.");

        return Ok(_rooms.AdminUpdate(roomId, accounts, data, Token));
    }
    #endregion Rooms
}