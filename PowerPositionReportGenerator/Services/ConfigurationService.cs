namespace PowerPositionReportGenerator.Services
{
    public class ConfigurationService : IConfigurationService
    {
        private readonly IConfiguration _configuration;

        public ConfigurationService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Configuration GetConfiguration()
        {
            return new Configuration
            {
                CsvOutputDirectory = _configuration.GetValue<string>("CsvOutputDirectory") ?? throw new Exception("Configuration setting CsvOutputDirectory is not specified"),
                IntervalInMinutes = _configuration.GetValue<int?>("IntervalInMinutes") ?? throw new Exception("Configuration setting IntervalInMinutes is not specified"),
                TimeZoneWindowsId = _configuration.GetValue<string>("TimeZoneWindowsId") ?? "GMT Standard Time"
            };
        }
    }
}
