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
            player.ReplayRecorder.Tick(player);
            player.HUD.Display();
        }

        if (CurrentMap == null)
            return;

        for(int i = 0; i < CurrentMap!.ReplayBots.Count; i++)
        {
            CurrentMap.ReplayBots[i].Tick();
            if (CurrentMap.ReplayBots[i].RepeatCount == 0)
                CurrentMap.KickReplayBot(i);
        }
    }
}