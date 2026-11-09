using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace MiniHttpServer
{
    public class RouteEntry
    {
        public string Method { get; set; }
        public string Template { get; set; }
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

                    if (getAttr != null)
                    {
                        httpMethod = "GET";
                        methodRoute = getAttr.Template;
                    }
                    else if (postAttr != null)
                    {
                        httpMethod = "POST";
                        methodRoute = postAttr.Template;
                    }
                    else
                    {
                        continue;
                    }

                    string fullTemplate = $"/{controllerRoute}/{methodRoute}".Replace("//", "/").TrimEnd('/');
                    if (string.IsNullOrEmpty(fullTemplate)) fullTemplate = "/";

                    _routes.Add(new RouteEntry
                    {
                        Method = httpMethod,
                        Template = fullTemplate,
                        ActionMethod = method,
                        ControllerType = type
                    });
                }
            }
        }

        public async Task HandleRouteAsync(HttpRequest request, HttpResponse response)
        {
            var route = _routes.FirstOrDefault(r => 
                r.Method.Equals(request.Method, StringComparison.OrdinalIgnoreCase) && 
                r.Template.Equals(request.Path, StringComparison.OrdinalIgnoreCase));

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

                    if (param.ParameterType == typeof(HttpRequest))
                    {
                        invokeArgs[i] = request;
                        continue;
                    }
                    if (param.ParameterType == typeof(HttpResponse))
                    {
                        invokeArgs[i] = response;
                        continue;
                    }

                    if (request.QueryString.TryGetValue(param.Name, out var queryVal))
                    {
                        invokeArgs[i] = Convert.ChangeType(queryVal, param.ParameterType);
                        continue;
                    }

                    if (!param.ParameterType.IsPrimitive && param.ParameterType != typeof(string))
                    {
                        if (!string.IsNullOrEmpty(request.Body))
                        {
                            invokeArgs[i] = JsonSerializer.Deserialize(request.Body, param.ParameterType, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            continue;
                        }
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
