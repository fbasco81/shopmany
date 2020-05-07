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
    _logger.LogInformation("ConsumerHostedService is connecting to rabbit mq.");

    var factory = new ConnectionFactory() { HostName = Environment.GetEnvironmentVariable("RABBIT_HOST") };
    _connection = factory.CreateConnection();
    // create channel  
    _channel = _connection.CreateModel();

    _channel.QueueDeclare(queue: "wharehouse",
                             durable: false,
                             exclusive: false,
                             autoDelete: false,
                             arguments: null);

    _logger.LogInformation("ConsumerHostedService connected");
  }

  protected override Task ExecuteAsync(CancellationToken stoppingToken)
  {
    stoppingToken.ThrowIfCancellationRequested();

    var consumer = new EventingBasicConsumer(_channel);
    consumer.Received += async (ch, ea) =>
    {
      var body = ea.Body;
      var message = Encoding.UTF8.GetString(body.ToArray());
      var item = System.Text.Json.JsonSerializer.Deserialize<WharehouseItem>(message);
      using (var scope = TracingExtension.StartServerSpan(_tracer, item.TraceKeys, "item-sold-ack"))
      {
        //some user activation link send business logics
        var itemUrl = Environment.GetEnvironmentVariable("ITEMS_URL");
        if (string.IsNullOrEmpty(itemUrl))
        {
          Console.WriteLine(" [x] {0} received, nothing to do", item);
          return;
        }

        var request = new HttpRequestMessage(HttpMethod.Get,
           Environment.GetEnvironmentVariable("ITEMS_URL"));
        //request.Headers.Add("Content-Type", "application/json");

        var client = _clientFactory.CreateClient();

        var response = await client.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
          using var responseStream = await response.Content.ReadAsStreamAsync();
          var shopItemContainer = await System.Text.Json.JsonSerializer.DeserializeAsync<ShopItemContainer>(responseStream);
          
          var shopItem = shopItemContainer.items.FirstOrDefault(x => x.id == item.ItemId);
          Console.WriteLine(" [x] {0} removed from stock", shopItem.name);

        }
        else
        {
          Console.WriteLine(" [x] Error getting items from store");

        }

      }

      
    };

    _channel.BasicConsume(queue: "wharehouse",
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