using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Controllers
{
	[ApiController, Route(template: "user"), Produces(contentType: "application/json")]
	public class UserController : ChatControllerBase
	{
		private const int COUNT_MESSAGES_FOR_REPORT = 25;
		private ReportService _reportService;

		public UserController(RoomService roomService, IConfiguration config) : base(roomService, config) { }

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
		
		[HttpPost, Route(template: "report")]
		public ActionResult Report([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth);
			// Generate a snapshot of the conversation when the report was generated.
			return Problem();
		}
	}
}