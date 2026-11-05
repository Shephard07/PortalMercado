using PortalMercado.Infrastructure.Security;
using PortalMercado.Infrastructure;
using PortalMercado.Web.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPortalAccess("MVC", builder.Configuration, builder.Environment);
builder.Services.AddControllersWithViews();

var app = builder.Build();
app.UseMiddleware<RequestHandlingMiddleware>();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
app.Run();
