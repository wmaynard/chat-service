using Microsoft.AspNetCore.Mvc;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.Common.Attributes;
using Rumble.Platform.Common.Exceptions;
using Rumble.Platform.Common.Web;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Controllers;

[Route("chat/rooms"), RequireAuth]
public class RoomsController : PlatformController
{
    #pragma warning disable
    private readonly RoomService _rooms;
    #pragma warning restore
    
    [HttpGet]
    public ActionResult ListGlobalRooms()
    {
        int page = Optional<int>("page");

        Room[] results = _rooms.ListGlobalRooms(page, out long remainingRooms);
        
        return Ok(new RumbleJson
        {
            { "rooms", results },
            { "page", page },
            { "roomsPerPage", RoomService.ROOM_LIST_PAGE_SIZE },
            { "remainingRoomCount", remainingRooms }
        });
    }

    [HttpPatch, Route("join")]
    public ActionResult JoinGlobalRoom()
    {
        string roomId = Require<string>("roomId");

        Room global = _rooms.JoinGlobal(Token.AccountId, roomId);

        return Ok(global);
    }

    [HttpDelete, Route("leave")]
    public ActionResult LeaveRoom()
    {
        string roomId = Require<string>("roomId");

        if (!_rooms.Leave(Token.AccountId, roomId))
            throw new PlatformException("Failed to leave DM room; player was not in it.");

        return Ok();
    }
}