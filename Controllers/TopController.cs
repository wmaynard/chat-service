using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Mvc;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.Common.Attributes;
using Rumble.Platform.Common.Exceptions;
using Rumble.Platform.Common.Extensions;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Controllers;

[Route("chat"), RequireAuth]
public class TopController : PlatformController
{
    #pragma warning disable
    private readonly MessageService _messages;
    private readonly RoomService _rooms;
    #pragma warning restore

    [HttpGet]
    public ActionResult GetMessages() => Ok();

    [HttpPost, Route("message")]
    public ActionResult SendMessage()
    {
        Message message = RequireMessage().EnforceRoomIdIsValid();
        message.Expiration = Message.StandardMessageExpiration;

        if (string.IsNullOrWhiteSpace(message?.RoomId) || !message.RoomId.CanBeMongoId())
            throw new PlatformException("Message must have a valid roomId");
        
        if (PlatformEnvironment.IsLocal || PlatformEnvironment.IsDev)
        {
            if (!_rooms.GetMembership(Token.AccountId).Select(room => room.Id).Contains(message.RoomId))
                throw new PlatformException("Message does not belong to any room the player is participating in and cannot be sent.  This error is exclusive to local / dev environments.");
        }
        
        _messages.Insert(message);
        
        return Ok(message);
    }
    
    [HttpPost, Route("dm")]
    public ActionResult DirectMessage()
    {
        List<string> members = Require<List<string>>("players");
        Message message = RequireMessage();
        message.Expiration = Message.DirectMessageExpiration;
        
        members.Add(Token.AccountId);
        if (members.Any(member => string.IsNullOrWhiteSpace(member) || !member.CanBeMongoId()))
            throw new PlatformException("Invalid player ID.");
        if (!members.Count.Between(2, 20))
            throw new PlatformException("DMs must be capacity 2-20");

        message.RoomId ??= _rooms.GetDmRoom(members.ToArray());
        
        _messages.Insert(message);

        return Ok(message);
    }

    [HttpGet, Route("search")]
    public ActionResult Search()
    {
        string terms = Require<string>("terms");

        return Ok(_messages.Search(terms));
    }

    private Message RequireMessage()
    {
        Message output = Require<Message>("message");

        output.AccountId = Token.AccountId;

        return output;
    }
}