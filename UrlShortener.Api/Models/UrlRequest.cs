using System;

namespace UrlShortener.Api.Models;

public class UrlRequest
{
    public string LongUrl { get; set; } = string.Empty;
}