using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Api.Data;
using UrlShortener.Api.Models;
using UrlShortener.Api.Validation;

var builder = WebApplication.CreateBuilder(args);

// EF Core + SqLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
                      ?? "Data Source=UrlShortener.db"));

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

/// ALL ENDPOINTS BELOW ////////////////

// 1 - ROOT -> REDIRECT TO SWAGGER
app.MapGet("/", () => Results.Redirect("/swagger"))
    .WithSummary("Redirects to API Doc");


// 2- GLOBAL ERROR HANDLER (for unhadled exceptions globaly) (b4 endpoints)
// returns ProblemDetails JSON w/ 500 status instead of crashing
app.UseExceptionHandler("/error");
app.MapGet("/error", (HttpContext http) =>
    Results.Problem(
        title: "Unexpected error", //short error title
        detail: "Something went wrong on our end.", // friendly message
        statusCode: StatusCodes.Status500InternalServerError // HTTP 500
    )
).WithSummary("Returns ProblemDetails JSON for unhandled server errors");

// 3- HEALTH CHECK ENDPOINT
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithSummary("Simple Health Check (200 = Good!)");

// DEMO DATA (for use in Development mode only for debugging/demoing)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (!db.UrlMaps.Any())
    {
        db.UrlMaps.AddRange(
            new UrlMap { ShortCode = "abc123", LongUrl = "https://example.com", CreatedAt = DateTime.UtcNow, ClickCount = 0 },
            new UrlMap { ShortCode = "xyz789", LongUrl = "https://openai.com", CreatedAt = DateTime.UtcNow, ClickCount = 0 }
        );
        db.SaveChanges();
    }
}

// MAIN 3 ENDPOINTS //////////////////////////
// 4 - POST (for creating shortened url)
app.MapPost("/api/shorten", async (
    UrlRequest request,     // DTO { string LongUrl }
    AppDbContext db,        // db context
    HttpContext http       // gives request info (scheme, host, etc.)
) =>
{
    // check if longurl is real HTTP/HTTPS URL, else return 400 Error + ProblemDetails JSON
    if (!UrlValidation.IsValidHttpUrl(request.LongUrl, out var uri, out var error))
    {
        return Results.ValidationProblem(
            errors: new Dictionary<string, string[]>
            {
                ["longUrl"] = new[] { error! }  // surface the field-specific error
            },
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid URL",
            detail: "Fix the errors and try again."
        );
    }

    // convert URI object into string & lowercase
    var normalized = uri!.ToString();

    // if long url alr exists, reuse it instead of making a new one
    var existing = await db.UrlMaps.FirstOrDefaultAsync(u => u.LongUrl == normalized);
    if (existing is not null)
    {
        var shortUrl = $"{http.Request.Scheme}://{http.Request.Host}/{existing.ShortCode}";
        return Results.Ok(new
        {
            code = existing.ShortCode,
            shortUrl,
            longUrl = existing.LongUrl,
            createdAt = existing.CreatedAt,
            clickCount = existing.ClickCount,
            reused = true   //lets user know that url was reused
        });
    }

    // Generate a random short code + build/save to new db record
    var shortCode = Guid.NewGuid().ToString("N")[..6]; // 6 chars
    var urlMap = new UrlMap
    {
        ShortCode = shortCode,
        LongUrl = normalized,
        CreatedAt = DateTime.UtcNow,
        ClickCount = 0
    };
    db.UrlMaps.Add(urlMap);
    await db.SaveChangesAsync();

    // build the full short link
    var path = $"/{shortCode}";
    var fullShortUrl = $"{http.Request.Scheme}://{http.Request.Host}{path}";

    // return 201 Code (Created Successufly) + necessary details
    return Results.Created(path, new
    {
        code = shortCode,
        shortUrl = fullShortUrl,
        longUrl = urlMap.LongUrl,
        createdAt = urlMap.CreatedAt,
        clickCount = 0,
        reused = false      //brand new link so hasn't been reused
    });
}).WithSummary("Creates a short URL just for you! (replace 'string' with 'https://example.com')");

// 5- GET (for redirecting shorturl to longurl when clicked)
app.MapGet("/{code}", async (string code, AppDbContext db) =>
{
    // if code is null, whitespace, or not 6 chars, return 400 error
    if (string.IsNullOrWhiteSpace(code) || code.Length != 6)
    {
        return Results.ValidationProblem(
            errors: new Dictionary<string, string[]>
            {
                ["code"] = new[] { "Short code is required and must be 6 characters." }
            },
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid short code",
            detail: "Please fix the errors and try again."
        );
    }

    // if passed check, look for a match w/the short code
    var urlMap = await db.UrlMaps.FirstOrDefaultAsync(u => u.ShortCode == code);
    if (urlMap is null)
    {
        return Results.Problem(
            title: "Not Found",
            detail: $"No URL found for code '{code}'.",
            statusCode: StatusCodes.Status404NotFound
        );
    }
    
    urlMap.ClickCount++;             //increment click count
    await db.SaveChangesAsync();    //save to db

    // permanent: false -> 302 TEMP REDIRECT -> browser won't cache forever
    return Results.Redirect(urlMap.LongUrl, permanent: false);

}).WithSummary("Redirects short code to its original long URL");


// 6 - GET (for stats [clickcount, timecreated, etc.])
app.MapGet("/api/urls/{code}/stats", async (string code, AppDbContext db) =>
{
    // if code is null, whitespace, or not 6 chars, return 400 error
    //same validation in REDIRECT endpoint
    if (string.IsNullOrWhiteSpace(code) || code.Length != 6)
    {
        return Results.ValidationProblem(
            errors: new Dictionary<string, string[]>
            {
                ["code"] = new[] { "Short code is required and must be 6 characters." }
            },
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid short code",
            detail: "Please fix the errors and try again."
        );
    }

    // look for match again (read only -> .AsNoTracking)
    var urlMap = await db.UrlMaps.AsNoTracking().FirstOrDefaultAsync(u => u.ShortCode == code);
    if (urlMap is null)
    {
        return Results.Problem(
            title: "Not Found",
            detail: $"No URL found for code '{code}'.",
            statusCode: StatusCodes.Status404NotFound
        );
    }

    return Results.Ok(new
    {
        code = urlMap.ShortCode,
        longUrl = urlMap.LongUrl,
        createdAt = urlMap.CreatedAt,
        clickCount = urlMap.ClickCount
    });
}).WithSummary("Returns Stats for a short URL");


app.Run();

// RECORD TYPE
record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
