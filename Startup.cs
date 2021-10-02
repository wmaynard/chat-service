using Microsoft.Extensions.DependencyInjection;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.ChatService.Settings;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService
{
	public class Startup : PlatformStartup
	{
		// This method gets called by the runtime. Use this method to add services to the container.
		public void ConfigureServices(IServiceCollection services)
		{
			base.ConfigureServices(services, warnMS: 1_000, errorMS: 5_000);
			
			Log.Verbose(Owner.Will, "Initializing Database Settings");
			SetCollectionName<BanDBSettings>("bans");
			SetCollectionName<ReportDBSettings>("reports");
			SetCollectionName<RoomDBSettings>("rooms");
			SetCollectionName<SettingsDBSettings>("settings");

			Log.Verbose(Owner.Will, "Creating Service Singletons");
			services.AddSingleton<BanService>();
			services.AddSingleton<ReportService>();
			services.AddSingleton<RoomService>();
			services.AddSingleton<SettingsService>();
		}
	}
}