using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace SurfTimer;

public partial class SurfTimer
{
    public void OnTick()
    {
        foreach (var player in playerList.Values)
        {
            player.Timer.Tick();
            player.HUD.Display();
            player.Replay.Tick(player);
        }
    }
}