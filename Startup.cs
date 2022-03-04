using Microsoft.Extensions.DependencyInjection;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;
using Rumble.Platform.Common.Interop;

namespace Rumble.Platform.ChatService;

public class Startup : PlatformStartup
{
	public void ConfigureServices(IServiceCollection services)
	{
#if DEBUG
		base.ConfigureServices(services, defaultOwner: Owner.Will, warnMS: 5_000, errorMS: 20_000, criticalMS: 300_000);
#else
		base.ConfigureServices(services, defaultOwner: Owner.Will, warnMS: 500, errorMS: 2_000, criticalMS: 30_000);
#endif
	}
}