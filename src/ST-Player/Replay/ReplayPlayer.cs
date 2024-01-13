using System.Text;
using System.Text.Json;
using CounterStrikeSharp.API.Core;
using MySqlConnector;

namespace SurfTimer;

internal class ReplayPlayer
{
    public bool IsPlaying { get; set; } = false;
    public bool IsPaused { get; set; } = false;

    // Tracking
    public List<ReplayFrame> Frames { get; set; } = new List<ReplayFrame>();

    // Playing
    public int CurrentFrameTick { get; set; } = 0;
    public int FrameTickIncrement { get; set; } = 1;

    public CCSPlayerController? Controller { get; set; }

    public void ResetReplay() {
        this.CurrentFrameTick = 0;
        this.FrameTickIncrement = 1;
    }

    public void Reset() {
        this.IsPlaying = false;
        this.IsPaused = false;

        this.Frames.Clear();

        this.ResetReplay();

        this.Controller = null;
    }

    public void Start() {
        this.IsPlaying = true;
    }

    public void Stop() {
        this.IsPlaying = false;
    }

    public void Pause() {
        if (this.IsPlaying)
            this.IsPaused = !this.IsPaused;
    }

    public void Tick() {
        if (!this.IsPlaying || this.Controller == null)
            return;

        if(this.CurrentFrameTick >= this.Frames.Count) {
            this.Stop();
            this.ResetReplay();
        }

        ReplayFrame current_frame = this.Frames[this.CurrentFrameTick];
        this.Controller.PlayerPawn.Value!.Teleport(current_frame.Pos, current_frame.Ang, current_frame.Vel);

        if (!this.IsPaused)
            this.CurrentFrameTick = Math.Max(0, this.CurrentFrameTick + this.FrameTickIncrement);
    }

        public void LoadReplayData(TimerDatabase DB, int map_id, int maptime_id = 0) {
        // TODO: make query for wr too
        Task<MySqlDataReader> dbTask = DB.Query($"SELECT `replay_frames` FROM MapTimeReplay " +
                                                    $"WHERE `map_id`={map_id} AND `maptime_id`={maptime_id} ");
        MySqlDataReader mapTimeReplay = dbTask.Result;
        if(!mapTimeReplay.HasRows) {
            Console.WriteLine($"CS2 Surf DEBUG >> internal class PlayerReplay -> Load -> No replay data found for Player.");
        }
        else {
            JsonSerializerOptions options = new JsonSerializerOptions {
                WriteIndented = false,
                Converters = { new VectorConverter(), new QAngleConverter() }
            };
            while(mapTimeReplay.Read()) {
                string json = Compressor.Decompress(Encoding.UTF8.GetString((byte[])mapTimeReplay[0]));
                this.Frames = JsonSerializer.Deserialize<List<ReplayFrame>>(json, options)!;
            }
        }
        mapTimeReplay.Close();
        dbTask.Dispose();
    }
}