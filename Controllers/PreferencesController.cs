using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Mvc;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.Common.Attributes;
using Rumble.Platform.Common.Exceptions;
using Rumble.Platform.Common.Extensions;
using Rumble.Platform.Common.Models;
using Rumble.Platform.Common.Web;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Controllers;

[Route("chat/preferences"), RequireAuth]
public class PreferencesController : PlatformController
{
    #pragma warning disable
    private readonly PreferencesService _preferences;
    #pragma warning restore
    
    [HttpGet]
    public ActionResult GetPreferences() => Ok(_preferences.FromAccountId(Token.AccountId));

    [HttpPut, Route("update"), RequestSizeLimit(3_145_728)] // 3MB
    public ActionResult SetPreferences()
    {
        RumbleJson settings = Require<RumbleJson>("settings");

        _preferences.Update(Token.AccountId, settings);

        return Ok();
    }
}