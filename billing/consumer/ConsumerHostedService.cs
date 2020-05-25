using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using OpenTracing;
using User.Activation.Consumer.Common;
using Newtonsoft.Json;
using consumer;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;

public class ConsumerHostedService : BackgroundService
{
	private readonly ILogger _logger;
	private IConnection _connection;
	private IModel _channel;
	private readonly ITracer _tracer;
	private readonly IHttpClientFactory _clientFactory;

	public ConsumerHostedService(ILogger<ConsumerHostedService> logger, ITracer tracer, IHttpClientFactory clientFactory)
	{
		_logger = logger;
		_tracer = tracer;
		_clientFactory = clientFactory;
		InitRabbitMQ();
	}

	private void InitRabbitMQ()
	{
		while (true)
		{
			try
			{
				_logger.LogInformation("Billing-ConsumerHostedService is connecting to rabbit mq.");

				var factory = new ConnectionFactory() { HostName = Environment.GetEnvironmentVariable("RABBIT_HOST") };
				_connection = factory.CreateConnection();
				// create channel  
				_channel = _connection.CreateModel();

				_channel.QueueDeclare(queue: "payment",
																 durable: false,
																 exclusive: false,
																 autoDelete: false,
																 arguments: null);

				_logger.LogInformation("Billing-ConsumerHostedService connected");
				break;
			}
			catch (Exception ex)
			{
				_logger.LogInformation("Billing-ConsumerHostedService connection failed. Next attempt in 5 seconds");
				System.Threading.Thread.Sleep(5000);

			}

		}
	}

	protected override Task ExecuteAsync(CancellationToken stoppingToken)
	{
		stoppingToken.ThrowIfCancellationRequested();

		var consumer = new EventingBasicConsumer(_channel);
		consumer.Received += async (ch, ea) =>
		{
			var body = ea.Body;
			var tracingKeysOriginal = (Dictionary<string, object>)ea.BasicProperties.Headers["tracingKeys"];

			var message = Encoding.UTF8.GetString(body.ToArray());
			var item = System.Text.Json.JsonSerializer.Deserialize<PayMessage>(message);

			// transform Dictionary<string, object> to Dictionary<string, string>
			Dictionary<string, string> tracingKeysTransformed = new Dictionary<string, string>();
			foreach (var element in tracingKeysOriginal)
			{
				var byteArray = element.Value as byte[];
				var value = Encoding.UTF8.GetString(byteArray, 0, byteArray.Length);
				tracingKeysTransformed.Add(element.Key, value);
				Console.WriteLine($" Decoded trace key: {element.Key} - {value}");
			}

			using (var scope = TracingExtension.StartServerSpan(_tracer, tracingKeysTransformed, "item-sold-ack"))
			{
				var accountUrl = Environment.GetEnvironmentVariable("ACCOUNT_URL");
				if (!string.IsNullOrEmpty(accountUrl))
				{
					var request = new HttpRequestMessage(HttpMethod.Post,
					 Environment.GetEnvironmentVariable("ACCOUNT_URL") + "?customerId=" + item.customerId);
					//request.Headers.Add("Content-Type", "application/json");

					var client = _clientFactory.CreateClient();

					var response = await client.SendAsync(request);

					if (response.IsSuccessStatusCode)
					{
						Console.WriteLine(" [x] {0} payment notified", item.customerId);

					}
					else
					{
						Console.WriteLine(" [x] Error notifying customer");

					}
				}
				

			}


		};

		_channel.BasicConsume(queue: "payment",
														 autoAck: true,
														 consumer: consumer);
		return Task.CompletedTask;
	}

	public override void Dispose()
	{
		_channel.Close();
		_connection.Close();
		base.Dispose();
	}



	public Task StopAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("ConsumerHostedService is stopping.");

		return Task.CompletedTask;
	}

}