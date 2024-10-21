using CounterStrikeSharp.API.Core;

namespace SurfTimer;

internal class ReplayManager
{
    public ReplayPlayer MapWR { get; set; }
    public ReplayPlayer? BonusWR { get; set; } = null;
    public ReplayPlayer? StageWR { get; set; } = null;
    public List<ReplayPlayer> CustomReplays { get; set; }

    public ReplayManager(int map_id, bool staged, bool bonused)
    {
        MapWR = new ReplayPlayer
        {
            Type = 0,
            Stage = 0,
            RecordRank = 1,
            MapID = map_id
        };

        if (staged)
        {
            StageWR = new ReplayPlayer
            {
                Type = 2,
                Stage = 1,
                RecordRank = 1,
                MapID = map_id
            };
        }

        if (bonused)
        {
            BonusWR = new ReplayPlayer
            {
                Type = 1,
                Stage = 1,
                RecordRank = 1,
                MapID = map_id
            };
        }

        CustomReplays = new List<ReplayPlayer>();
    }

    public bool IsControllerConnectedToReplayPlayer(CCSPlayerController controller)
    {
        if (this.MapWR.Controller?.Equals(controller) == true)
            return true;

        if (this.StageWR?.Controller?.Equals(controller) == true)

        if (this.BonusWR?.Controller?.Equals(controller) == true)
            return true;

        foreach (var replay in this.CustomReplays)
        {
            if (replay.Controller?.Equals(controller) == true)
                return true;
        }

        return false;
    }
}