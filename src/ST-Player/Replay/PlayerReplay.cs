using MySqlConnector;
using System.Text.Json;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text;
using System.IO.Compression;

namespace SurfTimer;

internal class PlayerReplay {
    // Status
    public bool IsRecording { get; set; } = false;
    public bool IsPlaying { get; set; } = false;
    public bool IsPlayingPaused { get; set; } = false;

    // Tracking
    public List<ReplayFrame> Frames { get; set; } = new List<ReplayFrame>();
    public int FramesCount { get; set; } = 0; // Replacing Frames.Count() for more efficient Replay playing

    // Playing
    public int CurrentFrameTick { get; set; } = 0;
    public int FrameTickIncrement { get; set; } = 1;

    public void Reset() {
        this.IsRecording = false;
        this.IsPlaying = false;
        this.IsPlayingPaused = false;

        this.Frames.Clear();
        this.FramesCount = 0;

        this.CurrentFrameTick = 0;
        this.FrameTickIncrement = 1;
    }

    public void StartRecording() {
        if (!this.IsPlaying)
            this.Reset();
            this.IsRecording = true;
    }

    public void StopRecording() {
        this.IsRecording = false;
    }

    public void Pause() {
        this.IsPlayingPaused = true;
    }

    public void Resume() {
        this.IsPlayingPaused = false;
    }

    public void StartPlaying(Player player, TimerDatabase DB) {
        this.Reset();
        player.Controller.PlayerPawn.Value!.MoveType = CounterStrikeSharp.API.Core.MoveType_t.MOVETYPE_NOCLIP;
        this.LoadReplayData(player, DB);

        this.IsPlaying = true;
    }

    public void StopPlaying(Player player) {
        this.IsPlaying = false;
        player.Controller.PlayerPawn.Value!.MoveType = CounterStrikeSharp.API.Core.MoveType_t.MOVETYPE_WALK;
    }

    public void Tick(Player player) {
        if (!this.IsRecording && !this.IsPlaying) {
            return;
        }

        if (this.IsRecording) {
                // Disabeling Recording if times disabled
                if (!player.Timer.IsEnabled) {
                    this.StopRecording();
                    return;
                }

                var player_pos = player.Controller.PlayerPawn.Value!.AbsOrigin!;
                var player_angle = player.Controller.PlayerPawn.Value!.EyeAngles!;
                var player_velocity = player.Controller.PlayerPawn.Value!.AbsVelocity!;
                var player_button = player.Controller.PlayerPawn.Value!.MovementServices!.Buttons.ButtonStates[0];
                var player_flags = player.Controller.PlayerPawn.Value!.Flags;
                var player_move_type = player.Controller.PlayerPawn.Value!.MoveType;

                var frame = new ReplayFrame {
                    Pos = new Vector(player_pos.X, player_pos.Y, player_pos.Z),
                    Ang = new QAngle(player_angle.X, player_angle.Y, player_angle.Z),
                    Vel = new Vector(player_velocity.X, player_velocity.Y, player_velocity.Z),
                    Button = player_button,
                    Flags = player_flags,
                    MoveType = player_move_type,
                };

                this.Frames.Add(frame);
                this.FramesCount++;
        }
        else if (this.IsPlaying) {
            // Checking for replay ending
            if(this.CurrentFrameTick >= this.FramesCount) {
                this.StopPlaying(player);
                return;
            }

            ReplayFrame current_frame = this.Frames[this.CurrentFrameTick];
            player.Controller.PlayerPawn.Value!.Teleport(current_frame.Pos, current_frame.Ang, current_frame.Vel);

            if (!this.IsPlayingPaused) {
                // Boundry check for reverse replay (FrameTickIncrement < 0)
                this.CurrentFrameTick = Math.Max(0, this.CurrentFrameTick + this.FrameTickIncrement);
            }
        }
    }

    /// <summary>
    /// [ player_id | maptime_id | replay_frames ]
    /// @ Adding a replay data for a run (PB/WR)
    /// @ Data saved can be accessed with `LoadReplayData`
    /// </summary>
    public void SaveReplayData(Player player, TimerDatabase DB) {
        JsonSerializerOptions options = new JsonSerializerOptions {
            WriteIndented = false,
            Converters = { new VectorConverter(), new QAngleConverter() }
        };
        string replay_frames = JsonSerializer.Serialize(Frames, options);
        string compressed_replay_frames = Compressor.Compress(replay_frames);
        Task<int> updatePlayerReplayTask = DB.Write($"INSERT INTO `MapTimeReplay` " +
                                                        $"(`player_id`, `maptime_id`, `replay_frames`) " +
                                                        $"VALUES ({player.Profile.ID}, {player.Stats.PB[0].ID}, '{compressed_replay_frames}')");
        if (updatePlayerReplayTask.Result <= 0)
            throw new Exception($"CS2 Surf ERROR >> internal class PlayerReplay -> Save -> Failed to insert/update player run in database. Player: {player.Profile.Name} ({player.Profile.SteamID})");
        updatePlayerReplayTask.Dispose();
    }

    /// <summary>
    /// Retrieving a PlayerReplay (PB/WR)
    /// @ Data retrieved was saved by `SaveReplayData`
    /// </summary>
    public void LoadReplayData(Player player, TimerDatabase DB, int maptime_id = 0) {
        // TODO: make query for wr too
        Task<MySqlDataReader> dbTask = DB.Query($"SELECT `replay_frames` FROM MapTimeReplay " +
                                                    $"WHERE `player_id`={player.Profile.ID} OR `maptime_id`={maptime_id}");
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
                this.FramesCount = this.Frames.Count();
            }
        }
    }
}