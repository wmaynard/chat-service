using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Controllers
{
	[ApiController, Route(template: "chat/messages"), Produces(contentType: "application/json")]
	public class MessageController : ChatControllerBase
	{
		// TODO: Mongo.updateMany
		// TODO: insert into mongo doc (as opposed to update, which could overwrite other messages)
		private ReportService _reportService;
		
		public MessageController(ReportService reports, RoomService rooms, IConfiguration config) : base(rooms, config)
		{
			_reportService = reports;
		}
		
		/// <summary>
		/// Attempts to send a message to the user's global chat room.  All submitted information must be sent as JSON in a request body.
		/// </summary>
		/// <param name="auth">The Authorization header from a request.  Tokens are provided by player-service.</param>
		/// <param name="body">The JSON body.  'lastRead', and 'message' are required fields.
		/// Expected body example:
		///	{
		///		"lastRead": 1625704809,
		///		"message": {
		///			"text": "Hello, World!"
		///		}
		///	}
		/// </param>
		/// <returns>Unread JSON messages from the room, as specified by "lastRead".</returns>
		/// <exception cref="InvalidTokenException">Thrown when a request comes in trying to post under another account.</exception>
		/// <exception cref="BadHttpRequestException">Default exception</exception>
		[HttpPost, Route(template: "broadcast")]
		public ActionResult Broadcast([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth);
			Message msg = Message.FromJToken(ExtractRequiredValue("message", body), token.AccountId).Validate();

			object updates = GetAllUpdates(token, body,(IEnumerable<Room> rooms) =>
			{
				foreach (Room r in rooms.Where(r => r.Type == Room.TYPE_GLOBAL))
				{
					r.AddMessage(msg);
					_roomService.Update(r); // TODO: Push the message rather than update the room.
				}
			});

			return Ok(updates);
		}

		[HttpPost, Route(template: "report")]
		public ActionResult Report([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth);
			string messageId = ExtractRequiredValue("messageId", body).ToObject<string>();
			string roomId = ExtractRequiredValue("roomId", body).ToObject<string>();

			Room room = _roomService.Get(roomId);
			
			IEnumerable<Message> logs = room.Snapshot(messageId, Models.Report.COUNT_MESSAGES_BEFORE_REPORTED, Models.Report.COUNT_MESSAGES_AFTER_REPORTED);
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
			return Ok(report.ResponseObject);	// TODO: udpates
		}
		/// <summary>
		/// Attempts to send a message to a chat room.  All submitted information must be sent as JSON in a request body.
		/// </summary>
		/// <param name="auth">The Authorization header from a request.  Tokens are provided by player-service.</param>
		/// <param name="body">The JSON body.  'roomId', 'lastRead', and 'message' are all required fields.
		/// Expected body example:
		///	{
		///		"lastRead": 1625704809,
		///		"message": {
		///			"isSticky": false, TODO: Remove IsSticky, convert to SitckyMessage (Text, Languages, Expiration)
		///			"text": "Hello, World!"
		///		},
		///		"roomId": "badfoodbadfoodbadfoodbad",
		///	}
		/// </param>
		/// <returns>Unread JSON messages from the room, as specified by "lastRead".</returns>
		/// <exception cref="InvalidTokenException">Thrown when a request comes in trying to post under another account.</exception>
		/// <exception cref="BadHttpRequestException">Default exception</exception>
		[HttpPost, Route(template: "send")]
		public ActionResult Send([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth);
			string roomId = ExtractRequiredValue("roomId", body).ToObject<string>();
			Message msg = Message.FromJToken(body["message"], token.AccountId).Validate();

			object updates = GetAllUpdates(token, body, delegate(IEnumerable<Room> rooms)
			{
				Room room = rooms.First(r => r.Id == roomId);
				room.AddMessage(msg);
				_roomService.Update(room);
			});

			return Ok(updates);
		}
		/// <summary>
		/// Retrieves all unread messages for a user based on a timestamp.  This timestamp should be the most recent
		/// timestamp from any message in the user's chat rooms.
		/// </summary>
		/// <param name="auth">The Authorization header from a request.  Tokens are provided by player-service.</param>
		/// <param name="body">The JSON body.  'lastRead' is a required field.
		/// Expected body example:
		///	{
		///		"lastRead": 1625704809
		///	}
		/// </param>
		/// <returns></returns>
		[HttpPost, Route(template: "unread")]
		public ActionResult Unread([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth);

			return Ok(GetAllUpdates(token, body));
		}

		[HttpPost, Route(template: "sticky")]
		public ActionResult StickyList([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth);
			bool all = ExtractOptionalValue("all", body)?.ToObject<bool>() ?? false;

			return Ok(new { Stickies = _roomService.GetStickyMessages(all) }); // TODO: Add stickies to GetAllUpdates
		}
	}
}