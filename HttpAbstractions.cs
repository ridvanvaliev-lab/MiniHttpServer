using System;
using System.Collections.Generic;
using System.Text.Json;

namespace MiniHttpServer
{
    [AttributeUsage(AttributeTargets.Class)]
    public class RouteAttribute : Attribute
    {
        public string Template { get; }
        public RouteAttribute(string template) => Template = template;
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class HttpGetAttribute : Attribute
    {
        public string Template { get; }
        public HttpGetAttribute(string template = "") => Template = template;
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class HttpPostAttribute : Attribute
    {
        public string Template { get; }
        public HttpPostAttribute(string template = "") => Template = template;
    }

    public class HttpRequest
    {
        public string Method { get; set; }
        public string Path { get; set; }
        public Dictionary<string, string> QueryString { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string Body { get; set; }
    }

    public class HttpResponse
    {
        public int StatusCode { get; set; } = 200;
        public string StatusMessage { get; set; } = "OK";
        public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string ContentType { get; set; } = "application/json; charset=utf-8";
        public string Body { get; set; }

        public void SendJson<T>(T data, int statusCode = 200)
        {
            StatusCode = statusCode;
            ContentType = "application/json; charset=utf-8";
            Body = JsonSerializer.Serialize(data);
        }

        public void SendText(string text, int statusCode = 200)
        {
            StatusCode = statusCode;
            ContentType = "text/plain; charset=utf-8";
            Body = text;
        }
    }
}
