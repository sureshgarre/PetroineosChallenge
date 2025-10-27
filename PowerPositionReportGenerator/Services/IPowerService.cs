using Services;

public interface IPowerService
{
    Task<IEnumerable<PowerTrade>> GetTradesAsync(DateTime date);
}