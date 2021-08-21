using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.ChatService.Settings;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService
{
	public class Startup
	{
		public const string CORS_SETTINGS_NAME = "_CORS_SETTINGS";
		public Startup(IConfiguration configuration)
		{
			Configuration = configuration;
		}

		public IConfiguration Configuration { get; }

		// This method gets called by the runtime. Use this method to add services to the container.
		public void ConfigureServices(IServiceCollection services)
		{
#if DEBUG
			string mongoConnection = "mongodb://localhost:27017";
			string database = "ChatDB";
#endif
#if RELEASE
			// Important note: at the time of this comment, it seems that the only possible way to get Rider to use
			// environment variables is to add those into launchSettings.json as opposed to regular env vars.  These
			// vars show up under the configurations, but are only read-only.
			// Rather than risk accidentally committing sensitive information into the repo, we'll use conditional
			// compilation as a workaround.  If you need to test the configuration against your local environment variables,
			// run the service from Terminal with the command "dotnet {path}/chat-service.dll".  This will circumnavigate
			// Rider's restrictions as well.
			string mongoConnection = Environment.GetEnvironmentVariable("MONGODB_URI");
			string database = Environment.GetEnvironmentVariable("MONGODB_NAME");
			Log.Write($"mongoConnection: '{mongoConnection}'");
			if (mongoConnection == null)
				throw new Exception("mongoConnection is null, and the service cannot start.  This will happen if the system cannot read the environment variables.");
#endif
			Log.Write("Initializing ChatDBSettings");
			services.Configure<ChatDBSettings>(settings =>
			{
				settings.CollectionName = "chat_rooms";
				settings.ConnectionString = mongoConnection;
				settings.DatabaseName = database;
			});
			Log.Write("Initializing ReportDBSettings");
			services.Configure<ReportDBSettings>(settings =>
			{
				settings.CollectionName = "chat_reports";
				settings.ConnectionString = mongoConnection;
				settings.DatabaseName = database;
			});
			Log.Write("Initializing SettingsDBSettings");
			services.Configure<SettingsDBSettings>(settings =>
			{
				settings.CollectionName = "chat_settings";
				settings.ConnectionString = mongoConnection;
				settings.DatabaseName = database;
			});
			Log.Write("Initializing BanDBSettings");
			services.Configure<BanDBSettings>(settings =>
			{
				settings.CollectionName = "chat_bans";
				settings.ConnectionString = mongoConnection;
				settings.DatabaseName = database;
			});

			Log.Write("Creating Settings Providers");
			services.AddSingleton<ChatDBSettings>(provider => provider.GetRequiredService<IOptions<ChatDBSettings>>().Value);
			services.AddSingleton<ReportDBSettings>(provider => provider.GetRequiredService<IOptions<ReportDBSettings>>().Value);
			services.AddSingleton<SettingsDBSettings>(provider => provider.GetRequiredService<IOptions<SettingsDBSettings>>().Value);
			services.AddSingleton<BanDBSettings>(provider => provider.GetRequiredService<IOptions<BanDBSettings>>().Value);

			Log.Write("Creating Service Singletons");
			services.AddSingleton<RoomService>();
			services.AddSingleton<ReportService>();
			services.AddSingleton<SettingsService>();
			services.AddSingleton<BanService>();
			
			Log.Write("Adding Controllers");
			services.AddControllers(config =>
			{
				config.Filters.Add(new RumbleFilter());
			}).AddJsonOptions(options =>
			{
				options.JsonSerializerOptions.IgnoreNullValues = true;
			}).AddNewtonsoftJson();

			services.AddCors(options =>
			{
				options.AddPolicy(name: CORS_SETTINGS_NAME, builder =>
					{
						builder
							.AllowAnyMethod()
							.AllowAnyHeader()
							.AllowAnyOrigin();
					}
				);
			});
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
		{
			app.UseRouting();
			app.UseCors(CORS_SETTINGS_NAME);
			app.UseAuthorization();
			app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
		}
	}
}