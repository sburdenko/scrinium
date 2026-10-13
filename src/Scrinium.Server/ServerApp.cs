using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Scrinium.Core;
using Scrinium.Core.Settings;
using Scrinium.Extraction;
using Scrinium.Ollama;
using Scrinium.Server.Endpoints;
using Scrinium.Server.Security;
using Scrinium.Storage;

namespace Scrinium.Server;

public static class ServerApp
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
            WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot")
        });

        var options = builder.Configuration.GetSection("Scrinium").Get<ServerOptions>() ?? new ServerOptions();
        var home = Path.GetFullPath(options.Home);

        builder.Services.AddSingleton(options);
        builder.Services.AddScriniumCore(Path.Combine(home, "data"), Path.Combine(home, "settings.json"));
        builder.Services.AddScriniumSqliteStorage();
        builder.Services.AddScriniumTextExtractors();
        builder.Services.AddScriniumOllama();
        builder.Services.AddSingleton(new ChatGate(options.MaxConcurrentChats));
        builder.Services.AddSingleton<ApiKeyFilter>();
        builder.Services.AddHostedService<DocumentFolderImporter>();

        AddRateLimiting(builder.Services, options);
        AddCors(builder.Services, options);

        configure?.Invoke(builder);

        var app = builder.Build();
        ApplyModelOverrides(app, options);
        Configure(app, options);
        return app;
    }

    private static void AddRateLimiting(IServiceCollection services, ServerOptions options) =>
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (!context.Request.Path.StartsWithSegments("/api/chat"))
                    return RateLimitPartition.GetNoLimiter("other");

                var client = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(client, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, options.ChatRequestsPerMinute),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
            });
        });

    private static void AddCors(IServiceCollection services, ServerOptions options)
    {
        if (options.AllowedOrigins.Length == 0)
            return;

        services.AddCors(cors => cors.AddDefaultPolicy(policy => policy
            .WithOrigins(options.AllowedOrigins)
            .WithMethods("GET", "POST")
            .WithHeaders("Content-Type", ApiKeyFilter.HeaderName)));
    }

    private static void ApplyModelOverrides(WebApplication app, ServerOptions options)
    {
        var settings = app.Services.GetRequiredService<SettingsService>();
        settings.Update(current => current with
        {
            OllamaUrl = options.OllamaUrl ?? current.OllamaUrl,
            ChatModel = options.ChatModel ?? current.ChatModel,
            EmbeddingModel = options.EmbeddingModel ?? current.EmbeddingModel
        });
    }

    private static void Configure(WebApplication app, ServerOptions options)
    {
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        });

        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Content-Security-Policy"] =
                "default-src 'self'; script-src 'self'; style-src 'self'; base-uri 'none'";
            await next();
        });

        if (options.AllowedOrigins.Length > 0)
            app.UseCors();

        app.UseRateLimiter();
        app.UseDefaultFiles();
        app.UseStaticFiles();

        ChatEndpoint.Map(app);
        SourcesEndpoint.Map(app);
        DocumentsEndpoint.Map(app);
        HealthEndpoint.Map(app);
    }
}
