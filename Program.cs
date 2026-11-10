using System;
using System.Threading.Tasks;

namespace MiniHttpServer
{
    public interface ITimeService
    {
        Task<string> GetCurrentTimeAsync();
    }

    public class TimeService : ITimeService
    {
        public async Task<string> GetCurrentTimeAsync()
        {
            await Task.Delay(100); // Имитация асинхронной работы (например, запрос к БД)
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
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
        public async Task GetTime(HttpResponse response)
        {
            var time = await _timeService.GetCurrentTimeAsync();
            response.SendJson(new { server_time = time });
        }

        // Демонстрация извлечения параметра {id} из пути
        [HttpGet("users/{id}")]
        public void GetUserById(int id, HttpResponse response)
        {
            response.SendJson(new { user_id = id, message = $"Поиск пользователя с ID {id}" });
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
            services.AddSingleton<ITimeService, TimeService>();
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
