using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using LanguageExt;
using Serilog;
using static LanguageExt.Prelude;

namespace DailyBing
{
    internal class Program
    {
        private const string RootUrl = "https://www.bing.com";
        private const string FeedPath = "HPImageArchive.aspx?format=js&idx=0&n=2";

        private static async Task Main()
        {
            var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "DailyBing.log");
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(logPath, fileSizeLimitBytes: 200000, rollOnFileSizeLimit: true, retainedFileCountLimit: 1)
                .WriteTo.Console()
                .CreateLogger();

            var http = new HttpClient();
            var httpResult = await http.GetJsonAsync<WallpaperMeta>($"{RootUrl}/{FeedPath}");

            var meta = httpResult.Match(
                Succ: wallpaperMeta => wallpaperMeta,
                Fail: ex =>
                {
                    Log.Error(ex, "Loading meta data from Bing failed");
                    throw ex;
                });

            if (meta.Images.Length <= 0)
            {
                Log.Warning("No images today");
                Environment.Exit(1);
            }

            var imageUri = new Uri(RootUrl + meta.Images[0].Url);
            var imgQuery = HttpUtility.ParseQueryString(imageUri.Query);
            var oldImgQuery = HttpUtility.ParseQueryString(new Uri(RootUrl + meta.Images[1].Url).Query);
            var path = Path.Combine(Path.GetTempPath(), imgQuery["id"]);
            var oldPath = Path.Combine(Path.GetTempPath(), oldImgQuery["id"]);

            var dlAndWrite = from d in TryAsync(http.GetByteArrayAsync(imageUri))
                       from u in TryAsync(File.WriteAllBytesAsync(path, d))
                       select u;


            _ = await dlAndWrite.IfFail(Fail: () =>
            {
                Log.Error("Download failed");
                Environment.Exit(1);
                return Task.CompletedTask;
            });

            Win32Wallpaper.Set(path, Win32Wallpaper.Style.Fill);

            var deleteFunc = fun(f: (string p) =>
            {
                File.Delete(p);
                return Unit.Default;
            });

            if (File.Exists(oldPath))
            {
                var rmResult = Try(deleteFunc(oldPath));
                if (rmResult.IsFail()) Log.Warning("Deleting old bing wallpaper failed");
            }

            Log.Information("Wallpaper update completed with {title} @ {file}",meta.Images[0].Title, path);

            Log.CloseAndFlush();
        }
    }
}