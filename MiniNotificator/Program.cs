using MiniNotificator.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// Configurazione AppIO da sezione "AppIo" di appsettings.json
builder.Services.Configure<AppIoOptions>(builder.Configuration.GetSection("AppIo"));

// Client HTTP tipizzato per le API IO
builder.Services.AddHttpClient<AppIoClient>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();
