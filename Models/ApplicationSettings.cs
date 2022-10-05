using Newtonsoft.Json;
namespace veeam_repository_reporter.Models
{
    public partial class VBRSettings
    {
        [JsonProperty("Host")]
        public string Host { get; set; }

        [JsonProperty("Port")]
        public long Port { get; set; }

        [JsonProperty("APIVersion")]
        public string ApiVersion { get; set; }

        [JsonProperty("APIRouteVersion")]
        public string ApiRouteVersion { get; set; }

        [JsonProperty("Username")]
        public string Username { get; set; }

        [JsonProperty("Password")]
        public string Password { get; set; }
    }
}
