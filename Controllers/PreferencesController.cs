using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Mvc;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Exceptions;
using Rumble.Platform.Common.Extensions;
using Rumble.Platform.Common.Models;
using Rumble.Platform.Common.Web;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Controllers;

[Route("chat/preferences")]
public class PreferencesController : PlatformController
{
    #pragma warning disable
    private readonly PreferencesService _preferences;
    #pragma warning restore
    
    [HttpGet]
    public ActionResult GetPreferences() => Ok(_preferences.FromAccountId(Token.AccountId));

    [HttpPut, Route("save"), RequestSizeLimit(3_145_728)] // 3MB
    public ActionResult SetPreferences()
    {
        RumbleJson settings = Require<RumbleJson>("settings");

        _preferences.Update(Token.AccountId, settings);

        return Ok();
    }
}

[Route("chat/rooms")]
public class RoomsController : PlatformController
{
    #pragma warning disable
    private readonly RoomService _rooms;
    #pragma warning restore
    
    [HttpGet]
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


    
    // TODO: DMs, DM rooms get deleted after X inactive time
}

[Route("chat/report")]
public class ReportsController : PlatformController
{
    #pragma warning disable
    private readonly ReportService _reports;
    #pragma warning restore
    
    [HttpPost]
    public ActionResult ReportMessage()
    {
        string messageId = Require<string>("messageId");

        Report report = _reports.Submit(Token.AccountId, messageId);
        
        // TODO: Notify CS?  Log?  Keep GDPR in mind!

        return Ok(report);
    }
}