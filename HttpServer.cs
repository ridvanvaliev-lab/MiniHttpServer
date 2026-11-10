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

namespace MiniHttpServer
{
    public class HttpServer
    {
        private readonly int _port;
        private readonly Func<HttpRequest, HttpResponse, Task> _requestHandler;
        private X509Certificate2 _serverCertificate;

        public HttpServer(int port, Func<HttpRequest, HttpResponse, Task> requestHandler)
        {
            _port = port;
            _requestHandler = requestHandler;
            _serverCertificate = GenerateSelfSignedCertificate();
        }

        public async Task StartAsync()
        {
            Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Any, _port));
            listener.Listen(128);

            Console.WriteLine($"[HTTP Server] Запущен на https://localhost:{_port}");

            while (true)
            {
                Socket clientSocket = await listener.AcceptAsync();
                _ = Task.Run(() => HandleClientAsync(clientSocket));
            }
        }

        private async Task HandleClientAsync(Socket clientSocket)
        {
            try
            {
                using (clientSocket)
                await using (var networkStream = new NetworkStream(clientSocket, ownsSocket: false))
                await using (var sslStream = new SslStream(networkStream, leaveInnerStreamOpen: false))
                {
                    await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                    {
                        ServerCertificate = _serverCertificate,
                        ClientCertificateRequired = false,
                        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                    });

                    var pipe = new Pipe();
                    await FillPipeAsync(sslStream, pipe.Writer);
                    
                    var request = await ParseRequestAsync(pipe.Reader);
                    var response = new HttpResponse();

                    await _requestHandler(request, response);

                    await SendResponseAsync(sslStream, response);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Ошибка соединения]: {ex.Message}");
            }
        }

        private async Task FillPipeAsync(SslStream sslStream, PipeWriter writer)
        {
            const int minimumBufferSize = 512;
            while (true)
            {
                Memory<byte> memory = writer.GetMemory(minimumBufferSize);
                try
                {
                    int bytesRead = await sslStream.ReadAsync(memory);
                    if (bytesRead == 0) break;

                    writer.Advance(bytesRead);
                }
                catch
                {
                    break;
                }

                FlushResult result = await writer.FlushAsync();
                if (result.IsCompleted) break;
            }
            await writer.CompleteAsync();
        }

        private async Task<HttpRequest> ParseRequestAsync(PipeReader reader)
        {
            ReadResult result = await reader.ReadAsync();
            ReadOnlySequence<byte> buffer = result.Buffer;

            string rawData = Encoding.UTF8.GetString(buffer.ToArray());
            var request = new HttpRequest();

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

            if (parts.Length > 1 && request.Headers.TryGetValue("Content-Length", out var contentLengthStr) 
                && int.TryParse(contentLengthStr, out int contentLength))
            {
                request.Body = parts[1].Substring(0, Math.Min(contentLength, parts[1].Length));
            }

            reader.AdvanceTo(buffer.End);
            return request;
        }

        private async Task SendResponseAsync(SslStream sslStream, HttpResponse response)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"HTTP/1.1 {response.StatusCode} {response.StatusMessage}");
            sb.AppendLine($"Content-Type: {response.ContentType}");
            sb.AppendLine($"Content-Length: {Encoding.UTF8.GetByteCount(response.Body ?? "")}");
            sb.AppendLine("Connection: close");
            sb.AppendLine();
            sb.Append(response.Body);

            byte[] responseBytes = Encoding.UTF8.GetBytes(sb.ToString());
            await sslStream.WriteAsync(responseBytes);
            await sslStream.FlushAsync();
        }

        private X509Certificate2 GenerateSelfSignedCertificate()
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=MiniHttpServerDev", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));

            var certificate = request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));
            return new X509Certificate2(certificate.Export(X509ContentType.Pfx));
        }
    }
}
