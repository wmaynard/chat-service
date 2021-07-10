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
using Microsoft.Extensions.Configuration;
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
	public class MessageController : RumbleController
	{
		// TODO: DeleteMessage > Admin only, or admin & owner?  Can guild moderate messages?
		// TODO: Announcement
		// TODO: Sticky (probably should be limited to certain roles; should stickies be a different array?)
		// TODO: Mongo.updateMany
		// TODO: insert into mongo doc (as opposed to update, which could overwrite other messages)
		
		protected override string TokenAuthEndpoint { get => _config["player-service-verify"]; }
		private readonly RoomService _roomService;
		private readonly IConfiguration _config;
		
		public MessageController(RoomService service, IConfiguration config)
		{
			_config = config;
			_roomService = service;
		}

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
		[HttpPost, Route(template: "activityBroadcast")]
		public ActionResult<IEnumerable<RoomUpdate>> BroadcastActivity([FromHeader(Name = "Authorization")] string auth, [FromBody] JObject body)
		{
			TokenInfo tokenInfo = ValidateToken(auth);
			long lastRead = ExtractRequiredValue("lastRead", body).ToObject<long>();

			IEnumerable<Room> rooms = _roomService.GetRoomsForUser(tokenInfo.AccountId).Where(r => r.Type == Room.TYPE_GLOBAL);
			Message msg = Message.FromJToken(body["message"], tokenInfo.AccountId).Validate();
			
			foreach (Room r in rooms)
			{
				r.AddMessage(msg);
				_roomService.Update(r);
			}

			return Ok(RoomUpdate.GenerateResponseFrom(rooms, lastRead));
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
			TokenInfo info = ValidateToken(auth);
			long lastRead = ExtractRequiredValue("lastRead", body).ToObject<long>();

			IEnumerable<Room> rooms = _roomService.GetRoomsForUser(info.AccountId);
			
			return Ok(RoomUpdate.GenerateResponseFrom(rooms, lastRead));
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
		///			"isSticky": false,
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
			try
			{
				TokenInfo tokenInfo = ValidateToken(auth);
				long lastRead = ExtractRequiredValue("lastRead", body).ToObject<long>();
				string roomId = ExtractRequiredValue("roomId", body).ToObject<string>();

				Message msg = Message.FromJToken(body["message"], tokenInfo.AccountId).Validate();
				Room room = _roomService.Get(roomId);
				room.AddMessage(msg);
				_roomService.Update(room);
				
				return Ok(RoomUpdate.GenerateResponseFrom(room, lastRead));
			}
			catch (ArgumentException ex)	// Failed on Message.Validate.
			{
				throw new BadHttpRequestException(ex.Message);
			}
			catch (NotInRoomException ex)	// Failed on Room.AddMessage.
			{
				throw new BadHttpRequestException(ex.Message);
			}
		}
		
		/// <summary>
		/// Verifies that a message conforms to a standard format.  The text and author's account ID must not be null.
		/// </summary>
		/// <param name="msg">The message to validate.</param>
		/// <returns>The message itself is returned so this can be used in a chained assignment.</returns>
		/// <exception cref="BadHttpRequestException">Thrown whenever a message is invalid.</exception>
		private static Message Validate(Message msg)
		{
			if (msg.Text == null)
				throw new BadHttpRequestException(message:"Text cannot be null.");
			if (msg.AccountId == null)
				throw new BadHttpRequestException(message: "UserInfo cannot be null.");
			return msg;
		}
	}
}