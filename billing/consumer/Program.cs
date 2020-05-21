using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTracing.Util;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Threading.Tasks;

namespace consumer
{
	class Program
	{
		public static async Task Main(string[] args)
		{
			var hostBuilder = new HostBuilder()
					// Add configuration, logging, ...
					.ConfigureServices((hostContext, services) =>
					{
						services.AddHttpClient();
						services.AddOpenTracing();
						services.AddSingleton(serviceProvider =>
						{
							//Environment.SetEnvironmentVariable("JAEGER_SERVICE_NAME", "wharehouse.producer");
							//Environment.SetEnvironmentVariable("JAEGER_AGENT_HOST", "localhost");
							//Environment.SetEnvironmentVariable("JAEGER_AGENT_PORT", "6831");
							//Environment.SetEnvironmentVariable("JAEGER_SAMPLER_TYPE", "const");

							var loggerFactory = new LoggerFactory();

							
							var config = Jaeger.Configuration.FromEnv(loggerFactory);
							var tracer = config.GetTracer();

							GlobalTracer.Register(tracer);

							return tracer;
						});
						services.AddHostedService<ConsumerHostedService>();
					});

			await hostBuilder.RunConsoleAsync();
		}

		//static void Main(string[] args)
		//{
		//	var factory = new ConnectionFactory() { HostName = "localhost" };
		//	using (var connection = factory.CreateConnection())
		//	using (var channel = connection.CreateModel())
		//	{
		//		channel.QueueDeclare(queue: "wharehouse",
		//												 durable: false,
		//												 exclusive: false,
		//												 autoDelete: false,
		//												 arguments: null);

		//		var consumer = new EventingBasicConsumer(channel);
		//		consumer.Received += (model, ea) =>
		//		{
		//			var body = ea.Body;
		//			var message = Encoding.UTF8.GetString(body.ToArray());
		//			Console.WriteLine(" [x] Received {0}", message);
		//		};
		//		channel.BasicConsume(queue: "wharehouse",
		//												 autoAck: true,
		//												 consumer: consumer);

		//		Console.WriteLine(" Press [enter] to exit.");
		//		Console.ReadLine();
		//	}
		//}
	}
}
