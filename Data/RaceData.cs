using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace RCLayoutPreview.Data
{
    public class RaceData
    {
        public GenericData GenericData { get; set; }
        public List<Racer> Racers { get; set; }
    }

    public class GenericData
    {
        public string RaceName { get; set; }
        public string EventName { get; set; }
        public string TrackName { get; set; }
        public string NextHeatNumber { get; set; }
        public string TotalHeats { get; set; }
        public string HeatDuration { get; set; }
        public string Weather { get; set; }
        public string RaceFormat { get; set; }
    }

    public class Racer
    {
        public int Lane { get; set; }
        public string Name { get; set; }
        public int Position { get; set; }
        public int Lap { get; set; }
        public string BestLapTime { get; set; }
        public string GapLeader { get; set; }
        public string CarModel { get; set; }
        public string TireChoice { get; set; }
        public int PitStops { get; set; }
        public int FuelPercent { get; set; }
        public string ReactionTime { get; set; }
        public string Avatar { get; set; }
        public string LaneColor { get; set; }
        public bool IsLeader { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JToken> Extras { get; set; }
    }
}