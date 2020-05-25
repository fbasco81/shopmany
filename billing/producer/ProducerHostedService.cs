using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTracing;
using OpenTracing.Propagation;
using OpenTracing.Tag;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace producer
{
	class ProducerHostedService : BackgroundService
	{

		private readonly ILogger _logger;
		private IConnection _connection;
		private IModel _channel;
		private readonly ITracer _tracer;

		public ProducerHostedService(ILogger<ProducerHostedService> logger, ITracer tracer)
		{
			_logger = logger;
			_tracer = tracer;

			InitRabbitMQ();
		}

		private void InitRabbitMQ()
		{
			while (true)
			{
				try
				{
					_logger.LogInformation("Billing-ProducerHostedService is connecting to rabbit mq.");

					var factory = new ConnectionFactory() { HostName = Environment.GetEnvironmentVariable("RABBIT_HOST") };
					_connection = factory.CreateConnection();
					// create channel  
					_channel = _connection.CreateModel();

					_channel.QueueDeclare(queue: "payment",
																	 durable: false,
																	 exclusive: false,
																	 autoDelete: false,
																	 arguments: null);

					_logger.LogInformation("Billing-ProducerHostedService connected");
					break;
				}
				catch (Exception ex)
				{
					_logger.LogInformation("Billing-ProducerHostedService connection failed. Next attempt in 5 seconds");
					System.Threading.Thread.Sleep(5000);

				}
			}
		}

		protected override Task ExecuteAsync(CancellationToken stoppingToken)
		{
			stoppingToken.ThrowIfCancellationRequested();

			ThreadPool.QueueUserWorkItem((x) =>
			{
				while (true)
				{
					var rnd = new Random();
					var itemId = rnd.Next(4);

					using (var scope = _tracer.BuildSpan("item-sold").StartActive(finishSpanOnDispose: true))
					{
						var span = scope.Span.SetTag(Tags.SpanKind, Tags.SpanKindClient);

						var traceKeys = new Dictionary<string, string>();
						_tracer.Inject(span.Context, BuiltinFormats.TextMap, new TextMapInjectAdapter(traceKeys));

						string message = JsonSerializer.Serialize(new PayMessage
						{
							customerId = "test customer",
							status = "Confirmed",
						});
						var body = Encoding.UTF8.GetBytes(message);
						var properties = _channel.CreateBasicProperties();
						properties.Headers = new Dictionary<string, object>();
						properties.Headers.Add("tracingKeys", traceKeys);

						_channel.BasicPublish(exchange: "",
																 routingKey: "payment",
																 basicProperties: properties,
																 body: body);

						Console.WriteLine(" [x] Sent {0}", message);

					}

					System.Threading.Thread.Sleep(5000);

				}

			});


			return Task.CompletedTask;
		}

		public override void Dispose()
		{
			_channel.Close();
			_connection.Close();
			base.Dispose();
		}

	}
}
