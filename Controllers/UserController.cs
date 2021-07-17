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
		private const int COUNT_MESSAGES_FOR_REPORT = 25;
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

		[HttpGet, Route(template: "reports/list")]
		public ActionResult ListReports()
		{
			return Ok(new { Reports = _reportService.List() });
		}
		
		[HttpPost, Route(template: "report")]
		public ActionResult Report([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth);
			string messageId = ExtractRequiredValue("messageId", body).ToObject<string>();
			string roomId = ExtractRequiredValue("roomId", body).ToObject<string>();

			Room room = _roomService.Get(roomId);

			// TODO: Only use messages from around the messageId
			IEnumerable<Message> logs = room.Messages.OrderByDescending(m => m.Timestamp).Take(COUNT_MESSAGES_FOR_REPORT);
			IEnumerable<PlayerInfo> players = room.Members
				.Where(p => logs.Select(m => m.AccountId)
					.Contains(p.AccountId));
			PlayerInfo reporter = room.Members.First(p => p.AccountId == token.AccountId);
			
			Report report = new Report()
			{
				Reporter = reporter,
				Log = logs,
				Players = players
			};
			report.Log.First(m => m.Id == messageId).Reported = true;
			
			_reportService.Create(report);
			return Ok(new { Report = report });
		}
	}
}