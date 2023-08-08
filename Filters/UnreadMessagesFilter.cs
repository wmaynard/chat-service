using Microsoft.AspNetCore.Mvc.Filters;
using Rumble.Platform.Common.Filters;

namespace Rumble.Platform.ChatService.Filters;

public class UnreadMessagesFilter : PlatformFilter, IActionFilter
{

	public void OnActionExecuting(ActionExecutingContext context)
	{
		
	}
	
	public void OnActionExecuted(ActionExecutedContext context) { }
}