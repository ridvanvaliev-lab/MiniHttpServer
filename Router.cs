using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MiniHttpServer
{
    public class RouteEntry
    {
        public string Method { get; set; }
        public string Template { get; set; }
        public Regex RouteRegex { get; set; }
        public MethodInfo ActionMethod { get; set; }
        public Type ControllerType { get; set; }
    }

    public class Router
    {
        private readonly List<RouteEntry> _routes = new();
        private readonly ServiceProvider _serviceProvider;

        public Router(ServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            RegisterControllers();
        }

        private void RegisterControllers()
        {
            var controllerTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.GetCustomAttribute<RouteAttribute>() != null);

            foreach (var type in controllerTypes)
            {
                var routeAttr = type.GetCustomAttribute<RouteAttribute>();
                string controllerRoute = routeAttr.Template?.Trim('/') ?? "";

                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                foreach (var method in methods)
                {
                    string httpMethod = "GET";
                    string methodRoute = "";

                    var getAttr = method.GetCustomAttribute<HttpGetAttribute>();
                    var postAttr = method.GetCustomAttribute<HttpPostAttribute>();

                    if (getAttr != null) { httpMethod = "GET"; methodRoute = getAttr.Template; }
                    else if (postAttr != null) { httpMethod = "POST"; methodRoute = postAttr.Template; }
                    else continue;

                    string fullTemplate = $"/{controllerRoute}/{methodRoute}".Replace("//", "/").TrimEnd('/');
                    if (string.IsNullOrEmpty(fullTemplate)) fullTemplate = "/";

                    // Преобразуем шаблон /users/{id} в регулярное выражение ^/users/(?<id>[^/]+)$
                    string regexPattern = "^" + Regex.Replace(fullTemplate, @"\{(\w+)\}", @"(?<$1>[^/]+)") + "$";

                    _routes.Add(new RouteEntry
                    {
                        Method = httpMethod,
                        Template = fullTemplate,
                        RouteRegex = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled),
                        ActionMethod = method,
                        ControllerType = type
                    });
                }
            }
        }

        public async Task HandleRouteAsync(HttpRequest request, HttpResponse response)
        {
            Match match = null;
            var route = _routes.FirstOrDefault(r => 
                r.Method.Equals(request.Method, StringComparison.OrdinalIgnoreCase) && 
                (match = r.RouteRegex.Match(request.Path)).Success);

            if (route == null)
            {
                response.SendText("Маршрут не найден (404)", 404);
                return;
            }

            try
            {
                var controllerInstance = _serviceProvider.GetService(route.ControllerType);
                var parameters = route.ActionMethod.GetParameters();
                var invokeArgs = new object[parameters.Length];

                for (int i = 0; i < parameters.Length; i++)
                {
                    var param = parameters[i];

                    // 1. Инъекция HTTP контекста
                    if (param.ParameterType == typeof(HttpRequest)) { invokeArgs[i] = request; continue; }
                    if (param.ParameterType == typeof(HttpResponse)) { invokeArgs[i] = response; continue; }

                    // 2. Извлечение параметров из пути (Path variables)
                    if (match.Groups.TryGetValue(param.Name, out var pathGroup) && pathGroup.Success)
                    {
                        invokeArgs[i] = Convert.ChangeType(pathGroup.Value, param.ParameterType);
                        continue;
                    }

                    // 3. Извлечение параметров из QueryString
                    if (request.QueryString.TryGetValue(param.Name, out var queryVal))
                    {
                        invokeArgs[i] = Convert.ChangeType(queryVal, param.ParameterType);
                        continue;
                    }

                    // 4. Парсинг тела (JSON)
                    if (!param.ParameterType.IsPrimitive && param.ParameterType != typeof(string) && !string.IsNullOrEmpty(request.Body))
                    {
                        invokeArgs[i] = JsonSerializer.Deserialize(request.Body, param.ParameterType, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        continue;
                    }

                    invokeArgs[i] = param.HasDefaultValue ? param.DefaultValue : null;
                }

                var result = route.ActionMethod.Invoke(controllerInstance, invokeArgs);

                if (result is Task task)
                {
                    await task;
                }
            }
            catch (Exception ex)
            {
                response.SendText($"Внутренняя ошибка сервера: {ex.InnerException?.Message ?? ex.Message}", 500);
            }
        }
    }
}
