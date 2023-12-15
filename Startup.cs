using Microsoft.Extensions.DependencyInjection;
using RCL.Logging;
using Rumble.Platform.ChatService.Filters;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.Common.Enums;
using Rumble.Platform.Common.Services;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService;

public class Startup : PlatformStartup
{
    protected override PlatformOptions ConfigureOptions(PlatformOptions options) => options
        .SetProjectOwner(Owner.Will)
        .SetTokenAudience(Audience.ChatService)
        .SetRegistrationName("Chat")
        .SetPerformanceThresholds(warnMS: 500, errorMS: 2_000, criticalMS: 30_000)
        .DisableFeatures(CommonFeature.LogglyThrottling | CommonFeature.ConsoleObjectPrinting)
        .AddFilter<UnreadFilter>()
        .SetIndividualRps(0.5)
        .OnReady(_ =>
        {
            #if  DEBUG
            if (!PlatformEnvironment.MongoConnectionString.Contains("local"))
                return;
            PlatformService.Require<MessageService>().WipeDatabase();
            PlatformService.Require<RoomService>().WipeDatabase();
            PlatformService.Require<ActivityService>().WipeDatabase();
            PlatformService.Require<PreferencesService>().WipeDatabase();
            #endif
        });
}