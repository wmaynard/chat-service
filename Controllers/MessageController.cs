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
using Platform.CSharp.Common.Web;
using RestSharp;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Controllers
{
	[ApiController, Route(template: "messages"), Produces(contentType: "application/json")]
	public class MessageController : ChatControllerBase
	{
		// TODO: DeleteMessage > Admin only, or admin & owner?  Can guild moderate messages?
		// TODO: Announcement
		// TODO: Sticky (probably should be limited to certain roles; should stickies be a different array?)
		// TODO: Mongo.updateMany
		// TODO: insert into mongo doc (as opposed to update, which could overwrite other messages)

		public MessageController(RoomService service, IConfiguration config) : base(service, config) { }

		[HttpPost, Route(template: "schedule")]
		public ActionResult Schedule([FromHeader(Name = "Authorization")] string auth, [FromBody] JObject body)
		{
			// Should the scope be local to one room?  One language?  Globally?  All valid options?
			// Should probably have a model for ScheduledMessage, stored separately from rooms
			// Require elevated auth
			throw new NotImplementedException();
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
		public ActionResult BroadcastActivity([FromHeader(Name = "Authorization")] string auth, [FromBody] JObject body)
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
		public ActionResult<IEnumerable<RoomUpdate>> GetUnread([FromHeader(Name = "Authorization")] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth);

			return Ok(GetAllUpdates(token, body));
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
		public ActionResult<IEnumerable<RoomUpdate>> Send([FromHeader(Name = "Authorization")] string auth, [FromBody] JObject body)
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
	}
}