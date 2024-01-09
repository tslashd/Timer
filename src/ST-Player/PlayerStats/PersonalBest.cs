namespace SurfTimer;

// To-do: make Style (currently 0) be dynamic
// To-do: add `Type`
internal class PersonalBest
{
    public int ID { get; set; } = -1; // Exclude from constructor, retrieve from Database when loading/saving
    public int Ticks { get; set; }
    public int Rank { get; set; } = -1; // Exclude from constructor, retrieve from Database when loading/saving
    public Dictionary<int, Checkpoint> Checkpoint { get; set; }
    // public int Type { get; set; }
    public float StartVelX { get; set; }
    public float StartVelY { get; set; }
    public float StartVelZ { get; set; }
    public float EndVelX { get; set; }
    public float EndVelY { get; set; }
    public float EndVelZ { get; set; }
    public int RunDate { get; set; }
    // Add other properties as needed

    // Constructor
    public PersonalBest()
    {
        Ticks = -1; // To-do: what type of value we use here? DB uses DECIMAL but `.Tick` is int???
        Checkpoint = new Dictionary<int, Checkpoint>();
        // Type = type;
        StartVelX = -1.0f;
        StartVelY = -1.0f;
        StartVelZ = -1.0f;
        EndVelX = -1.0f;
        EndVelY = -1.0f;
        EndVelZ = -1.0f;
        RunDate = 0;
    }

    /// <summary>
    /// Saves the player's run to the database and reloads the data for the player.
    /// NOTE: Not re-loading any data at this point as we need `LoadMapTimesData` to be called from here as well, otherwise we may not have the `this.ID` populated
    /// </summary>
    public void SaveMapTime(Player player, TimerDatabase DB, int mapId = 0)
    {
        // Add entry in DB for the run
        // To-do: add `type`
        Task<int> updatePlayerRunTask = DB.Write($"INSERT INTO `MapTimes` " +
                                                    $"(`player_id`, `map_id`, `style`, `type`, `stage`, `run_time`, `start_vel_x`, `start_vel_y`, `start_vel_z`, `end_vel_x`, `end_vel_y`, `end_vel_z`, `run_date`) " +
                                                    $"VALUES ({player.Profile.ID}, {player.CurrMap.ID}, 0, 0, 0, {this.Ticks}, " +
                                                    $"{player.Stats.ThisRun.StartVelX}, {player.Stats.ThisRun.StartVelY}, {player.Stats.ThisRun.StartVelZ}, {player.Stats.ThisRun.EndVelX}, {player.Stats.ThisRun.EndVelY}, {player.Stats.ThisRun.EndVelZ}, {(int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()}) " +
                                                    $"ON DUPLICATE KEY UPDATE run_time=VALUES(run_time), start_vel_x=VALUES(start_vel_x), start_vel_y=VALUES(start_vel_y), " +
                                                    $"start_vel_z=VALUES(start_vel_z), end_vel_x=VALUES(end_vel_x), end_vel_y=VALUES(end_vel_y), end_vel_z=VALUES(end_vel_z), run_date=VALUES(run_date);");
        if (updatePlayerRunTask.Result <= 0)
            throw new Exception($"CS2 Surf ERROR >> internal class PersonalBest -> SaveMapTime -> Failed to insert/update player run in database. Player: {player.Profile.Name} ({player.Profile.SteamID})");
        updatePlayerRunTask.Dispose();

        // Will have to LoadMapTimesData right here as well to get the ID of the run we just inserted
        // this.SaveCurrentRunCheckpoints(player, DB); // Save checkpoints for this run
        // this.LoadCheckpointsForRun(DB); // Re-Load checkpoints for this run
    }
}