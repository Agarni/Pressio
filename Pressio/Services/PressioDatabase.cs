using System;
using System.IO;

namespace Pressio.Services;

public static class PressioDatabase
{
    public static string Path => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pressio", "pressio.db");
}
