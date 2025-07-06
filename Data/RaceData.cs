using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace RCLayoutPreview.Data
{
    public class RaceData
    {
        // Original properties for backward compatibility
        public GenericData GenericData { get; set; }
        public List<Racer> Racers { get; set; }
        
        // New properties to handle the flattened JSON structure
       