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

public class ConsumerHostedService : BackgroundService
{
  private readonly ILogger _logger;
  private IConnection _connection;
  private IModel _channel;
  private readonly ITracer _tracer;

  public ConsumerHostedService(ILogger<ConsumerHostedService> logger, ITracer tracer)
  {
    _logger = logger;
    _tracer = tracer;
    InitRabbitMQ();
  }

  private void InitRabbitMQ()
  {
    _logger.LogInformation("ConsumerHostedService is connecting to rabbit mq.");

    var factory = new ConnectionFactory() { HostName = "localhost" };
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
    consumer.Received += (ch, ea) =>
    {
      var body = ea.Body;
      var message = Encoding.UTF8.GetString(body.ToArray());
      var item = System.Text.Json.JsonSerializer.Deserialize<Item>(message);
      using (var scope = TracingExtension.StartServerSpan(_tracer, item.TraceKeys, "user-activation-link-sender-consumer"))
      {
        //some user activation link send business logics

        Console.WriteLine(" [x] Received {0}", message);
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