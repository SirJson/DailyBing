using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace DailyBing
{
    internal class Program
    {
        private const string RootUrl = "https://www.bing.com";
        private const string FeedUrlFormat = "{0}/HPImageArchive.aspx?format=js&idx={1}&n=1";

        private static async Task Main(string[] args)
        {
            var pictureIndex = 0;
            if (args.Length > 0)
            {
                if (int.TryParse(args[0], NumberStyles.Integer,CultureInfo.InvariantCulture, out var newIndex))
                {
                    pictureIndex = newIndex;
                }
            }
            var http = new HttpClient();
            var meta = await http.GetJsonAsync<WallpaperMeta>(string.Format(FeedUrlFormat, RootUrl, pictureIndex));
            var imageUri = new Uri(RootUrl + meta.Images.First().Url);
            var queryParts = HttpUtility.ParseQueryString(imageUri.Query);
            var data = await http.GetByteArrayAsync(imageUri);
            var path = Path.Combine(Path.GetTempPath(), queryParts["id"]);
            await File.WriteAllBytesAsync(path, data);
            Win32Wallpaper.Set(path, Win32Wallpaper.Style.Fill);
        }
    }
}