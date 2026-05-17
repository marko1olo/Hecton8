namespace Hecton8.Vehicles.Automation
{
    public enum DockingFailureReason : byte
    {
        None = 0,
        ObstacleBlocked = 1,
        InvalidRequest = 2,
        LostHub = 3
    }
}
