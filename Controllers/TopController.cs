using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Controllers
{
	[ApiController, Route("chat")]
	public class TopController : RumbleController
	{
		protected override string TokenAuthEndpoint => _config["player-service-verify"];

		// public TopController(IConfiguration config) => _config = config;

		private readonly BanService _banService;
		private readonly ReportService _reportService;
		private readonly RoomService _roomService;
		private readonly SettingsService _settingsService;

		public TopController(BanService bans, ReportService reports, RoomService rooms, SettingsService settings, IConfiguration config)
			: base(config)
		{
			_banService = bans;
			_reportService = reports;
			_roomService = rooms;
			_settingsService = settings;
		}

		// Called when an account is logging in to chat.  Returns sticky messages, bans applied, and user settings.
		[HttpPost, Route(template: "launch")]
		public ActionResult Launch([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			// TODO: Join global room
			TokenInfo token = ValidateToken(auth);
			long lastRead = ExtractRequiredValue("lastRead", body).ToObject<long>();

			IEnumerable<Message> stickies = _roomService.GetStickyMessages();
			IEnumerable<Ban> bans = _banService.GetBansForUser(token.AccountId);
			ChatSettings settings = _settingsService.Get(token.AccountId);

			IEnumerable<Room> rooms = _roomService.GetRoomsForUser(token.AccountId);
			object updates = RoomUpdate.GenerateResponseFrom(rooms, lastRead);

			return Ok(
				Ban.GenerateResponseFrom(bans),
				settings.ResponseObject,
				Message.GenerateStickyResponseFrom(stickies),
				updates
			);
		}

		// Required for load balancers.  Verifies that all services are healthy.
		[HttpGet, Route(template: "health")]
		public override ActionResult HealthCheck()
		{
			Log.Write("/health");
			// TODO: Check status of services and controllers
			return Ok(
				_banService.HealthCheckResponseObject,
				_reportService.HealthCheckResponseObject,
				_roomService.HealthCheckResponseObject,
				_settingsService.HealthCheckResponseObject
			);
		}
	}
}