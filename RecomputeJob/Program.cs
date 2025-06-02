using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace RecomputeJob
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("RecomputeJob songstats|propertycleanup [force] [sync] [production]");
                return;
            }
            RunAsync(args[0], args.Contains("force"), args.Contains("sync"), args.Contains("production")).Wait();
        }

        static async Task RunAsync(string id, bool force, bool sync, bool production)
        {
            var isDeveloper = !production && IsDeveloper();

            using (var client = new HttpClient(new HttpClientHandler { ClientCertificateOptions = ClientCertificateOption.Automatic }))
            {
                client.BaseAddress = new Uri(isDeveloper ? "https://localhost:44301/" : "https://www.music4dance.net/");
                client.Timeout = new TimeSpan(3,0,0);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var key = Environment.GetEnvironmentVariable("RECOMPUTEJOB_KEY");
                client.DefaultRequestHeaders.Authorization = new TokenAuthenticationHeaderValue(key);

                var response = await client.GetAsync("/api/recompute/" + id + "?" + (force ? "force=true" : "force=false") + "&" + (sync ? "sync=true" : "sync=false"));
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Successfully Recomputed {id}: '{content}'");
                }
                else
                {
                    Console.WriteLine($"Failed to recompute {id}.  Error {response.StatusCode}");
                }
            }
        }

        private static bool IsDeveloper()
        {
            try
            {
                return string.Equals(Environment.GetEnvironmentVariable("AzureWebJobsEnv"), "Development");
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
