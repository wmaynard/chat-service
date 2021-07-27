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

		private readonly BanMongoService _banMongoService;
		private readonly ReportService _reportService;
		private readonly RoomService _roomService;
		private readonly SettingsService _settingsService;

		public TopController(BanMongoService bansMongo, ReportService reports, RoomService rooms, SettingsService settings, IConfiguration config)
			: base(config)
		{
			_banMongoService = bansMongo;
			_reportService = reports;
			_roomService = rooms;
			_settingsService = settings;
		}

		// Called when an account is logging in to chat.  Returns sticky messages, bans applied, and user settings.
		[HttpPost, Route(template: "launch")]
		public ActionResult Launch([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			// TODO: Add RoomUpdates?  Join global room?
			
			TokenInfo token = ValidateToken(auth);

			IEnumerable<Message> stickies = _roomService.GetStickyMessages();
			IEnumerable<Ban> bans = _banMongoService.GetBansForUser(token.AccountId);
			ChatSettings settings = _settingsService.Get(token.AccountId);

			return Ok(Merge(
				Ban.GenerateResponseFrom(bans),
				settings.ResponseObject,
				Message.GenerateStickyResponseFrom(stickies)
			));
		}

		// Required for load balancers.  Verifies that all services are healthy.
		[HttpGet, Route(template: "health")]
		public ActionResult HealthCheck()
		{
			Log.Write("/health");
			string s = Environment.GetEnvironmentVariable("RUMBLE_GAME") ?? "$RUMBLE_GAME not found.";
			Log.Write("$RUMBLE_GAME: " + s);
			// TODO: Check status of services and controllers
			return Ok(new {Healthy = true});
		}
	}
}