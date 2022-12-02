using Microsoft.Extensions.DependencyInjection;
using RCL.Logging;
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
		.SetPerformanceThresholds(warnMS: 500, errorMS: 2_000, criticalMS: 30_000)
		.DisableFeatures(CommonFeature.LogglyThrottling | CommonFeature.ConsoleObjectPrinting);
}