using System;

namespace UrlShortener.Api.Validation;

public static class UrlValidation
{
    // hardcoded max length of long url
    public const int MaxUrlLength = 2048;

    // Returns true if input is a valid http/https  URL, else return error
    public static bool IsValidHttpUrl(string? input, out Uri? uri, out string? error)
    {
        error = null;
        uri = null; //uri is the full url with extra data bits at the end kinda

        // if url is empty
        if (string.IsNullOrWhiteSpace(input))
        {
            error = "URL is required.";
            return false;
        }

        // if not empty, trim the input whitespaces
        input = input.Trim();

        //check length
        if (input.Length > MaxUrlLength)
        {
            error = $"URL exceeds max length of {MaxUrlLength} characters.";
            return false;
        }

        // parse into a valid URI
        if (!Uri.TryCreate(input, UriKind.Absolute, out var parsed))
        {
            error = "URL is not a valid absolute URI.";
            return false;
        }

        // only allow HTTP or HTTPS
        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
        {
            error = "Only http and https URLs are allowed.";
            return false;
        }

        //if all checks pass, return URI (parsed through)
        uri = parsed;
        return true;
    }
}