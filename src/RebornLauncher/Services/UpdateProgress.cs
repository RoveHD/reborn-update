namespace PokemonRebornLauncher.Services;

public enum UpdateStage
{
    Checking,
    Downloading,
    Extracting,
    Installing,
    Complete
}

public sealed record UpdateProgress(
    UpdateStage Stage,
    double? Percent,
    long BytesReceived,
    long? TotalBytes,
    double BytesPerSecond,
    string Message);
