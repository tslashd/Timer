using MySqlConnector;

namespace SurfTimer;

internal class Checkpoint : PersonalBest
{
    public int CP { get; set; }
    public float EndTouch { get; set; }
    public int Attempts { get; set; }

    public Checkpoint(int cp, int ticks, float startVelX, float startVelY, float startVelZ, float endVelX, float endVelY, float endVelZ, float endTouch, int attempts)
    {
        CP = cp;
        Ticks = ticks; // To-do: this was supposed to be the ticks but that is used for run_time for HUD????
        StartVelX = startVelX;
        StartVelY = startVelY;
        StartVelZ = startVelZ;
        EndVelX = endVelX;
        EndVelY = endVelY;
        EndVelZ = endVelZ;
        EndTouch = endTouch;
        Attempts = attempts;
    }

    /// <summary>
    /// Executes the DB query to get all the checkpoints and store them in the Checkpoint dictionary
    /// </summary>
    public void LoadCheckpointsForRun(TimerDatabase DB)
    {
        Task<MySqlDataReader> dbTask = DB.Query($"SELECT * FROM `Checkpoints` WHERE `maptime_id` = {this.ID};");
        MySqlDataReader results = dbTask.Result;
        if (this == null)
        {
            #if DEBUG
            Console.WriteLine("CS2 Surf ERROR >> internal class Checkpoint : PersonalBest -> LoadCheckpointsForRun -> PersonalBest object is null.");
            #endif

            results.Close();
            return;
        }

        if (this.Checkpoint == null)
        {
            #if DEBUG
            Console.WriteLine($"CS2 Surf DEBUG >> internal class Checkpoint : PersonalBest -> LoadCheckpointsForRun -> Checkpoints list is not initialized.");
            #endif

            this.Checkpoint = new Dictionary<int, Checkpoint>(); // Initialize if null
        }

        #if DEBUG
        Console.WriteLine($"this.Checkpoint.Count {this.Checkpoint.Count} ");
        Console.WriteLine($"this.ID {this.ID} ");
        Console.WriteLine($"this.Ticks {this.Ticks} ");
        Console.WriteLine($"this.RunDate {this.RunDate} ");
        #endif

        if (!results.HasRows)
        {
            #if DEBUG
            Console.WriteLine($"CS2 Surf DEBUG >> internal class Checkpoint : PersonalBest -> LoadCheckpointsForRun -> No checkpoints found for this mapTimeId {this.ID}.");
            #endif

            results.Close();
            return;
        }

        #if DEBUG
        Console.WriteLine($"======== CS2 Surf DEBUG >> internal class Checkpoint : PersonalBest -> LoadCheckpointsForRun -> Checkpoints found for this mapTimeId");
        #endif

        while (results.Read())
        {
            #if DEBUG
            Console.WriteLine($"cp {results.GetInt32("cp")} ");
            Console.WriteLine($"run_time {results.GetFloat("run_time")} ");
            Console.WriteLine($"sVelX {results.GetFloat("start_vel_x")} ");
            Console.WriteLine($"sVelY {results.GetFloat("start_vel_y")} ");
            #endif

            Checkpoint cp = new(results.GetInt32("cp"),
                                results.GetInt32("run_time"),   // To-do: what type of value we use here? DB uses DECIMAL but `.Tick` is int???
                                results.GetFloat("start_vel_x"),
                                results.GetFloat("start_vel_y"),
                                results.GetFloat("start_vel_z"),
                                results.GetFloat("end_vel_x"),
                                results.GetFloat("end_vel_y"),
                                results.GetFloat("end_vel_z"),
                                results.GetFloat("end_touch"),
                                results.GetInt32("attempts"));
            cp.ID = results.GetInt32("cp");
            // To-do: cp.ID = calculate Rank # from DB

            Checkpoint[cp.CP] = cp;

            #if DEBUG
            Console.WriteLine($"======= CS2 Surf DEBUG >> internal class Checkpoint : PersonalBest -> LoadCheckpointsForRun -> Loaded CP {cp.CP} with RunTime {cp.Ticks}.");
            #endif
        }
        results.Close();

        #if DEBUG
        Console.WriteLine($"======= CS2 Surf DEBUG >> internal class Checkpoint : PersonalBest -> LoadCheckpointsForRun -> Checkpoints loaded from DB. Count: {Checkpoint.Count}");
        #endif
    }
}