using System;
using System.Threading.Tasks;

namespace MiniHttpServer
{
    public interface ITimeService
    {
        string GetCurrentTime();
    }

    public class TimeService : ITimeService
    {
        public string GetCurrentTime() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public class CreateUserDto
    {
        public string Username { get; set; }
        public string Email { get; set; }
    }

    [Route("api/v1")]
    public class SampleController
    {
        private readonly ITimeService _timeService;

        public SampleController(ITimeService timeService)
        {
            _timeService = timeService;
        }

        [HttpGet("time")]
        public void GetTime(HttpResponse response)
        {
            response.SendJson(new { server_time = _timeService.GetCurrentTime() });
        }

        [HttpGet("greet")]
        public void Greet(string name, HttpResponse response)
        {
            string userName = string.IsNullOrEmpty(name) ? "Гость" : name;
            response.SendJson(new { message = $"Привет, {userName}!" });
        }

        [HttpPost("users")]
        public void CreateUser(CreateUserDto dto, HttpResponse response)
        {
            response.SendJson(new
            {
                status = "Успешно создано",
                received_data = dto
            }, 201);
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var services = new ServiceCollection();
            services.AddTransient<ITimeService, TimeService>();
            services.AddTransient<SampleController, SampleController>();

            var serviceProvider = services.BuildServiceProvider();
            var router = new Router(serviceProvider);

            var server = new HttpServer(8080, async (req, res) =>
            {
                await router.HandleRouteAsync(req, res);
            });

            await server.StartAsync();
        }
    }
}
