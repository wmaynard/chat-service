using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.Common.Web;
using System.Linq;
using Rumble.Platform.ChatService.Utilities;

namespace Rumble.Platform.ChatService.Controllers
{
	[ApiController, Route(template: "user"), Produces(contentType: "application/json")]
	public class UserController : ChatControllerBase
	{
		
		private ReportService _reportService;

		public UserController(ReportService reportService, RoomService roomService, IConfiguration config) : base(roomService, config)
		{
			_reportService = reportService;
		}

		[HttpPost, Route(template: "ban")]
		public ActionResult Ban([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			// TODO: Require admin auth
			return Problem();
		}

		[HttpPost, Route(template: "mute")]
		public ActionResult Mute([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth);
			return Problem();
		}

		[HttpPost, Route(template: "report/list")]
		public ActionResult ListReports()
		{
			return Ok(new { Reports = _reportService.List() });
		}
	}
}