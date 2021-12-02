using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Rumble.Platform.ChatService.Exceptions;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Controllers
{
	[ApiController, Route("chat"), RequireAuth]
	public class TopController : PlatformController
	{
		private readonly BanService _banService;
		private readonly ReportService _reportService;
		private readonly RoomService _roomService;
		private readonly SettingsService _settingsService;
		private readonly InactiveUserService _inactiveUserService;

		public TopController(BanService bans, ReportService reports, RoomService rooms, SettingsService settings, InactiveUserService inactiveUserService, IConfiguration config) : base(config)
		{
			_banService = bans;
			_reportService = reports;
			_roomService = rooms;
			_settingsService = settings;
			_inactiveUserService = inactiveUserService;
		}

		#region CLIENT
		// Called when an account is logging in to chat.  Returns sticky messages, bans applied, and user settings.
		[HttpPost, Route(template: "launch")]
		public ActionResult Launch()
		{
			long lastRead = Require<long>("lastRead");
			string language = Require<string>(Room.FRIENDLY_KEY_LANGUAGE);
			PlayerInfo player = PlayerInfo.FromJsonElement(Require(PlayerInfo.FRIENDLY_KEY_SELF), Token);

			IEnumerable<Message> stickies = _roomService.GetStickyMessages();
			Ban[] bans = _banService.GetBansForUser(Token.AccountId).ToArray();

			foreach (Ban b in bans)
				b.PurgeSnapshot();
			
			ChatSettings settings = _settingsService.Get(Token.AccountId);

			Room global = _roomService.JoinGlobal(player, language);
			IEnumerable<Room> rooms = _roomService.GetRoomsForUser(Token.AccountId);
			object updates = RoomUpdate.GenerateResponseFrom(rooms, lastRead);
			_inactiveUserService.Track(player);

			return Ok(
				new { Bans = bans },
				settings.ResponseObject,
				Message.GenerateStickyResponseFrom(stickies),
				global.ResponseObject,
				updates
			);
		}
		#endregion CLIENT
		
		#region LOAD BALANCER
		[HttpGet, Route(template: "health"), NoAuth]
		public override ActionResult HealthCheck()
		{
			return Ok(
				_banService.HealthCheckResponseObject,
				_reportService.HealthCheckResponseObject,
				_roomService.HealthCheckResponseObject,
				_settingsService.HealthCheckResponseObject,
				_inactiveUserService.HealthCheckResponseObject
			);
		}
		#endregion LOAD BALANCER
	}
}