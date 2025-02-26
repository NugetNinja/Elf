﻿using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Elf.Api;

public static class Utils
{
    public static string AppVersion
    {
        get
        {
            var asm = Assembly.GetEntryAssembly();
            if (null == asm) return "N/A";

            var fileVersion = asm.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
            var version = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(version) && version.IndexOf('+') > 0)
            {
                var gitHash = version[(version.IndexOf('+') + 1)..];
                var prefix = version[..version.IndexOf('+')];

                if (gitHash.Length <= 6) return version;

                var gitHashShort = gitHash.Substring(gitHash.Length - 6, 6);
                return !string.IsNullOrWhiteSpace(gitHashShort) ? $"{prefix} ({gitHashShort})" : fileVersion;
            }

            return version ?? fileVersion;
        }
    }

    public static string GetClientIP(HttpContext context)
    {
        if (null == context) return null;

        string ip = context.Connection.RemoteIpAddress?.ToString();

        var forwardHeaders = context.Request.Headers["X-Forwarded-For"];
        if (forwardHeaders.Any() && null != ip)
        {
            var f = forwardHeaders.First();

            // f looks like: "x.x.x.x:xxxxx, x.x.x.x:xxxx, ..."
            // need to extract values
            var ipArray = f.Split(',').Select(x =>
                x.Trim().IndexOf(":", StringComparison.Ordinal) > 0 ?
                    x.Trim()[..(x.IndexOf(":", StringComparison.Ordinal) - 1)] :
                    x.Trim());

            var dip = ipArray.FirstOrDefault(p => p != ip);
            if (dip != null) ip = dip;
        }

        return ip;
    }

    public static bool IsPrivateIP(string ip) => IPAddress.Parse(ip).GetAddressBytes() switch
    {
        // Regex.IsMatch(ip, @"(^127\.)|(^10\.)|(^172\.1[6-9]\.)|(^172\.2[0-9]\.)|(^172\.3[0-1]\.)|(^192\.168\.)")
        // Regex has bad performance, this is better

        var x when x[0] is 192 && x[1] is 168 => true,
        var x when x[0] is 10 => true,
        var x when x[0] is 127 => true,
        var x when x[0] is 172 && x[1] is >= 16 and <= 31 => true,
        _ => false
    };

    public enum UrlScheme
    {
        Http,
        Https,
        All
    }

    public static bool IsValidUrl(this string url, UrlScheme urlScheme = UrlScheme.All)
    {
        var isValidUrl = Uri.TryCreate(url, UriKind.Absolute, out var uriResult);
        if (!isValidUrl) return false;

        isValidUrl &= urlScheme switch
        {
            UrlScheme.All => uriResult.Scheme == Uri.UriSchemeHttps || uriResult.Scheme == Uri.UriSchemeHttp,
            UrlScheme.Https => uriResult.Scheme == Uri.UriSchemeHttps,
            UrlScheme.Http => uriResult.Scheme == Uri.UriSchemeHttp,
            _ => throw new ArgumentOutOfRangeException(nameof(urlScheme), urlScheme, null),
        };
        return isValidUrl;
    }

    /// <summary>
    /// Get values from `ELF_TAGS` Environment Variable
    /// </summary>
    /// <returns>string values</returns>
    public static IEnumerable<string> GetEnvironmentTags()
    {
        var tagsEnv = Environment.GetEnvironmentVariable("ELF_TAGS");
        if (string.IsNullOrWhiteSpace(tagsEnv))
        {
            yield return string.Empty;
            yield break;
        }

        var tagRegex = new Regex(@"^[a-zA-Z0-9-#@$()\[\]/]+$");
        var tags = tagsEnv.Split(',');
        foreach (var tag in tags)
        {
            var t = tag.Trim();
            if (tagRegex.IsMatch(t))
            {
                yield return t;
            }
        }
    }
}
