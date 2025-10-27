# PetroineosChallenge
- Service runs an extract immediately on start and then every IntervalMinutes.
- Aggregation uses PowerPeriod period numbers (1..24) where period 1 corresponds to 23:00 previous day.
- CSV filename follows the required convention and contains a header row and 24 rows for each hourly bucket.
