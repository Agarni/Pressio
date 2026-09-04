using System;

namespace Pressio.Services;

/// <summary>Ponte entre o deep-link da plataforma (ex.: scheme "pressio://") e o app.</summary>
public static class DeepLink
{
    public static event Action<string>? UrlReceived;
    public static void Handle(string url) => UrlReceived?.Invoke(url);
}
