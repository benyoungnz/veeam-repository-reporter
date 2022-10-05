using Microsoft.Extensions.Configuration;
using RestSharp;
using RestSharp.Serializers.NewtonsoftJson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security;
using veeam_repository_reporter.Models;

namespace veeam_repository_reporter
{
    class Program
    {

        private static string vbrAPIRouteVersion { get; set; }
        static void Main(string[] args)
        {

            //setup config
            IConfiguration config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

            var vbrSettings = config.GetRequiredSection("VBR").Get<Models.VBRSettings>();
            vbrAPIRouteVersion = vbrSettings.ApiRouteVersion;

          
            
            var options = new RestClientOptions(string.Format("https://{0}:{1}", vbrSettings.Host, vbrSettings.Port));

            //dev only - trust self signed certs, uncomment the line below if you want to put these protection back in place
            options.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

            //define rest client
            var restClient = new RestClient(options);
            restClient.UseNewtonsoftJson();




            //needed for all requests to the api
            restClient.AddDefaultHeader("x-api-version", vbrSettings.ApiVersion);

            if (string.IsNullOrEmpty(vbrSettings.Password))
            {
                ColorConsole.WriteInfo($"Please enter password for {vbrSettings.Username}");

                vbrSettings.Password = GetPassword();
            }
                


            //perform the login
            var loginReq = new RestRequest("/api/oauth2/token", Method.Post);
            loginReq.AddParameter("username", vbrSettings.Username);
            loginReq.AddParameter("password", vbrSettings.Password);
            loginReq.AddParameter("grant_type", "password");


            ColorConsole.WriteWrappedHeader($"{vbrSettings.Host}", headerColor: ConsoleColor.Cyan);

            var resp = restClient.Execute<OAuth2TokenResponse>(loginReq);

            if (resp.IsSuccessful)
            {
                if (string.IsNullOrEmpty(resp.Data.AccessToken))
                    throw new Exception("Login failed, no session token returned");
                else
                {
                    //all requests from now need to be authorized, add the default header
                    restClient.AddDefaultHeader("Authorization", "Bearer " + resp.Data.AccessToken);
                }
            }
            else
            {
                throw new ApplicationException("Login failed, no session token returned");
            }


            //we have a session let's make the magic happen.

            //get sobrs, we will start here
            var sobrs = GetSOBRS(restClient);

            //query the states up front, we can then just query this based on refs in the sobr
            var repoStates = GetRepoStates(restClient);


            //sobr
            foreach (var sobr in sobrs)
            {
                ColorConsole.WriteWrappedHeader($"{sobr.Name}", headerColor: ConsoleColor.Green);
                ColorConsole.WriteEmbeddedColorLine($"Capacity Tier: [green]{sobr.CapacityTier.Enabled}[/green] // Archive Tier: [green]{sobr.ArchiveTier.IsEnabled}[/green]");
                ColorConsole.WriteEmbeddedColorLine($"Placement: [yellow]{sobr.PlacementPolicy.Type}[/yellow] // ID: [yellow]{sobr.Id}[/yellow]");

                //perf extents this SOBR (there may be more than one)
                ColorConsole.WriteInfo("\nPerformance Extents //");
                foreach (var pe in sobr.PerformanceTier.PerformanceExtents)
                {
                    ColorConsole.WriteEmbeddedColorLine($"Name: [yellow]{pe.Name}[/yellow] // Status: [yellow]{pe.Status}[/yellow]");


                    //get state information, such as utilisation
                    var perfRepoState = repoStates.Where(x => x.Id == pe.Id).FirstOrDefault();
                    ColorConsole.WriteSuccess("\nState Information:");
                    if (perfRepoState != null)
                    {
                        ConsoleProgress.WriteProgressBar(CalculatePercent(perfRepoState.UsedSpaceGb, perfRepoState.CapacityGb));
                        ColorConsole.WriteEmbeddedColorLine($"\nPath: [yellow]{perfRepoState.Path}[/yellow] // Hostname: [yellow]{perfRepoState.HostName}[/yellow]");
                        ColorConsole.WriteEmbeddedColorLine($"Capacity (Gb): [yellow]{perfRepoState.CapacityGb}[/yellow] // Free (Gb): [yellow]{perfRepoState.FreeGb}[/yellow] // Used (Gb): [yellow]{perfRepoState.UsedSpaceGb}[/yellow]");
                       
                    }
                    else
                    {
                        ColorConsole.WriteError("No matching state information found for extent");
                    }

                }

                //capacity tier for this SOBR
                if (sobr.CapacityTier.Enabled)
                {
                    ColorConsole.WriteInfo("\nCapacity Tier //");
                    ColorConsole.WriteEmbeddedColorLine($"Operational Restore Days: [yellow]{sobr.CapacityTier.OperationalRestorePeriodDays}[/yellow] // Encryption: [yellow]{sobr.CapacityTier.Encryption.IsEnabled}[/yellow]");
                    ColorConsole.WriteEmbeddedColorLine($"Copy Policy: [yellow]{sobr.CapacityTier.CopyPolicyEnabled}[/yellow] // Move Policy: [yellow]{sobr.CapacityTier.MovePolicyEnabled}[/yellow]");

                    //get state information, such as utilisation
                    var capRepoState = repoStates.Where(x => x.Id == sobr.CapacityTier.ExtentId).FirstOrDefault();
                    ColorConsole.WriteLine("State Information:");
                    if (capRepoState != null)
                    {
                   
                        ColorConsole.WriteEmbeddedColorLine($"Path: [yellow]{capRepoState.Path}[/yellow]");
                        if (capRepoState.Type.Equals("S3Compatible"))
                            ColorConsole.WriteEmbeddedColorLine($"Used (Gb): [yellow]{capRepoState.UsedSpaceGb}[/yellow]");
                        else
                            ColorConsole.WriteEmbeddedColorLine($"Capacity (Gb): [yellow]{capRepoState.CapacityGb}[/yellow] // Free (Gb): [yellow]{capRepoState.FreeGb}[/yellow] // Used (Gb): [yellow]{capRepoState.UsedSpaceGb}[/yellow]");

                    }
                    else
                    {
                        ColorConsole.WriteError("No matching state information found for capacity tier");
                    }
                }

                //archive tier for this SOBR
                if (sobr.ArchiveTier.IsEnabled)
                {
                    ColorConsole.WriteInfo("\nArchive Tier //");
                    ColorConsole.WriteEmbeddedColorLine($"Archive Period Days: [yellow]{sobr.ArchiveTier.ArchivePeriodDays}[/yellow]");
                    ColorConsole.WriteEmbeddedColorLine($"Cost Optimized Enabled: [yellow]{sobr.ArchiveTier.AdvancedSettings.CostOptimizedArchiveEnabled}[/yellow] // Archive Dedupe Enabled: [yellow]{sobr.ArchiveTier.AdvancedSettings.ArchiveDeduplicationEnabled}[/yellow]");
                }

            }

            Console.ReadLine();
        }

        public static string GetPassword()
        {
            string password = "";
            ConsoleKeyInfo info = Console.ReadKey(true);
            while (info.Key != ConsoleKey.Enter)
            {
                if (info.Key != ConsoleKey.Backspace)
                {
                    Console.Write("*");
                    password += info.KeyChar;
                }
                else if (info.Key == ConsoleKey.Backspace)
                {
                    if (!string.IsNullOrEmpty(password))
                    {
                        // remove one character from the list of password characters
                        password = password.Substring(0, password.Length - 1);
                        // get the location of the cursor
                        int pos = Console.CursorLeft;
                        // move the cursor to the left by one character
                        Console.SetCursorPosition(pos - 1, Console.CursorTop);
                        // replace it with space
                        Console.Write(" ");
                        // move the cursor to the left by one character again
                        Console.SetCursorPosition(pos - 1, Console.CursorTop);
                    }
                }
                info = Console.ReadKey(true);
            }

            // add a new line because user pressed enter at the end of their password
            Console.WriteLine();
            return password;
        }

        public static int CalculatePercent(double used, double capacity)
        {
            return (int)Math.Round((double)(100 * used) / capacity);
        }

        public static List<Models.SOBR.ScaleoutRepository> GetSOBRS(RestClient rc)
        {

            var req = new RestRequest("/api/{apiVersionRoute}/backupInfrastructure/scaleoutrepositories", Method.Get);

            req.AddUrlSegment("apiVersionRoute", vbrAPIRouteVersion);
            var content = rc.Execute<Models.SOBR.ScaleoutRepositories>(req);

            return content.Data.Data;
        }

        public static List<Models.RepoState.RepositoryState> GetRepoStates(RestClient rc)
        {

            var req = new RestRequest("/api/{apiVersionRoute}/backupInfrastructure/repositories/states", Method.Get);

            req.AddUrlSegment("apiVersionRoute", vbrAPIRouteVersion);
            var content = rc.Execute<Models.RepoState.RepositoryStates>(req);

            return content.Data.Data;
        }


    }
}
