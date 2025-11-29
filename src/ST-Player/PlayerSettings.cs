namespace SurfTimer;

public class PlayerSettings
{
    public TimeFormatStyle TimeFormat { get; set; } = TimeFormatStyle.Compact;
    public VelocityFormatStyle VelocityFormat { get; set; } = VelocityFormatStyle.XY;

    public string TimerColor { get; set; } = "#4FC3F7";
    public string TimerColorPractice { get; set; } = "#BA68C8";
    public string TimerColorActive { get; set; } = "#43A047";
    public string RankColorPb { get; set; } = "#7986CB";
    public string RankColorWr { get; set; } = "#FFD700";
    public string SpectatorColor { get; set; } = "#9E9E9E";

    /// <summary>
    /// Different types of time formatting for chat and HUD
    /// </summary>
    public enum TimeFormatStyle
    {
        Compact,
        Full,
        Verbose,
    }

    /// <summary>
    /// Different types of velocity formatting for chat and HUD
    /// </summary>
    public enum VelocityFormatStyle
    {
        XY,
        XYZ,
    }
}
