using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RCL.Logging;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Filters;
using Rumble.Platform.Common.Services;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Filters;

public class UnreadFilter : PlatformFilter, IActionFilter
{
    private RoomService _rooms;
    private MessageService _messages;
    private ActivityService _activities;
    
    public void OnActionExecuting(ActionExecutingContext context) { }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        if (string.IsNullOrWhiteSpace(Token?.AccountId) || Token.IsAdmin)
            return;
        if (context.Result is not OkObjectResult ok)
            return;

        _rooms ??= PlatformService.Require<RoomService>();
        _messages ??= PlatformService.Require<MessageService>();
        _activities ??= PlatformService.Require<ActivityService>();

        _activities.MarkAsActive(Token.AccountId);
        Room[] rooms = _rooms.GetMembership(Token.AccountId); // This guarantees membership in a global room
        

        long lastRead = Body?.Optional<long?>("lastRead") ?? Timestamp.OneDayAgo;
        Message[] messages = _messages.GetAllMessages(rooms.Select(room => room.Id).ToArray(), lastRead);

        foreach (Room room in rooms)
            room.Messages = messages
                .Where(message => message.RoomId == room.Id)
                .Where(message => room.Members.Contains(message.AccountId)) // TODO: Track inactive members
                .Select(message => message.Prune())
                .ToArray();

        // TODO: Rate-limiting
        
        try
        {
            if (ok.Value is RumbleJson response)
                response["roomUpdates"] = rooms
                    .Where(room => room.Messages.Any())
                    .ToArray();
        }
        catch (Exception e)
        {
            Log.Error(Owner.Will, "Unable to append unread activity to chat response", exception: e);
        }
    }
}