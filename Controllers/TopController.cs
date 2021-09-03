using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;
using Rumble.Platform.CSharp.Common.Interop;

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
			TokenInfo token = ValidateToken(auth);
			Log.Info(Owner.Will, "Testing new LogglyClient", token, new {Foo = "bar", Jouney = new int[] {1, 2, 3, 4, 5}, Nested = new {World = "hello"}});
			long lastRead = ExtractRequiredValue("lastRead", body).ToObject<long>();
			string language = ExtractRequiredValue(RoomController.POST_KEY_LANGUAGE, body).ToObject<string>();
			PlayerInfo player = PlayerInfo.FromJToken(ExtractRequiredValue(RoomController.POST_KEY_PLAYER_INFO, body), token);

			IEnumerable<Message> stickies = _roomService.GetStickyMessages();
			IEnumerable<Ban> bans = _banService.GetBansForUser(token.AccountId);
			ChatSettings settings = _settingsService.Get(token.AccountId);

			Room global = _roomService.JoinGlobal(player, language);
			IEnumerable<Room> rooms = _roomService.GetRoomsForUser(token.AccountId);
			object updates = RoomUpdate.GenerateResponseFrom(rooms, lastRead);

			return Ok(
				CollectionResponseObject(bans), // TODO: Clear reports from response
				settings.ResponseObject,
				Message.GenerateStickyResponseFrom(stickies),
				global.ResponseObject,
				updates
			);
		}

		// Required for load balancers.  Verifies that all services are healthy.
		[HttpGet, Route(template: "health")]
		public override ActionResult HealthCheck()
		{
			return Ok(
				_banService.HealthCheckResponseObject,
				_reportService.HealthCheckResponseObject,
				_roomService.HealthCheckResponseObject,
				_settingsService.HealthCheckResponseObject
			);
		}
	}
}