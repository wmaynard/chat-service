using Microsoft.Extensions.DependencyInjection;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService
{
	public class Startup : PlatformStartup
	{
		public void ConfigureServices(IServiceCollection services)
		{
			base.ConfigureServices(services, warnMS: 500, errorMS: 2_000, criticalMS: 30_000);
		}
	}
}