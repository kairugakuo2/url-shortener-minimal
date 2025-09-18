using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// EF Core + SqLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi

// 1 - SERVICES
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo{Title = "Url Shortener API", Version = "v1"});
});

var app = builder.Build();

////////////////////////////////////////////////////////////

// Configure the HTTP request pipeline.
// 2 - PIPELINE
app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => 
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Url Shortener API v1");
        c.RoutePrefix = "swagger"; //UI at /swagger
    });
}

// ROOT -> REDIRECT TO SWAGGER
app.MapGet("/", () => Results.Redirect("/swagger"));

// Sample endpoint (default template)
var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

// RECORD TYPE
record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
