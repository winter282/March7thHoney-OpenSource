using System.Net;
using March7thHoney.Util;
using March7thHoney.WebServer.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace March7thHoney.WebServer;

public class WebProgram
{
    // LOCAL(fix-login-4600): request trace logger.
    private static readonly Logger HttpTraceLogger = new("HttpTrace");

    public static void Main(string[] args, int port, string address)
    {
        BuildWebApp(args, port, address).Start();
    }

    public static WebApplication BuildWebApp(string[] args, int port, string address)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls(address);

        if (ConfigManager.Config.HttpServer.UseSSL)
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Listen(IPAddress.Any, port, listenOptions =>
                {
                    listenOptions.UseHttps(
                        ConfigManager.Config.KeyStore.KeyStorePath,
                        ConfigManager.Config.KeyStore.KeyStorePassword
                    );
                });
            });

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll",
                policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        });

        // Use source-generated JSON metadata (AOT/trim safe) for all endpoint
        // serialization and body binding, ahead of the reflection-based resolver.
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, WebJsonContext.Default));

        var app = builder.Build();

        app.UseForwardedHeaders();

        // LOCAL(fix-login-4600): request-level trace for login debugging.
        // Records method/path/status only (never the body). Remove once login is stable.
        app.Use(async (context, next) =>
        {
            var started = DateTime.UtcNow;
            await next.Invoke();
            var ms = (DateTime.UtcNow - started).TotalMilliseconds;
            HttpTraceLogger.Debug(
                $"http {context.Request.Method} {context.Request.Path}{context.Request.QueryString} -> {context.Response.StatusCode} ({ms:F0}ms)");
        });

        // Buffer the response so we can emit an explicit Content-Length and strip
        // Transfer-Encoding: chunked — some game clients are strict about this.
        app.Use(async (context, next) =>
        {
            using var buffer = new MemoryStream();
            var response = context.Response;

            var bodyStream = response.Body;
            response.Body = buffer;

            try
            {
                await next.Invoke();
                buffer.Position = 0;

                if (!response.HasStarted)
                {
                    context.Response.Headers["Content-Length"] = (response.ContentLength ?? buffer.Length).ToString();
                    context.Response.Headers.Remove("Transfer-Encoding");
                }

                await buffer.CopyToAsync(bodyStream);
            }
            finally
            {
                response.Body = bodyStream;
            }
        });

        app.UseHttpsRedirection();

        app.UseCors("AllowAll");

        // Minimal API endpoints (converted from MVC controllers).
        app.MapLogServerRoutes();
        app.MapGateServerRoutes();
        app.MapServerExchangeRoutes();
        app.MapMuipServerRoutes();
        app.MapHandbookRoutes();
        app.MapJsonRoutes();
        app.MapDispatchRoutes();
        app.MapSdkShopRoutes();
        app.MapAdminRoutes();

        return app;
    }
}
