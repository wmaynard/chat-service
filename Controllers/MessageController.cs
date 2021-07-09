using System;
using System.Collections.Generic;
using System.Dynamic;
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
	[ApiController, Route(template: "message"), Produces(contentType: "application/json")]
	public class MessageController : RumbleController
	{
		// TODO: DeleteMessage > Admin only, or admin & owner?  Can guild moderate messages?
		// TODO: Announcement
		// TODO: Sticky (probably should be limited to certain roles; should stickies be a different array?)
		// TODO: /message/read roomIds
		private readonly RoomService _roomService;
		private readonly IConfiguration _config;
		// public MessageController(RoomService service) => _roomService = service;
		// public MessageController(IConfiguration config) => _config = config;
		public MessageController(RoomService service, IConfiguration config)
		{
			_config = config;
			_roomService = service;
		}

		/// <summary>
		/// Attempts to send a message to a chat room.  All submitted information must be sent as JSON in a request body.
		/// </summary>
		/// <param name="bearer">The Authorization header from a request.  Tokens are provided by player-service.</param>
		/// <param name="body">The JSON body.  Expected body example:
		///	{
		///		"roomId": "badfoodbadfoodbadfoodbad",
		///		"lastRead": 1625704809,
		///		"message": {
		///			"aid": "deadbeefdeadbeefdeadbeef",
		///			"isSticky": false,
		///			"text": "Hello, World!"
		///		}
		///	}
		/// </param>
		/// <returns>Unread JSON messages from the room, as specified by "lastRead".</returns>
		/// <exception cref="InvalidTokenException">Thrown when a request comes in trying to post under another account.</exception>
		/// <exception cref="BadHttpRequestException">Default exception</exception>
		[HttpPost, Route(template: "send")]
		public ActionResult<Room> Send([FromHeader(Name = "Authorization")] string bearer, [FromBody] JObject body)
		{
			try
			{
				TokenInfo ti = ValidateToken(_config["player-service-verify"], bearer);
				long lastRead = ExtractOptionalValue("lastRead", body)?.ToObject<long>() ?? 0;
				string roomId = ExtractRequiredValue("roomId", body).ToObject<string>();
				Message msg = Message.FromJToken(body["message"]).Validate();
				if (msg.AccountId != ti.AccountId)
					throw new InvalidTokenException("Token ID does not match message author ID.");

				Room room = _roomService.Get(roomId);
				IEnumerable<Message> unreads = room.AddMessage(msg, lastRead);
				_roomService.Update(room);
				return Ok(new {UnreadMessages = unreads});
			}
			catch (ArgumentException ex)
			{
				throw new BadHttpRequestException(ex.Message);
			}
			catch (NotInRoomException ex)
			{
				throw new BadHttpRequestException(ex.Message);
			}
			catch (UnauthorizedAccessException ex)
			{
				throw new BadHttpRequestException(ex.Message);
			}
			return Problem();
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
			// if (msg.UserInfo.Aid == null)
			// 	throw new BadHttpRequestException(message: "User aid cannot be null.");
			return msg;
		}
	}
}