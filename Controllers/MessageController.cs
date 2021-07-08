using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
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
		public MessageController(RoomService service) => _roomService = service;

		[HttpPost, Route(template: "send")]
		public ActionResult<Room> Send([FromHeader(Name = "token")] string bearer, [FromBody] JObject body)
		{
			try
			{
				long lastRead = ExtractOptionalValue("lastRead", body)?.ToObject<long>() ?? 0;
				string roomId = ExtractRequiredValue("roomId", body).ToObject<string>();
				Message msg = Message.FromJToken(body["message"]).Validate();

				// TODO: Make sure bearer aid == msg.userInfo.aid

				Room room = _roomService.Get(roomId);
				IEnumerable<Message> unreads = room.AddMessage(msg, lastRead);
				_roomService.Update(room);
				return Ok(new { UnreadMessages = unreads });
			}
			catch (ArgumentException ex)
			{
				throw new BadHttpRequestException(ex.Message);
			}
			catch (NotInRoomException ex)
			{
				throw new BadHttpRequestException(ex.Message);
			}
			return Ok();
		}
		
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