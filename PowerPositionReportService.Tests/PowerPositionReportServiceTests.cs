using Microsoft.Extensions.Logging;
using Moq;
using PowerPositionReportGenerator;
using PowerPositionReportGenerator.Services;
using Services;

namespace PowerPositionWindowsService.Tests
{
    [TestClass]
    public class PowerPositionReportServiceTests
    {
        private Mock<IConfigurationService> _configurationServiceMock;
        private Mock<IPowerService> _powerServiceMock;
        private Mock<ILogger<PowerPositionReportService>> _loggerMock;

        private string _tempDirectory;

        [TestInitialize]
        public void Setup()
        {
            _configurationServiceMock = new Mock<IConfigurationService>();
            _powerServiceMock = new Mock<IPowerService>();
            _loggerMock = new Mock<ILogger<PowerPositionReportService>>();

            _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDirectory);

            var configuration = new Configuration
            {
                CsvOutputDirectory = _tempDirectory,
                IntervalInMinutes = 15,
                TimeZoneWindowsId = "GMT Standard Time"
            };

            _configurationServiceMock.Setup(x => x.GetConfiguration()).Returns(configuration);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDirectory)) Directory.Delete(_tempDirectory, true);
        }

        [TestMethod]
        public async Task ExecuteExtract_ShouldGenerateCsvWithCorrectAggregatedVolumes()
        {
            // Arrange
            var testDate = new DateTime(2025, 10, 27);

            var volume = 1;
            var trade1 = PowerTrade.Create(testDate, 2);
            foreach(var period in trade1.Periods)
            {
                period.Volume = volume;
                volume++;
            }

            var trade2 = PowerTrade.Create(testDate, 2);
            foreach (var period in trade2.Periods)
            {
                period.Volume = volume;
                volume++;
            }

            var trades = new List<PowerTrade> { trade1, trade2 };

            _powerServiceMock.Setup(p => p.GetTradesAsync(It.IsAny<DateTime>()))
                             .ReturnsAsync(trades);

            var service = new PowerPositionReportService(
                _powerServiceMock.Object,
                _configurationServiceMock.Object,
                _loggerMock.Object);

            // Act
            await service.ExecuteExtract(Guid.NewGuid());

            // Assert
            var csvFile = Directory.GetFiles(_tempDirectory, "PowerPosition_*.csv").SingleOrDefault();
            Assert.IsNotNull(csvFile, "CSV file should be created");

            var lines = await File.ReadAllLinesAsync(csvFile);
            Assert.AreEqual(25, lines.Length, "CSV should contain 1 header + 24 data rows");
            Assert.AreEqual("Local Time,Volume", lines[0]);

            var data = lines.Skip(1).Select((l, i) => new { i, l }).ToList();
            Assert.IsTrue(data.Any(d => d.l.Contains(",4")), "Aggregated volume for period 1 should be 4");
            Assert.IsTrue(data.Any(d => d.l.Contains(",6")), "Volume for period 2 should be 6");
        }
    }
}