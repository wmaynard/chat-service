using Microsoft.AspNetCore.Mvc;
using Rumble.Platform.Common.Attributes;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Controllers;

[Microsoft.AspNetCore.Components.Route("chat"), RequireAuth]
public class TopController : PlatformController
{
    public ActionResult GetMessages()
    {
        return Ok();
    }

    [HttpPost, Route("message")]
    public ActionResult SendMessage()
    {
        return Ok();
    }

    [HttpGet, Route("rooms")]
    public ActionResult GetRooms()
    {
        return Ok();
    }
}