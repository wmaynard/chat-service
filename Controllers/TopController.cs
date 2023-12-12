using Microsoft.AspNetCore.Mvc;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Attributes;
using Rumble.Platform.Common.Exceptions;
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
    public ActionResult GetMessages()
    {
        return Ok();
    }

    [HttpPost, Route("message")]
    public ActionResult SendMessage()
    {
        Message message = Require<Message>("message");
        
        message.AccountId = Token.AccountId;
        message.Expiration = Timestamp.TwoWeeksFromNow;
        
        _messages.Insert(message);
        
        return Ok(message);
    }

    [HttpGet, Route("rooms")]
    public ActionResult ListGlobalRooms()
    {
        int page = Optional<int>("page");

        Room[] results = _rooms.ListGlobalRoomsWithCapacity(page, out long remainingRooms);
        
        return Ok(new RumbleJson
        {
            { "rooms", results },
            { "page", page },
            { "roomsPerPage", RoomService.ROOM_LIST_PAGE_SIZE },
            { "remainingRoomCount", remainingRooms }
        });
    }
}