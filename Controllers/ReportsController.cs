using Microsoft.AspNetCore.Mvc;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.Common.Attributes;
using Rumble.Platform.Common.Interop;
using Rumble.Platform.Common.Utilities;
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

        string url = PlatformEnvironment.Url($"/dmz/chat/report?reportId={report.Id}");
        SlackDiagnostics
            .Log(
                title: "New Chat Report",
                message: $"To view the report, visit {url}."
            )
            .Send()
            .Wait();

        return Ok(report);
    }
}