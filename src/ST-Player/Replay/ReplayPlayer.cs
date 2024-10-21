using System.Dynamic;
using System.Text;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;
using MySqlConnector;

namespace SurfTimer;

internal class ReplayPlayer
{
    public bool IsPlaying { get; set; } = false;
    public bool IsPaused { get; set; } = false;
    public bool IsPlayable { get; set; } = false;

    // Tracking for replay counting
    public int RepeatCount { get; set; } = -1;

    public int MapID { get; set; } = -1;
    public int MapTimeID { get; set; } = -1;
    public int Type { get; set; } = -1;
    public int Stage { get; set; } = -1;

    public int RecordRank { get; set; } = -1; // This is used to determine whether replay is for wr or for pb
    public string RecordPlayerName { get; set; } = "N/A";
    public int RecordRunTime { get; set; } = 0;
    public int ReplayCurrentRunTime { get; set; } = 0;
    public bool IsReplayOutsideZone { get; set; } = false;

    // Tracking
    public List<ReplayFrame> Frames { get; set; } = new List<ReplayFrame>();

    // Playing
    public int CurrentFrameTick { get; set; } = 0;
    public int FrameTickIncrement { get; set; } = 1;

    public CCSPlayerController? Controller { get; set; }

    public void ResetReplay() 
    {
        this.CurrentFrameTick = 0;
        this.FrameTickIncrement = 1;
        if(this.RepeatCount > 0)
            this.RepeatCount--;

        this.IsReplayOutsideZone = false;
        this.ReplayCurrentRunTime = 0;
    }

    public void Reset() 
    {
        this.IsPlaying = false;
        this.IsPaused = false;
        this.IsPlayable = false;
        this.RepeatCount = -1;

        this.Frames.Clear();

        this.ResetReplay();

        this.Controller = null;
    }

    public void SetController(CCSPlayerController c, int repeat_count = -1)
    {
        this.Controller = c;
        if (repeat_count != -1)
            this.RepeatCount = repeat_count;
        this.IsPlayable = true;
    }

    public void Start() 
    {
        if (!this.IsPlayable)
            return;

        this.IsPlaying = true;
    }

    public void Stop() 
    {
        this.IsPlaying = false;
    }

    public void Pause() 
    {
        if (!this.IsPlaying)
            return;

        this.IsPaused = !this.IsPaused;
        this.IsReplayOutsideZone = !this.IsReplayOutsideZone;
    }

    public void Tick() 
    {
        if (!this.IsPlaying || !this.IsPlayable || this.Frames.Count == 0)
            return;

        ReplayFrame current_frame = this.Frames[this.CurrentFrameTick];

        // SOME BLASHPEMY FOR YOU
        if (this.FrameTickIncrement >= 0)
        {
            if (current_frame.Situation == (byte)ReplayFrameSituation.START_ZONE_EXIT)
            {
                this.IsReplayOutsideZone = true;
                this.ReplayCurrentRunTime = 0;
            }
            else if (current_frame.Situation == (byte)ReplayFrameSituation.END_ZONE_ENTER)
            {
                this.IsReplayOutsideZone = false;
            }
        }
        else
        {
            if (current_frame.Situation == (byte)ReplayFrameSituation.START_ZONE_EXIT)
            {
                this.IsReplayOutsideZone = false;
            }
            else if (current_frame.Situation == (byte)ReplayFrameSituation.END_ZONE_ENTER)
            {
                this.IsReplayOutsideZone = true;
                this.ReplayCurrentRunTime = this.CurrentFrameTick - (64*2); // (64*2) counts for the 2 seconds before run actually starts
            }
        }
        // END OF BLASPHEMY

        var current_pos = this.Controller!.PlayerPawn.Value!.AbsOrigin!;
        var current_frame_pos = current_frame.GetPos();
        var current_frame_ang = current_frame.GetAng();

        bool is_on_ground = (current_frame.Flags & (uint)PlayerFlags.FL_ONGROUND) != 0;

        Vector velocity = (current_frame_pos - current_pos) * 64;

        if (is_on_ground)
            this.Controller.PlayerPawn.Value.MoveType = MoveType_t.MOVETYPE_WALK;
        else
            this.Controller.PlayerPawn.Value.MoveType = MoveType_t.MOVETYPE_NOCLIP;

        if ((current_pos - current_frame_pos).Length() > 200)
                this.Controller.PlayerPawn.Value.Teleport(current_frame_pos, current_frame_ang, new Vector(nint.Zero));
            else
                this.Controller.PlayerPawn.Value.Teleport(new Vector(nint.Zero), current_frame_ang, velocity);
                

        if (!this.IsPaused)
        {
            this.CurrentFrameTick = Math.Max(0, this.CurrentFrameTick + this.FrameTickIncrement);
            if (this.IsReplayOutsideZone)
                this.ReplayCurrentRunTime = Math.Max(0, this.ReplayCurrentRunTime + this.FrameTickIncrement);
        }

        if(this.CurrentFrameTick >= this.Frames.Count) 
            this.ResetReplay();
            if(RepeatCount != -1)
                System.Console.WriteLine($"CS2 Surf DEBUG >> internal class PlayerReplay -> ====================> {this.RepeatCount} <====================");
    }

    public async void LoadReplayData(int style = 0, int repeat_count = -1) 
    {
        if (!this.IsPlayable)
            return;

        API_MapTime? maptime = null;
        if (this.Type == 0)
            maptime = await APICall.GET<API_MapTime>($"/surftimer/getmaprunbyrank?map_id={this.MapID}&style={style}&rank={this.RecordRank}");
        else if (this.Type == 1)
            maptime = await APICall.GET<API_MapTime>($"/surftimer/getbonusrunbyrank?map_id={this.MapID}&style={style}&rank={this.RecordRank}&stage={this.Stage}");
        else if (this.Type == 2)
            maptime = await APICall.GET<API_MapTime>($"/surftimer/getstagerunbyrank?map_id={this.MapID}&style={style}&rank={this.RecordRank}&stage={this.Stage}");
        
        if (maptime == null)
        {
            Console.WriteLine($"CS2 Surf DEBUG >> internal class PlayerReplay -> Load -> No replay data found for Player.");
            return;
        }


        JsonSerializerOptions options = new JsonSerializerOptions {WriteIndented = false, Converters = { new VectorConverter(), new QAngleConverter() }};

        string json = Compressor.Decompress(maptime.replay_frames);
        this.Frames.Clear();
        this.Frames = JsonSerializer.Deserialize<List<ReplayFrame>>(json, options)!;

        this.MapTimeID = maptime.id;
        this.RecordRunTime = maptime.run_time;
        this.RecordPlayerName = maptime.name;

        System.Console.WriteLine($"CS2 Surf DEBUG >> internal class PlayerReplay -> Load -> Loaded replay data for Player. MapTime ID: {maptime.id}");        
        this.ResetReplay();
        this.RepeatCount = repeat_count;
    }

    public void FormatBotName()
    {
        if (!this.IsPlayable)
            return;

        string prefix;
        if (this.RecordRank == 1) {
            prefix = "WR";
        } else {
            prefix = $"Rank #{this.RecordRank}";
        }

        if (this.Type == 1)
            prefix = prefix + $" B{this.Stage}";
        else if (this.Type == 2)
            prefix = prefix + $" S{this.Stage}";

        SchemaString<CBasePlayerController> bot_name = new SchemaString<CBasePlayerController>(this.Controller!, "m_iszPlayerName");

        string replay_name = $"[{prefix}] {this.RecordPlayerName} | {PlayerHUD.FormatTime(this.RecordRunTime)}";
        if(this.RecordRunTime <= 0)
            replay_name = $"[{prefix}] {this.RecordPlayerName}";

        bot_name.Set(replay_name);
        Utilities.SetStateChanged(this.Controller!, "CBasePlayerController", "m_iszPlayerName");
    }
}