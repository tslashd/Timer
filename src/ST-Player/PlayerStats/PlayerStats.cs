using MySqlConnector;

namespace SurfTimer;

internal class PlayerStats
{
    // To-Do: Each stat should be a class of its own, with its own methods and properties - easier to work with. 
    //        Temporarily, we store ticks + basic info so we can experiment
    // These account for future style support and a relevant index.
    public int[,] StagePB { get; set; } = { { 0, 0 } }; // First dimension: style (0 = normal), second dimension: stage index
    public int[,] StageRank { get; set; } = { { 0, 0 } }; // First dimension: style (0 = normal), second dimension: stage index
    //

    public Dictionary<int, PersonalBest> PB { get; set; } = new Dictionary<int, PersonalBest>();
    public CurrentRun ThisRun { get; set; } = new CurrentRun(); // This is a CurrenntRun object that tracks the data for the Player's current run
    // Initialize PersonalBest for each `style` (e.g., 0 for normal) - this is a temporary solution
    // Here we can loop through all available styles at some point and initialize them
    public PlayerStats()
    {
        PB[0] = new PersonalBest();
        // Add more styles as needed
    }

    /// <summary>
    /// Loads the player's MapTimes data from the database along with `Rank` for the run.
    /// `Checkpoints` are loaded separately because inside the while loop we cannot run queries.
    /// This can populate all the `style` stats the player has for the map - currently only 1 style is supported
    /// </summary>
    public void LoadMapTimesData(Player player, TimerDatabase DB, int playerId = 0, int mapId = 0)
    {
        Task<MySqlDataReader> dbTask2 = DB.Query($"SELECT mainquery.*, (SELECT COUNT(*) FROM `MapTimes` AS subquery " +
                                                 $"WHERE subquery.`map_id` = mainquery.`map_id` AND subquery.`style` = mainquery.`style` " +
                                                 $"AND subquery.`run_time` <= mainquery.`run_time`) AS `rank` FROM `MapTimes` AS mainquery " +
                                                 $"WHERE mainquery.`player_id` = {player.Profile.ID} AND mainquery.`map_id` = {player.CurrMap.ID}; ");
        MySqlDataReader playerStats = dbTask2.Result;
        int style = 0; // To-do: implement styles
        if (!playerStats.HasRows)
        {
            Console.WriteLine($"CS2 Surf DEBUG >> internal class PlayerStats -> LoadMapTimesData -> No MapTimes data found for Player.");
        }
        else
        {
            while (playerStats.Read())
            {
                // Load data into PersonalBest object
                // style = playerStats.GetInt32("style"); // Uncomment when style is implemented
                PB[style].ID = playerStats.GetInt32("id");
                PB[style].StartVelX = (float)playerStats.GetDouble("start_vel_x");
                PB[style].StartVelY = (float)playerStats.GetDouble("start_vel_y");
                PB[style].StartVelZ = (float)playerStats.GetDouble("start_vel_z");
                PB[style].EndVelX = (float)playerStats.GetDouble("end_vel_x");
                PB[style].EndVelY = (float)playerStats.GetDouble("end_vel_y");
                PB[style].EndVelZ = (float)playerStats.GetDouble("end_vel_z");
                PB[style].Ticks = playerStats.GetInt32("run_time");
                PB[style].RunDate = playerStats.GetInt32("run_date");
                PB[style].Rank = playerStats.GetInt32("rank");

                Console.WriteLine($"============== CS2 Surf DEBUG >> LoadMapTimesData -> PlayerID: {player.Profile.ID} | Rank: {PB[style].Rank} | ID: {PB[style].ID} | RunTime: {PB[style].Ticks} | SVX: {PB[style].StartVelX} | SVY: {PB[style].StartVelY} | SVZ: {PB[style].StartVelZ} | EVX: {PB[style].EndVelX} | EVY: {PB[style].EndVelY} | EVZ: {PB[style].EndVelZ} | Run Date (UNIX): {PB[style].RunDate}");
                #if DEBUG
                Console.WriteLine($"CS2 Surf DEBUG >> internal class PlayerStats -> LoadMapTimesData -> PlayerStats.PB (ID {PB[style].ID}) loaded from DB.");
                #endif
            }
        }
        playerStats.Close();
    }
}