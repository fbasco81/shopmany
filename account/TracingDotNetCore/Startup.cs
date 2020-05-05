using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.OpenTracing;
using Jaeger;
using Jaeger.Reporters;
using Jaeger.Samplers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTracing;
using OpenTracing.Util;
using TracingDotNetCore.Config;

namespace TracingDotNetCore
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
			Directory.CreateDirectory("/var/log/datadog/dotnet/");
			services.AddHttpClient();
			var tracingOptions = Configuration.GetSection(nameof(TracingOptions));
			var tracingSettings = tracingOptions.Get<TracingOptions>();
			if (tracingSettings.TracerTarget.Equals("Jaeger", StringComparison.OrdinalIgnoreCase))
			{
				services.AddJaegerTracer();
				if (tracingSettings.EnableOpenTracingAutoTracing)
				{
					services.AddOpenTracing();

				}
			}
			else if (tracingSettings.TracerTarget.Equals("DataDog", StringComparison.OrdinalIgnoreCase))
			{
				services.AddDataDogTracer();
			}

			services.AddCors(options =>
			{

				options.AddPolicy("All",
						builder =>
						{
							builder.AllowAnyOrigin()
											.AllowAnyHeader()
											.AllowAnyMethod();
						});
			});


			services.AddControllers();
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
		{
			app.UseRouting();
			app.UseCors();
			app.UseAuthorization();

			app.UseEndpoints(endpoints =>
			{
				endpoints.MapControllers();
			});
		}


	}

	public static class TracerSelectionExtensions
	{
		public static IServiceCollection AddDataDogTracer(this IServiceCollection services)
		{
			var tracer = OpenTracingTracerFactory.WrapTracer(Datadog.Trace.Tracer.Instance);
			GlobalTracer.Register(tracer);
			services.TryAddSingleton(tracer);
			return services;
		}

		public static IServiceCollection AddJaegerTracer(this IServiceCollection services/*, TracingOptions tracingOptions, string serviceName*/)
		{
			var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
			Jaeger.Configuration config = Jaeger.Configuration.FromEnv(loggerFactory);
			var tracer = config.GetTracer();
			// Allows code that can't use DI to also access the tracer.
			GlobalTracer.Register(tracer);
			// Adds the Jaeger Tracer.
			return services.AddSingleton<ITracer>(tracer);

		}
	}
}
