using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System;

namespace SurfTimer;

public class ReplayRecorder
{
    private readonly ILogger<ReplayRecorder> _logger;

    internal ReplayRecorder()
    {
        // Resolve the logger instance from the DI container
        _logger = SurfTimer.ServiceProvider.GetRequiredService<ILogger<ReplayRecorder>>();
    }

    public bool IsRecording { get; set; } = false;
    public bool IsSaving { get; set; } = false;
    public ReplayFrameSituation CurrentSituation { get; set; } = ReplayFrameSituation.NONE;

    // Live recording frames for the ongoing run
    public List<ReplayFrame> Frames { get; set; } = new List<ReplayFrame>();

    // Tail snapshot buffer (single new property requested)
    // Holds frames from the run plus up to 1s of tail frames after end zone touch.
    public List<ReplayFrame> FramesToSave { get; private set; } = new List<ReplayFrame>();

    // Internal tail state
    private DateTimeOffset? _tailCaptureUntil;

    public List<int> StageEnterSituations { get; set; } = new List<int>();
    public List<int> StageExitSituations { get; set; } = new List<int>();
    public List<int> CheckpointEnterSituations { get; set; } = new List<int>();
    public List<int> CheckpointExitSituations { get; set; } = new List<int>();
    /// <summary>
    /// Indexes should always follow this pattern: START_ZONE_ENTER > START_ZONE_EXIT > END_ZONE_ENTER > END_ZONE_EXIT
    /// Where END_ZONE_EXIT is not guaranteed
    /// </summary>
    public List<int> MapSituations { get; set; } = new List<int>();
    /// <summary>
    /// Indexes should always follow this pattern: START_ZONE_ENTER > START_ZONE_EXIT > END_ZONE_ENTER > END_ZONE_EXIT
    /// Where END_ZONE_EXIT is not guaranteed
    /// </summary>
    public List<int> BonusSituations { get; set; } = new List<int>();

    internal void Reset([CallerMemberName] string methodName = "")
    {
        this.IsRecording = false;
        this.Frames.Clear();
        this.StageEnterSituations.Clear();
        this.StageExitSituations.Clear();
        this.CheckpointEnterSituations.Clear();
        this.CheckpointExitSituations.Clear();
        this.MapSituations.Clear();
        this.BonusSituations.Clear();

        // Do NOT clear FramesToSave if a tail capture is still in progress.
        if (_tailCaptureUntil == null || DateTimeOffset.UtcNow > _tailCaptureUntil.Value)
        {
            FramesToSave.Clear();
            _tailCaptureUntil = null;
        }

#if DEBUG
        _logger.LogDebug("[{ClassName}] {MethodName} -> Reset (Frames cleared, FramesToSave={SaveCount}, tailActive={TailActive})",
            nameof(ReplayRecorder), methodName, FramesToSave.Count, _tailCaptureUntil != null
        );
#endif
    }

    internal void Start([CallerMemberName] string methodName = "")
    {
        this.IsRecording = true;

#if DEBUG
        _logger.LogDebug("[{ClassName}] {MethodName} -> Started recording", 
            nameof(ReplayRecorder), methodName
        );
#endif
    }

    internal void Stop([CallerMemberName] string methodName = "")
    {
        this.IsRecording = false;

#if DEBUG
        _logger.LogDebug("[{ClassName}] {MethodName} -> Stopped recording", 
            nameof(ReplayRecorder), methodName
        );
#endif
    }

    /// <summary>
    /// Begin tail capture: snapshot current Frames, keep adding new frames for 'seconds'.
    /// If already capturing, overwrite.
    /// </summary>
    internal void BeginSaveTail(double seconds = 1.0, [CallerMemberName] string methodName = "")
    {
        FramesToSave.Clear();
        FramesToSave.AddRange(Frames);
        _tailCaptureUntil = DateTimeOffset.UtcNow.AddSeconds(seconds);
#if DEBUG
        _logger.LogDebug("[{ClassName}] {MethodName} -> BeginSaveTail seconds={Seconds}, snapshot={Snapshot}", 
            nameof(ReplayRecorder), methodName, seconds, FramesToSave.Count
        );
#endif
    }

    /// <summary>
    /// Explicitly end tail capture (optional; normally ends when timer elapses and save runs).
    /// </summary>
    internal void EndSaveTail([CallerMemberName] string methodName = "")
    {
        _tailCaptureUntil = null;
#if DEBUG
        _logger.LogDebug("[{ClassName}] {MethodName} -> EndSaveTail (FramesToSave={SaveCount})", 
            nameof(ReplayRecorder), methodName, FramesToSave.Count
        );
#endif
    }

    internal void Tick(Player player, [CallerMemberName] string methodName = "")
    {
        if (!this.IsRecording || player == null)
            return;

        if (!player.Timer.IsEnabled && !player.ReplayRecorder.IsSaving)
        {
            this.Stop();
            this.Reset();
            _logger.LogTrace("[{ClassName}] {MethodName} -> Disabled - stopped & reset for {Name}",
                nameof(ReplayRecorder), methodName, player.Profile.Name
            );
            return;
        }

        var player_pos = player.Controller.Pawn.Value!.AbsOrigin!;
        var player_angle = player.Controller.PlayerPawn.Value!.EyeAngles;
        var player_flags = player.Controller.Pawn.Value.Flags;

        var frame = new ReplayFrame
        {
            pos = [player_pos.X, player_pos.Y, player_pos.Z],
            ang = [player_angle.X, player_angle.Y, player_angle.Z],
            Situation = this.CurrentSituation,
            Flags = player_flags,
        };

        this.Frames.Add(frame);

        // If tail capture still active, append this frame also to FramesToSave
        if (_tailCaptureUntil != null && DateTimeOffset.UtcNow <= _tailCaptureUntil.Value)
        {
            FramesToSave.Add(frame);
        }
        else if (_tailCaptureUntil != null && DateTimeOffset.UtcNow > _tailCaptureUntil.Value)
        {
            // Tail window elapsed; mark end so TrimReplay will use FramesToSave
            _tailCaptureUntil = null;
        }

        // One-tick situation semantics
        this.CurrentSituation = ReplayFrameSituation.NONE;
    }

    internal string TrimReplay(Player player, short type = 0, bool lastStage = false, [CallerMemberName] string methodName = "")
    {
        this.IsSaving = true;

        // Always prefer FramesToSave for replay saving and fall back to Frames if empty
        List<ReplayFrame> source = new();
        if (FramesToSave.Count > 0)
        {
            source = FramesToSave;
#if DEBUG
            _logger.LogTrace(">>> [{ClassName}] {MethodName} -> Using `FramesToSave` with a count of {Count}",
                nameof(ReplayRecorder), methodName, FramesToSave.Count
            );
#endif
        }
        else if (Frames.Count > 0)
        {
            source = Frames;
#if DEBUG
            _logger.LogTrace(">>> [{ClassName}] {MethodName} -> Using `Frames` with a count of {Count}",
                nameof(ReplayRecorder), methodName, Frames.Count
            );
#endif
        }

        if (source.Count == 0)
        {
            _logger.LogError("[{ClassName}] {MethodName} -> No frames to trim for {Name}",
                nameof(ReplayRecorder), methodName, player.Profile.Name
            );
            this.IsSaving = false;
            return Compressor.Compress(JsonSerializer.Serialize(new List<ReplayFrame>()));
        }

        _logger.LogTrace(">>> [{ClassName}] {MethodName} -> TrimReplay '{Player}' type={Type} lastStage={LastStage} sourceFrames={Count} (tailUsed={TailUsed})",
            nameof(ReplayRecorder), methodName, player.Profile.Name, type, lastStage, source.Count, source == FramesToSave
        );

        List<ReplayFrame>? trimmed_frames = type switch
        {
            0 => TrimMapRun(player, source),
            1 => TrimBonusRun(player, source),
            2 => TrimStageRun(player, source, lastStage),
            _ => new List<ReplayFrame>()
        };

        this.IsSaving = false;
        _logger.LogTrace("[{ClassName}] {MethodName} -> Trimmed frames count = {Trimmed}", 
            nameof(ReplayRecorder), methodName, trimmed_frames?.Count
        );

        var trimmedJson = JsonSerializer.Serialize(trimmed_frames ?? new List<ReplayFrame>());
        return Compressor.Compress(trimmedJson);
    }

    internal List<ReplayFrame>? TrimMapRun(Player player, List<ReplayFrame> frames, [CallerMemberName] string methodName = "")
    {
        List<ReplayFrame> new_frames = new();

        var start_enter_index = frames.FindLastIndex(f => f.Situation == ReplayFrameSituation.START_ZONE_ENTER);
        var start_exit_index = frames.FindLastIndex(f => f.Situation == ReplayFrameSituation.START_ZONE_EXIT);
        var end_enter_index = frames.FindLastIndex(f => f.Situation == ReplayFrameSituation.END_ZONE_ENTER);

        _logger.LogInformation("[{ClassName}] {MethodName} -> Map trim indexes: startEnter={SE} startExit={SX} endEnter={EE}",
            nameof(ReplayRecorder), methodName, start_enter_index, start_exit_index, end_enter_index);

        if (start_enter_index == -1)
        {
            _logger.LogError("[{ClassName}] {MethodName} -> start_enter_index -1 for '{Name}' (StageMode={StageMode} BonusMode={BonusMode})",
                nameof(ReplayRecorder), methodName, player.Profile.Name, player.Timer.IsStageMode, player.Timer.IsBonusMode);
            start_enter_index = 0;
        }

        if (start_enter_index != -1 && start_exit_index != -1 && end_enter_index != -1)
        {
            int startIndex = CalculateStartIndex(start_enter_index, start_exit_index, Config.ReplaysPre);
            int endIndex = CalculateEndIndex(end_enter_index, frames.Count, Config.ReplaysPre);
            new_frames = frames.GetRange(startIndex, endIndex - startIndex + 1);

#if DEBUG
            _logger.LogDebug("<<< [{ClassName}] {MethodName} -> Map trimmed {Start}->{End} new={NewCount} total={Total}",
                nameof(ReplayRecorder), methodName, startIndex, endIndex, new_frames.Count, frames.Count
            );
#endif
        }
        return new_frames;
    }

    internal List<ReplayFrame>? TrimBonusRun(Player player, List<ReplayFrame> frames, [CallerMemberName] string methodName = "")
    {
        List<ReplayFrame> new_frames = new();

        var bonus_enter_index = frames.FindLastIndex(f => f.Situation == ReplayFrameSituation.START_ZONE_ENTER);
        var bonus_exit_index = frames.FindLastIndex(f => f.Situation == ReplayFrameSituation.START_ZONE_EXIT);
        var bonus_end_enter_index = frames.FindLastIndex(f => f.Situation == ReplayFrameSituation.END_ZONE_ENTER);

        _logger.LogInformation("[{ClassName}] {MethodName} -> Bonus trim indexes: enter={Enter} exit={Exit} end={End}",
            nameof(ReplayRecorder), methodName, bonus_enter_index, bonus_exit_index, bonus_end_enter_index
        );

        if (bonus_enter_index == -1)
        {
            _logger.LogError("[{ClassName}] {MethodName} -> bonus_enter_index -1 for '{Name}' bonus={Bonus}",
                nameof(ReplayRecorder), methodName, player.Profile.Name, player.Timer.Bonus
            );
            bonus_enter_index = 0;
        }

        if (bonus_enter_index != -1 && bonus_exit_index != -1 && bonus_end_enter_index != -1)
        {
            int startIndex = CalculateStartIndex(bonus_enter_index, bonus_exit_index, Config.ReplaysPre);
            int endIndex = CalculateEndIndex(bonus_end_enter_index, frames.Count, Config.ReplaysPre);
            new_frames = frames.GetRange(startIndex, endIndex - startIndex + 1);

#if DEBUG
            _logger.LogDebug("<<< [{ClassName}] {MethodName} -> Bonus trimmed {Start}->{End} new={NewCount} total={Total}",
                nameof(ReplayRecorder), methodName, startIndex, endIndex, new_frames.Count, frames.Count
            );
#endif
        }
        return new_frames;
    }

    internal List<ReplayFrame>? TrimStageRun(Player player, List<ReplayFrame> frames, bool lastStage = false, [CallerMemberName] string methodName = "")
    {
        List<ReplayFrame> new_frames = new();

        int stage = player.Timer.Stage - 1;
        ReplayFrameSituation enterZone;
        ReplayFrameSituation exitZone;
        ReplayFrameSituation endZone;

        // Select the correct enums for trimming
        if (stage == 1 || stage == -1)
        {
            _logger.LogDebug("Stage replay trimming will use START_ZONE_*");
            enterZone = ReplayFrameSituation.START_ZONE_ENTER;
            exitZone = ReplayFrameSituation.START_ZONE_EXIT;
            endZone = ReplayFrameSituation.STAGE_ZONE_ENTER;
        }
        else
        {
            _logger.LogDebug("Stage replay trimming will use STAGE_ZONE_*");
            enterZone = ReplayFrameSituation.STAGE_ZONE_ENTER;
            exitZone = ReplayFrameSituation.STAGE_ZONE_EXIT;
            endZone = ReplayFrameSituation.STAGE_ZONE_ENTER;

            // If it's the last stage we need to use END_ZONE_ENTER for trimming
            if (lastStage)
            {
                _logger.LogDebug("This is the last stage, will end on END_ZONE_ENTER.");
                endZone = ReplayFrameSituation.END_ZONE_ENTER;
                stage += 1;
            }
        }

        _logger.LogInformation("[{ClassName}] {MethodName} -> Stage trim: logicalStage={Stage} currentPlayerStage={PlayerStage} lastStage={Last}",
            nameof(ReplayRecorder), methodName, stage, player.Timer.Stage, lastStage
        );

        int stage_end_index = frames.FindLastIndex(f => f.Situation == endZone);
        int stage_exit_index = frames.FindLastIndex(stage_end_index - 1, f => f.Situation == exitZone);
        int stage_enter_index = frames.FindLastIndex(stage_end_index - 1, f => f.Situation == enterZone); 
        stage_enter_index = stage_enter_index == -1 ? frames.FindLastIndex(f => f.Situation == enterZone) : stage_enter_index; // Use frame 0 if -1 is detected

        _logger.LogInformation("[{ClassName}] {MethodName} -> Stage indexes: enter={Enter} exit={Exit} end={End}",
            nameof(ReplayRecorder), methodName, stage_enter_index, stage_exit_index, stage_end_index
        );

        if (stage_enter_index == -1 || stage_exit_index == -1 || stage_end_index == -1)
        {
            _logger.LogError("[{ClassName}] {MethodName} -> Missing indexes for stage {Stage} replay '{Name}'",
                nameof(ReplayRecorder), methodName, stage, player.Profile.Name
            );
            return new_frames;
        }

        int startIndex = CalculateStartIndex(stage_enter_index, stage_exit_index, Config.ReplaysPre);
        int endIndex = CalculateEndIndex(stage_end_index, frames.Count, Config.ReplaysPre);
        new_frames = frames.GetRange(startIndex, endIndex - startIndex + 1);

#if DEBUG
        _logger.LogInformation("<<< [{ClassName}] {MethodName} -> Stage trimmed {Start}->{End}, frames={Count}",
            nameof(ReplayRecorder), methodName, startIndex, endIndex, new_frames.Count
        );
#endif
        return new_frames;
    }

    private static int CalculateStartIndex(int start_enter, int start_exit, int buffer)
    {
        if (start_exit - (buffer * 2) >= start_enter)
            return start_exit - (buffer * 2);
        else if (start_exit - buffer >= start_enter)
            return start_exit - buffer;
        else if (start_exit - (buffer / 2) >= start_enter)
            return start_exit - (buffer / 2);
        else
            return start_enter;
    }

    private static int CalculateEndIndex(int end_enter, int totalFrames, int buffer)
    {
        if (end_enter + (buffer * 2) < totalFrames)
            return end_enter + (buffer * 2);
        else if (end_enter + buffer < totalFrames)
            return end_enter + buffer;
        else if (end_enter + (buffer / 2) < totalFrames)
            return end_enter + (buffer / 2);
        else
            return end_enter;
    }
}