using Microsoft.AspNetCore.Mvc.Filters;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Filters;
using Rumble.Platform.Common.Services;
using Rumble.Platform.Common.Utilities;

namespace Rumble.Platform.ChatService.Filters;

public class UnreadFilter : PlatformFilter, IActionFilter
{
    private RoomService _rooms;
    private MessageService _messages;
    private ActivityService _activities;
    
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (string.IsNullOrWhiteSpace(Token?.AccountId) || Token.IsAdmin)
            return;

        _rooms ??= PlatformService.Require<RoomService>();
        _messages ??= PlatformService.Require<MessageService>();
        _activities ??= PlatformService.Require<ActivityService>();

        _activities.MarkAsActive(Token.AccountId);
        Room[] rooms = _rooms.GetMembership(Token.AccountId);

        long lastRead = Body.Optional<long?>("lastRead") ?? Timestamp.OneDayAgo;
        
        throw new System.NotImplementedException();
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        throw new System.NotImplementedException();
    }
}