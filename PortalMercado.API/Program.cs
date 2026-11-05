using PortalMercado.Infrastructure.Security;
using PortalMercado.API.Middleware;
using PortalMercado.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPortalAccess("API", builder.Configuration, builder.Environment);
builder.Services.AddControllers();
builder.Services.AddScoped<PortalMercado.API.Controllers.ApiAntiforgeryFilter>();
builder.Services.AddOpenApi(options => options.AddSchemaTransformer((schema, context, _) =>
{
    var example = PortalMercado.API.OpenApi.RequestExamples.For(context.JsonTypeInfo.Type);
    if (example is not null) schema.Examples = [example];
    return Task.CompletedTask;
}));

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("PortalMercadoWeb", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .WithMethods("GET")
            .WithHeaders("Accept", "Content-Type")
            .WithExposedHeaders("X-Correlation-ID");
    });
});

var app = builder.Build();
if (args.Contains("--bootstrap-admin") || args.Contains("--provision-test-user"))
{
    using var scope = app.Services.CreateScope();
    try
    {
        var bootstrap = scope.ServiceProvider.GetRequiredService<PortalMercado.Application.Security.IAdminBootstrap>();
        if (args.Contains("--provision-test-user"))
        {
            await bootstrap.CreateLocalTestUserAsync(builder.Configuration["Bootstrap:Correo"] ?? "",
                builder.Configuration["Bootstrap:Nombre"] ?? "", builder.Configuration["Bootstrap:Password"] ?? "",
                CancellationToken.None);
            Console.WriteLine("Usuario ficticio local aprovisionado. Sin permisos comerciales ni administrativos.");
        }
        else
        {
            await bootstrap.CreateAsync(builder.Configuration["Bootstrap:Correo"] ?? "",
                builder.Configuration["Bootstrap:Nombre"] ?? "", builder.Configuration["Bootstrap:Password"] ?? "",
                CancellationToken.None);
            Console.WriteLine("Administrador aprovisionado INACTIVO. Requiere enrolamiento MFA antes de habilitar acceso.");
        }
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(BootstrapDiagnostics.Describe(exception));
        Environment.ExitCode = 1;
    }
    return;
}
app.UseMiddleware<RequestHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "PortalMercado API - Etapa 02 (en desarrollo)");
        options.RoutePrefix = "swagger";
        options.UseRequestInterceptor("""
            function(request) {
              var method = (request.method || 'GET').toUpperCase();
              if (['GET', 'HEAD', 'OPTIONS'].includes(method)) {
                return request;
              }
              return fetch('/api/auth/csrf', {credentials: 'same-origin'})
                .then(function(response) {
                  if (!response.ok) {
                    throw new Error('No se pudo preparar la solicitud. Espera y vuelve a intentar.');
                  }
                  return response.json();
                })
                .then(function(data) {
                  request.headers = request.headers || {};
                  request.headers['X-CSRF-TOKEN'] = data.requestToken;
                  return request;
                });
            }
            """);
    });
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("PortalMercadoWeb");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program
{
}
