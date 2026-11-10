using System;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Net.Security; 
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;


private async Task<HttpRequest> ParseRequestAsync(PipeReader reader)
{
    ReadResult result = await reader.ReadAsync();
    ReadOnlySequence<byte> buffer = result.Buffer;

    string rawData = Encoding.UTF8.GetString(buffer.ToArray());
    var request = new HttpRequest();

    // Разделяем заголовки и тело по двойному переносу строки
    var parts = rawData.Split(new[] { "\r\n\r\n", "\n\n" }, 2, StringSplitOptions.None);
    var headerBlock = parts[0];
    var lines = headerBlock.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

    if (lines.Length == 0 || string.IsNullOrWhiteSpace(lines[0])) 
    {
        reader.AdvanceTo(buffer.End);
        return request;
    }

    var requestLine = lines[0].Split(' ');
    request.Method = requestLine[0];
    
    var fullUri = requestLine.Length > 1 ? requestLine[1] : "/";
    var uriParts = fullUri.Split('?');
    request.Path = uriParts[0];

    if (uriParts.Length > 1)
    {
        foreach (var param in uriParts[1].Split('&'))
        {
            var kv = param.Split('=');
            if (kv.Length == 2)
                request.QueryString[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]);
        }
    }

    for (int i = 1; i < lines.Length; i++)
    {
        var line = lines[i];
        var headerParts = line.Split(new[] { ':' }, 2);
        if (headerParts.Length == 2)
            request.Headers[headerParts[0].Trim()] = headerParts[1].Trim();
    }

    // Читаем тело, только если указан Content-Length
    if (parts.Length > 1 && request.Headers.TryGetValue("Content-Length", out var contentLengthStr) 
        && int.TryParse(contentLengthStr, out int contentLength))
    {
        // Берем ровно столько байт, сколько указано в Content-Length
        request.Body = parts[1].Substring(0, Math.Min(contentLength, parts[1].Length));
    }

    reader.AdvanceTo(buffer.End);
    return request;
}
