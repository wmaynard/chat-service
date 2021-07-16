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

namespace Rumble.Platform.ChatService
{
	public class Startup
	{
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
			if (mongoConnection == null)
				throw new Exception("mongoConnection is null, and the service cannot start.  This will happen if the system cannot read the environment variables.");
#endif
			services.Configure<ChatDBSettings>(settings =>
			{
				settings.CollectionName = "rooms";
				settings.ConnectionString = mongoConnection;
			});
			services.Configure<BanDBSettings>(settings =>
			{
				settings.CollectionName = "restrictions";
				settings.ConnectionString = mongoConnection;
			});
			services.Configure<ReportDBSettings>(settings =>
			{
				settings.CollectionName = "restrictions";
				settings.ConnectionString = mongoConnection;
			});

			
			services.AddSingleton<ChatDBSettings>(provider => provider.GetRequiredService<IOptions<ChatDBSettings>>().Value);
			services.AddSingleton<BanDBSettings>(provider => provider.GetRequiredService<IOptions<BanDBSettings>>().Value);
			services.AddSingleton<RoomService>();
			services.AddSingleton<MessageService>();
			services.AddSingleton<BanHammerService>();
			
			services.AddControllers(config =>
			{
				config.Filters.Add(new RumbleFilter());
			}).AddNewtonsoftJson();
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
		{
			app.UseFileServer(new FileServerOptions()
			{
				FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "StaticFiles")),
				RequestPath = "/StaticFiles",
				EnableDefaultFiles = true
			});
			app.UseHttpsRedirection();
			app.UseRouting();
			app.UseAuthorization();
			app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
		}
	}
}