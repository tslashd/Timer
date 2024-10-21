using System.Text.Json;
using CounterStrikeSharp.API.Modules.Utils;

namespace SurfTimer;

internal class ReplayRecorder
{
    public bool IsRecording { get; set; } = false;
    public ReplayFrameSituation CurrentSituation { get; set; } = ReplayFrameSituation.NONE;
    public List<ReplayFrame> Frames { get; set; } = new List<ReplayFrame>();

    public void Reset() 
    {
        this.IsRecording = false;
        this.Frames.Clear();
    }

    public void Start() 
    {
        this.IsRecording = true;
    }

    public void Stop() 
    {
        this.IsRecording = false;
    }

    public void Tick(Player player) 
    {
        if (!this.IsRecording || player == null)
            return;

        // Disabeling Recording if timer disabled
        if (!player.Timer.IsEnabled) 
        {
            this.Stop();
            this.Reset();
            return;
        }

        var player_pos = player.Controller.Pawn.Value!.AbsOrigin!;
        var player_angle = player.Controller.PlayerPawn.Value!.EyeAngles;
        var player_button = player.Controller.Pawn.Value.MovementServices!.Buttons.ButtonStates[0];
        var player_flags = player.Controller.Pawn.Value.Flags;
        var player_move_type = player.Controller.Pawn.Value.MoveType;

        var frame = new ReplayFrame 
        {
            pos = [player_pos.X, player_pos.Y, player_pos.Z],
            ang = [player_angle.X, player_angle.Y, player_angle.Z],
            Situation = (byte)this.CurrentSituation,
            Flags = player_flags,
        };

        this.Frames.Add(frame);

        // Every Situation should last for at most, 1 tick
        this.CurrentSituation = ReplayFrameSituation.NONE;
    }

    public string SerializeReplay()
    {
        // JsonSerializerOptions options = new JsonSerializerOptions {WriteIndented = false, Converters = { new VectorConverter(), new QAngleConverter() }};
        // string replay_frames = JsonSerializer.Serialize(Frames, options);
        string replay_frames = JsonSerializer.Serialize(Frames);
        return Compressor.Compress(replay_frames);
    }

    public string SerializeReplayPortion(int start_idx, int end_idx)
    {
        // JsonSerializerOptions options = new JsonSerializerOptions {WriteIndented = false, Converters = { new VectorConverter(), new QAngleConverter() }};
        // string replay_frames = JsonSerializer.Serialize(Frames.GetRange(start_idx, end_idx), options);
        string replay_frames = JsonSerializer.Serialize(Frames.GetRange(start_idx, end_idx));
        return Compressor.Compress(replay_frames);
    }

    public void SetLastTickSituation(ReplayFrameSituation situation)
    {
        if (this.Frames.Count == 0)
            return;
        this.Frames[this.Frames.Count-2].Situation = (byte)situation;
    }

    public int LastEnterTick(int start_idx=0)
    {
        if (start_idx == 0)
            start_idx = this.Frames.Count - 1;
        for (int i = start_idx; i > 0; i--)
        {
            if (
                this.Frames[i].Situation == (byte)ReplayFrameSituation.START_ZONE_ENTER ||
                this.Frames[i].Situation == (byte)ReplayFrameSituation.STAGE_ZONE_ENTER ||
                this.Frames[i].Situation == (byte)ReplayFrameSituation.CHECKPOINT_ZONE_ENTER ||
                this.Frames[i].Situation == (byte)ReplayFrameSituation.END_ZONE_ENTER
            )
                return i;
        }
        return 0;
    }

    public int LastExitTick(int start_idx=0)
    {
        if (start_idx == 0)
            start_idx = this.Frames.Count - 1;
        for (int i = start_idx; i > 0; i--)
        {
            if (
                this.Frames[i].Situation == (byte)ReplayFrameSituation.START_ZONE_EXIT ||
                this.Frames[i].Situation == (byte)ReplayFrameSituation.STAGE_ZONE_EXIT ||
                this.Frames[i].Situation == (byte)ReplayFrameSituation.CHECKPOINT_ZONE_EXIT ||
                this.Frames[i].Situation == (byte)ReplayFrameSituation.END_ZONE_EXIT
            )
                return i;
        }
        return 0;
    }

    // public int CalculateTicksSinceLastEnterStage()
    // {
    //     int start_stage_mark = -1;
    //     for (int i = this.Frames.Count-1; i > 0; i--)
    //     {
    //         if (start_stage_mark != -1 && start_stage_mark - i > 64*2)
    //             return i;

    //         if (this.Frames[i].Situation == (byte)ReplayFrameSituation.START_STAGE)
    //             start_stage_mark = i;
                
    //         if (this.Frames[i].Situation == (byte)ReplayFrameSituation.ENTER_STAGE)
    //             return i; // Fact check me
    //     }
    //     return 0;
    // }

    // public int CalculateTicksSinceLastStartStage()
    // {
    //     for (int i = this.Frames.Count-1; i > 0; i--)
    //     {
    //         if (this.Frames[i].Situation == (byte)ReplayFrameSituation.START_STAGE)
    //             return i;
    //     }
    //     return 0;
    // }
}