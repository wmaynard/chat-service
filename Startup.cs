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
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.ChatService.Settings;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;
using Rumble.Platform.CSharp.Common.Interop;

namespace Rumble.Platform.ChatService
{
	public class Startup
	{
		public const string CORS_SETTINGS_NAME = "_CORS_SETTINGS";
		public Startup(IConfiguration configuration)
		{
			Configuration = configuration;
			Log.Info(Owner.System, "Service started.", localIfNotDeployed: true);
		}

		public IConfiguration Configuration { get; }

		// This method gets called by the runtime. Use this method to add services to the container.
		public void ConfigureServices(IServiceCollection services)
		{
			string mongoConnection = RumbleEnvironment.Variable("MONGODB_URI");
			string database = RumbleEnvironment.Variable("MONGODB_NAME");
			Log.Local(Owner.System, $"mongoConnection: '{mongoConnection}'");
			if (mongoConnection == null)
			{
				Exception e = new Exception("mongoConnection is null, and the service cannot start.  This will happen if the system cannot read the environment variables.");
				Log.Critical(Owner.Will, e.Message, exception: e);
				throw e;
			}

			Log.Verbose(Owner.Will, "Initializing ChatDBSettings");
			services.Configure<ChatDBSettings>(settings =>
			{
				settings.CollectionName = "chat_rooms_wm";
				settings.ConnectionString = mongoConnection;
				settings.DatabaseName = database;
			});
			Log.Verbose(Owner.Will, "Initializing ReportDBSettings");
			services.Configure<ReportDBSettings>(settings =>
			{
				settings.CollectionName = "chat_reports";
				settings.ConnectionString = mongoConnection;
				settings.DatabaseName = database;
			});
			Log.Verbose(Owner.Will, "Initializing SettingsDBSettings");
			services.Configure<SettingsDBSettings>(settings =>
			{
				settings.CollectionName = "chat_settings";
				settings.ConnectionString = mongoConnection;
				settings.DatabaseName = database;
			});
			Log.Verbose(Owner.Will, "Initializing BanDBSettings");
			services.Configure<BanDBSettings>(settings =>
			{
				settings.CollectionName = "chat_bans";
				settings.ConnectionString = mongoConnection;
				settings.DatabaseName = database;
			});

			Log.Verbose(Owner.Will, "Creating Settings Providers");
			services.AddSingleton<ChatDBSettings>(provider => provider.GetRequiredService<IOptions<ChatDBSettings>>().Value);
			services.AddSingleton<ReportDBSettings>(provider => provider.GetRequiredService<IOptions<ReportDBSettings>>().Value);
			services.AddSingleton<SettingsDBSettings>(provider => provider.GetRequiredService<IOptions<SettingsDBSettings>>().Value);
			services.AddSingleton<BanDBSettings>(provider => provider.GetRequiredService<IOptions<BanDBSettings>>().Value);

			Log.Verbose(Owner.Will, "Creating Service Singletons");
			services.AddSingleton<RoomService>();
			services.AddSingleton<ReportService>();
			services.AddSingleton<SettingsService>();
			services.AddSingleton<BanService>();
			
			Log.Verbose(Owner.Will, "Adding Controllers");
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