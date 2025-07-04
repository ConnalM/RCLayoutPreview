using System.Collections.Generic;
using System.Linq;
using RCLayoutPreview; // Or wherever your Racer class lives

namespace RCLayoutPreview.Helpers
{
    public static class RacerHelpers
    {
        public static Racer GetRacerByQualifier(string qualifier, int index, List<Racer> racers)
        {
            if (racers == null || index < 1) return null;

            return qualifier switch
            {
                "Lane" => racers.FirstOrDefault(r => r.Lane == index),
                "Position" => racers.OrderBy(r => r.Position).Skip(index - 1).FirstOrDefault(),
                "RaceLeader" => racers.OrderBy(r => r.IsLeader ? 0 : 1).Skip(index - 1).FirstOrDefault(),
                "GroupLeader" => racers.Skip(index - 1).FirstOrDefault(),
                "TeamLeader" => racers.Skip(index - 1).FirstOrDefault(),
                _ => null
            };
        }
    }
}