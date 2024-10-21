using CounterStrikeSharp.API.Modules.Cvars;

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

        // Need to disable maps from executing their cfgs. Currently idk how (But seriusly it a security issue)
        ConVar? bot_quota = ConVar.Find("bot_quota");
        if (bot_quota != null)
        {
            int cbq = bot_quota.GetPrimitiveValue<int>();

            int replaybot_count = 1 +
                                (CurrentMap.ReplayManager.StageWR != null ? 1 : 0) +
                                (CurrentMap.ReplayManager.BonusWR != null ? 1 : 0) +
                                CurrentMap.ReplayManager.CustomReplays.Count;

            if(cbq != replaybot_count)
            {
                bot_quota.SetValue(replaybot_count);
            }
        }

        CurrentMap.ReplayManager.MapWR.Tick();
        CurrentMap.ReplayManager.StageWR?.Tick();
        CurrentMap.ReplayManager.BonusWR?.Tick();

        if (CurrentMap.ReplayManager.StageWR?.RepeatCount == 0)
        {
            CurrentMap.ReplayManager.StageWR.Stage = (CurrentMap.ReplayManager.StageWR.Stage % CurrentMap.Stages) + 1;
            CurrentMap.ReplayManager.StageWR.LoadReplayData(repeat_count: 3);
            //CurrentMap.ReplayManager.StageWR.FormatBotName();
            AddTimer(1.5f, () => CurrentMap.ReplayManager.StageWR.FormatBotName());
            // CurrentMap.ReplayManager.StageWR.ResetReplay();
            // CurrentMap.ReplayManager.StageWR.RepeatCount = 3;
        }

        if (CurrentMap.ReplayManager.BonusWR?.RepeatCount == 0)
        {
            CurrentMap.ReplayManager.BonusWR.Stage = (CurrentMap.ReplayManager.BonusWR.Stage % CurrentMap.Bonuses) + 1;
            CurrentMap.ReplayManager.BonusWR.LoadReplayData(repeat_count: 3);

            AddTimer(1.5f, () => CurrentMap.ReplayManager.BonusWR.FormatBotName());

            // CurrentMap.ReplayManager.BonusWR.ResetReplay();
            // //CurrentMap.ReplayManager.BonusWR.FormatBotName();
            // CurrentMap.ReplayManager.BonusWR.RepeatCount = 3;
        }

        for(int i = 0; i < CurrentMap.ReplayManager.CustomReplays.Count; i++)
        {
            if (CurrentMap.ReplayManager.CustomReplays[i].MapID != CurrentMap.ID)
                CurrentMap.ReplayManager.CustomReplays[i].MapID = CurrentMap.ID;

            CurrentMap.ReplayManager.CustomReplays[i].Tick();
            if (CurrentMap.ReplayManager.CustomReplays[i].RepeatCount == 0)
            {
                CurrentMap.KickReplayBot(i);                
            }
        }

        // for(int i = 0; i < CurrentMap!.ReplayBots.Count; i++)
        // {
        //     if (CurrentMap.ReplayBots[i].MapID != CurrentMap.ID)
        //         CurrentMap.ReplayBots[i].MapID = CurrentMap.ID;

        //     CurrentMap.ReplayBots[i].Tick();
        //     if (CurrentMap.ReplayBots[i].RepeatCount == 0)
        //     {
        //         int m = 1 + (CurrentMap.Stages > 0 ? 1 : 0) + (CurrentMap.Bonuses > 0 ? 1 :0);
                
        //         if(i == CurrentMap.ReplayBots.Count - 1)
        //             continue;

        //         if (i < CurrentMap.ReplayBots.Count - m)
        //         {
        //             CurrentMap.KickReplayBot(i);
        //             continue;
        //         }

        //         if (CurrentMap.ReplayBots[i].Type == 1)
        //             CurrentMap.ReplayBots[i].Stage = (CurrentMap.ReplayBots[i].Stage + 1) % CurrentMap.Bonuses;
        //         else if (CurrentMap.ReplayBots[i].Type == 2)
        //             CurrentMap.ReplayBots[i].Stage = (CurrentMap.ReplayBots[i].Stage + 1) % CurrentMap.Stages;

        //         CurrentMap.ReplayBots[i].LoadReplayData(DB!);
        //         CurrentMap.ReplayBots[i].ResetReplay();
        //         CurrentMap.ReplayBots[i].RepeatCount = 3;
        //     }
        // }
    }
}