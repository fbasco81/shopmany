using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpenTracing;

namespace Account.Controllers
{
	[ApiController]
	[Route("[controller]")]
	[EnableCors("All")]
	public class AccountController : ControllerBase
	{

		private readonly ILogger<AccountController> _logger;
		private readonly ITracer _tracer;
		private readonly IHttpClientFactory _httpClientFactory;
		private static readonly ConcurrentDictionary<string,string> _allEmails = new ConcurrentDictionary<string, string>();

		public AccountController(ILogger<AccountController> logger, ITracer tracer, IHttpClientFactory httpClientFactory)
		{
			_logger = logger;
			_tracer = tracer;
			_httpClientFactory = httpClientFactory;
		}

		[HttpGet("ddlogs")]
		public string GetLogs()
		{
			//var cc = Directory.Exists("/opt/datadog");
			var sb = new StringBuilder();
			//sb.AppendLine("profiler directory exists=" + cc.ToString());
			if (Directory.Exists("/var/log/datadog/dotnet/"))
			{
				Directory.GetFiles("/var/log/datadog/dotnet/").ToList().ForEach(f =>
				{
					sb.Append(System.IO.File.ReadAllText(f));
					sb.Append(Environment.NewLine);
				});
			}
			else
			{
				sb.Append("/var/log/datadog/dotnet/ does NOT Exists");
			}

			return sb.ToString();
		}
		//[HttpGet("envs")]
		//public string GetEnv()
		//{
		//    var sb = new StringBuilder();
		//    foreach(string key in Environment.GetEnvironmentVariables().Keys) { 
		//        sb.AppendLine(key + "=" + Environment.GetEnvironmentVariable(key));
		//    };
		//    return sb.ToString();
		//}

		[HttpGet]
		public async Task<Account> Get()
		{
			var builder = _tracer.BuildSpan("customSpan");

			using (var scope = builder.StartActive(true))
			{
				scope.Span.Log("begin customSpan");
				// to show an out going dependency
				var response = await _httpClientFactory.CreateClient().GetAsync("https://api.namefake.com/");
				var x = await response.Content.ReadAsStringAsync();
				
				scope.Span.Log("response from api.namefake.com: " + x);

				var person = JsonSerializer.Deserialize<Person>(x);
				
				scope.Span.Log("begin customSpan");
				return new Account()
				{
					Id = person.uuid,
					Name = person.name,
					FideltyPoint = 999
				};
			}
		}

		[HttpGet("mails")]
		public IEnumerable<string> GetEmails()
		{
			return _allEmails.Select(x => x.Key + " - " + x.Value + "<br />");
		}

		[HttpPost]
		public bool SendEmail(string customerId)
		{
			_allEmails.TryAdd(DateTime.Now.ToString(), $"Mail sent to ${customerId}");
			var builder = _tracer.BuildSpan("mail");

			using (var scope = builder.StartActive(true))
			{
				scope.Span.Log($"{customerId} payment notified");
				return true;
			}
		}

	}
}
