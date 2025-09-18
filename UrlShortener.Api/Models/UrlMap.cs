using System;

namespace UrlShortener.Api.Models;

public class UrlMap
{
    public int Id { get; set; } // Primary Key
    public string ShortCode { get; set; } = string.Empty;// shortened url code
    public string LongUrl { get; set; } = string.Empty; // original url
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // when created
    public int ClickCount { get; set; } = 0; // times short url was clicked
}