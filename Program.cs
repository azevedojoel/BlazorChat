using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using BlazorChat.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Register ChatService - read API key from configuration
var openAiApiKey = builder.Configuration["OpenAI:ApiKey"] ?? 
                   Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var braveApiKey = builder.Configuration["Brave:ApiKey"] ?? 
                  Environment.GetEnvironmentVariable("BRAVE_API_KEY");

builder.Services.AddSingleton(new ChatService(openAiApiKey, braveApiKey));

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
