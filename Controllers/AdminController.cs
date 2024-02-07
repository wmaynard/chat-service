using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
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

        foreach (Message message in messages)
        {
            message.Type = string.IsNullOrWhiteSpace(message.RoomId)
                ? MessageType.Announcement
                : MessageType.Administrator;
            message.Expiration = expiration;
            message.Administrator = Token;
        }
        
        _messages.Insert(messages);
        if (messages.Any(message => message.Type == MessageType.Announcement)) // only allow a maximum of 10 active announcements
            _messages.ExpireExcessAnnouncements();
        
        return Ok(messages);
    }

    [HttpPut, Route("messages/update")]
    public ActionResult EditMessage()
    {
        Message message = Require<Message>("message");

        if (string.IsNullOrWhiteSpace(message?.Id) || !message.Id.CanBeMongoId())
            throw new PlatformException("Invalid message; cannot replace.");

        message.Administrator = Token;
        
        _messages.Update(message);

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
            { "roomsPerPage", 10 },
            { "remainingRoomCount", remaining }
        });
    }
    
    [HttpPost, Route("rooms/new")]
    public ActionResult CreatePrivateRoom()
    {
        string[] accounts = Require<string[]>("accountIds");
        RumbleJson data = Optional<RumbleJson>("data");

        if (accounts.Any(account => string.IsNullOrWhiteSpace(account) || !account.CanBeMongoId()) || !accounts.Length.Between(1, 50))
            throw new PlatformException("Invalid account ID(s) detected; cannot create private room.");

        Room output = new()
        {
            Members = accounts,
            Data = data,
            Editor = Token,
            Type = RoomType.Private
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