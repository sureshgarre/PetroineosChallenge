using Services;

// Wrapper for PowerService is created in order to be able to mock in tests
public class PowerServiceWrapper : IPowerService
{
    private readonly PowerService _powerService;

    public PowerServiceWrapper()
    {
        _powerService = new PowerService();
    }

    public Task<IEnumerable<PowerTrade>> GetTradesAsync(DateTime date)
    {
        return _powerService.GetTradesAsync(date);
    }
}