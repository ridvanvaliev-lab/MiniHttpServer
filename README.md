# MiniHttpServer

Custom HTTP/HTTPS server and lightweight web framework built from scratch in C# (.NET 8).

## Features
- **Low-level networking:** Uses `Socket` and `SslStream` for secure TLS/HTTPS connections with auto-generated self-signed certificates.
- **High performance streaming:** Implements `System.IO.Pipelines` (`PipeReader` / `PipeWriter`) for zero-allocation byte stream processing.
- **Custom DI Container:** Reflection-based dependency injection supporting `Transient` and `Singleton` lifecycles and constructor injection.
- **Routing & Model Binding:** Automatically scans assembly via reflection for `[Route]`, `[HttpGet]`, and `[HttpPost]` attributes, resolving parameters from Query strings or JSON request bodies.
