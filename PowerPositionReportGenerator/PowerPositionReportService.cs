namespace PowerPositionReportGenerator
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using PowerPositionReportGenerator.Services;
    using System.Globalization;
    using System.Text;

    public class PowerPositionReportService : BackgroundService
    {
        private readonly Configuration _configuration;
        private readonly TimeZoneInfo _timeZone;

        private readonly IPowerService _powerService;
        private readonly ILogger<PowerPositionReportService> _logger;

        public PowerPositionReportService(IPowerService powerService, IConfigurationService configurationService, ILogger<PowerPositionReportService> logger)
        {
            _powerService = powerService;
            _configuration = configurationService.GetConfiguration();
            _logger = logger;

            _timeZone = GetLocalTimeZone();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("PowerPositionReportService starting. CSV directory: {dir}, IntervalInMinutes: {int}", _configuration.CsvOutputDirectory, _configuration.IntervalInMinutes);
                
                CreateOutputDirectory();
                QueueRun();

                await Task.Delay(TimeSpan.FromMinutes(_configuration.IntervalInMinutes), stoppingToken);
            }
        }

        private void CreateOutputDirectory()
        {
            try
            {
                if (!Directory.Exists(_configuration.CsvOutputDirectory))
                {
                    Directory.CreateDirectory(_configuration.CsvOutputDirectory);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create CSV output directory: {dir}", _configuration.CsvOutputDirectory);
                throw;
            }
        }

        private void QueueRun()
        {
            // Start a background task that does the extract; do NOT skip if previous still running
            Task.Run(async () =>
            {
                var runId = Guid.NewGuid();
                _logger.LogInformation("Starting extract (id={runId}) at {time}", runId, DateTimeOffset.Now);
                try
                {
                    await ExecuteExtract(runId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Extract (id={runId}) failed", runId);
                }
                _logger.LogInformation("Completed extract (id={runId}) at {time}", runId, DateTimeOffset.Now);
            });
        }

        // Explicitly made this method public in order to unit test this method
        public async Task ExecuteExtract(Guid runId)
        {
            var nowUtc = DateTime.UtcNow;
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, _timeZone);

            // The API wants the date to retrieve power position for. For example, to retrieve the position for 2025-10-27 logical day,
            // call GetTrades(date: 2025-10-27). Period 1 in the returned PowerPeriods corresponds to 23:00 of 2025-10-26.

            var logicalDayDate = nowLocal.Date; // date component in London local time

            _logger.LogInformation("Run {runId}: extracting power position for {logicalDay} (local now {localNow})", runId, logicalDayDate.ToString("yyyy-MM-dd"), nowLocal);

            var trades = await _powerService.GetTradesAsync(logicalDayDate);

            // Aggregate volumes per hour local time (the PowerPeriod period numbers start at 1 mapping to 23:00 previous day)
            // We'll build 24 buckets representing hours from 23:00 (previous day) to 22:00 (logical day)
            var buckets = new double[24];

            foreach (var trade in trades)
            {
                if (trade?.Periods == null) continue;
                foreach (var period in trade.Periods)
                {
                    int periodNumber = period.Period; // expected 1..24
                    if (periodNumber < 1 || periodNumber > 24) continue;
                    // period 1 -> hour 23:00 previous day
                    int index = periodNumber - 1; // 0..23
                    buckets[index] += period.Volume;
                }
            }

            // Build CSV rows in local time HH:MM where period 1 maps to local day start 23:00 previous day.
            // So row 0 -> time = logicalDayDate.AddDays(-1).AddHours(23)
            var startOfPeriod = logicalDayDate.AddDays(-1).AddHours(23);

            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine("Local Time,Volume");

            for (int i = 0; i < 24; i++)
            {
                var hourTime = startOfPeriod.AddHours(i);
                var display = hourTime.ToString("HH:mm", CultureInfo.InvariantCulture);
                
                var volume = buckets[i].ToString(CultureInfo.InvariantCulture);
                csvBuilder.AppendLine($"{display},{volume}");
            }

            var extractLocalTime = nowLocal;
            var filename = $"PowerPosition_{extractLocalTime:yyyyMMdd}_{extractLocalTime:HHmm}.csv";
            var filepath = Path.Combine(_configuration.CsvOutputDirectory, filename);

            try
            {
                await File.WriteAllTextAsync(filepath, csvBuilder.ToString(), CancellationToken.None);
                _logger.LogInformation("Extract written to {file}", filepath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write CSV to {file}", filepath);
                throw;
            }
        }

        private TimeZoneInfo GetLocalTimeZone()
        {
            TimeZoneInfo timeZone = null;
            var timeZoneId = _configuration.TimeZoneWindowsId;
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (Exception ex)
            {
                // fallback to Europe/London if available
                try { timeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London"); } catch { timeZone = TimeZoneInfo.Local; }
                _logger.LogWarning(ex, "Could not find timezone '{tzId}', falling back to {tz}.", timeZoneId, timeZone.Id);
            }

            return timeZone;
        }
    }
}
