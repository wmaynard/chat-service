using Microsoft.Extensions.DependencyInjection;
using RCL.Logging;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService;

public class Startup : PlatformStartup
{
	protected override PlatformOptions Configure(PlatformOptions options) => options
		.SetProjectOwner(Owner.Will)
		.SetPerformanceThresholds(warnMS: 500, errorMS: 2_000, criticalMS: 30_000);
}