using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using BlazorChat.Services;
using Microsoft.Extensions.Logging;
using BlazorChat.Tools;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

// Register ChatService - read API key from configuration
var openAiApiKey = builder.Configuration["OpenAI:ApiKey"] ?? 
                   Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var braveApiKey = builder.Configuration["Brave:ApiKey"] ?? 
                  Environment.GetEnvironmentVariable("BRAVE_API_KEY");

// Register tools
if (!string.IsNullOrEmpty(braveApiKey))
{
    builder.Services.AddSingleton<ITool>(new WebSearchTool(braveApiKey));
}
builder.Services.AddSingleton<ITool>(new FetchUrlsTool());

// Register ToolRegistry
builder.Services.AddSingleton<ToolRegistry>(sp => 
    new ToolRegistry(sp.GetServices<ITool>())
);

builder.Services.AddSingleton(serviceProvider => 
    new ChatService(
        openAiApiKey, 
        braveApiKey, 
        serviceProvider.GetRequiredService<ILogger<ChatService>>()
    )
);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
