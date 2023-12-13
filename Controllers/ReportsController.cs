using Microsoft.AspNetCore.Mvc;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.Common.Attributes;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Controllers;

[Route("chat/report"), RequireAuth]
public class ReportsController : PlatformController
{
    #pragma warning disable
    private readonly ReportService _reports;
    #pragma warning restore
    
    [HttpPost]
    public ActionResult ReportMessage()
    {
        string messageId = Require<string>("messageId");

        Report report = _reports.Submit(Token.AccountId, messageId);
        
        // TODO: Notify CS?  Log?  Keep GDPR in mind!

        return Ok(report);
    }
}