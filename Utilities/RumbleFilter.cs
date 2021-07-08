using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Utilities
{
	/// <summary>
	/// This class is designed to catch Exceptions that the API throws.  Our API should not be dumping stack traces
	/// or other potentially security-compromising data to a client outside of our debug environment.  In order to
	/// prevent that, we need to have a catch-all implementation for Exceptions in OnActionExecuted.
	/// </summary>
	public class RumbleFilter : IActionFilter
	{
		public void OnActionExecuting(ActionExecutingContext context)
		{
		}

		/// <summary>
		/// This triggers after an action executes, but before any uncaught Exceptions are dealt with.  Here we can
		/// make sure we prevent stack traces from going out and return a BadRequestResult instead (for example).
		/// </summary>
		/// <param name="context"></param>
		public void OnActionExecuted(ActionExecutedContext context)
		{
			// {
			// 	"success": false,
			// 	"errorCode": "authentication",
			// 	"debugText": "authentication"
			// }
			if (context.Exception == null)
				return;

			context.ExceptionHandled = true;
			string message = context.Exception.Message;
			Exception ex = context.Exception?.InnerException ?? context.Exception;
			
			if (ex is JsonSerializationException)	// Thrown when our model can't read a POST body
				context.Result = new BadRequestObjectResult(new ErrorResponse(
					errorCode: ErrorCodes.BAD_JSON,
					debugText: message
				));
			else if (ex is AuthException)			// Thrown when token is invalid
				context.Result = new BadRequestObjectResult(new ErrorResponse(
					errorCode: ErrorCodes.AUTHENTICATION,
					debugText: message
				));
			else if (ex is RoomNotFoundException)
				context.Result = new BadRequestObjectResult(new ErrorResponse(
					errorCode: ChatErrorCodes.ROOM_NOT_FOUND,
					debugText: message
				));
			else if (ex is BadHttpRequestException)
				context.Result = new BadRequestObjectResult(new ErrorResponse(
					errorCode: ErrorCodes.BAD_JSON,
					debugText: message
				));
			else									// Everything else
			{
				context.Result = new BadRequestObjectResult(new ErrorResponse(
					errorCode: ErrorCodes.UNKNOWN,
					debugText: "Unhandled exception"
				));
			}
		}
	}
}