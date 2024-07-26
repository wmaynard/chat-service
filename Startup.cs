using Rumble.Platform.ChatService.Filters;
using Rumble.Platform.Common.Enums;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService;

public class Startup : PlatformStartup
{
    protected override PlatformOptions ConfigureOptions(PlatformOptions options) => options
        .SetProjectOwner(Owner.Will)
        .SetTokenAudience(Audience.ChatService)
        .SetRegistrationName("Chat")
        .SetPerformanceThresholds(warnMS: 1_000, errorMS: 10_000, criticalMS: 30_000)
        .DisableFeatures(CommonFeature.LogglyThrottling | CommonFeature.ConsoleObjectPrinting)
        .AddFilter<UnreadFilter>()
        .SetIndividualRps(5)
        .WipeLocalDatabasesOnStartup()
        .OnReady(_ =>
        {
            #if  DEBUG
            if (!PlatformEnvironment.MongoConnectionString.Contains("local"))
                return;
            // PlatformService.Require<MessageService>().WipeDatabase();
            // PlatformService.Require<RoomService>().WipeDatabase();
            // PlatformService.Require<ActivityService>().WipeDatabase();
            // PlatformService.Require<PreferencesService>().WipeDatabase();
            #endif
        });
}