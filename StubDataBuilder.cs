using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using RCLayoutPreview; // Adjust if your actual model namespace differs
using RCLayoutPreview.Helpers;
using System.Windows.Media;
using RCLayoutPreview.Helpers;
using static RCLayoutPreview.Helpers.RacerHelpers;

namespace RCLayoutPreview.Data
{
    public static class StubDataBuilder
    {
        public static RaceData GenerateFromLayout(FrameworkElement layout)
        {
            var raceData = new RaceData
            {
                GenericData = new GenericData(),
                Racers = new List<Racer>()
            };

            var racerPool = new Dictionary<string, Racer>(); // key: QualifierType + index

            foreach (var element in GetAllNamedElements(layout))
            {
                string tagOrName = element.Tag as string ?? element.Name;
                if (!FieldNameParser.TryParse(tagOrName, out var parsed)) continue;

                // Use the full parsed.BaseName (e.g., "NextHeatNickname1") as the field name
                string field = parsed.BaseName;

                if (parsed.IsGeneric)
                {
                    InjectGeneric(raceData.GenericData, field);
                }
                else
                {
                    string key = $"{parsed.QualifierType}_{parsed.QualifierIndex ?? 1}";
                    if (!racerPool.TryGetValue(key, out var racer))
                    {
                        racer = new Racer
                        {
                            Extras = new Dictionary<string, JToken>()
                        };

                        if (parsed.QualifierType == "Lane")
                            racer.Lane = parsed.QualifierIndex ?? 1;

                        if (parsed.QualifierType == "Position")
                            racer.Position = parsed.QualifierIndex ?? 1;

                        racerPool[key] = racer;
                    }

                    // Always inject the full field name (e.g., "NextHeatNickname1")
                    InjectRacer(racer, field);
                    Console.WriteLine($"Injecting field: {field}");
                }
            }

            raceData.Racers = racerPool.Values.ToList();
            return raceData;
        }

        public static void PrintFieldOriginSummary(RaceData data, RaceData layoutStub)
        {
            Console.WriteLine("🧾 Field Origin Summary:");

            foreach (var layoutRacer in layoutStub.Racers)
            {
                var actual = GetRacerByQualifier("Lane", layoutRacer.Lane, data.Racers);
                if (actual == null) continue;

                Console.WriteLine($"Racer Lane {layoutRacer.Lane}:");

                foreach (var kvp in layoutRacer.Extras)
                {
                    string origin = (actual.Extras != null && actual.Extras.ContainsKey(kvp.Key)) ? "🟢 From stub" : "🟠 Synthesized";
                    Console.WriteLine($"  {origin}  {kvp.Key}");
                }
            }

            var genericProps = layoutStub.GenericData.GetType().GetProperties();
            foreach (var prop in genericProps)
            {
                var expected = prop.GetValue(layoutStub.GenericData);
                var actual = data.GenericData?.GetType().GetProperty(prop.Name)?.GetValue(data.GenericData);
                string origin = actual != null ? "🟢 From stub" : "🟠 Synthesized";
                Console.WriteLine($"{origin}  Generic: {prop.Name}");
            }
        }

        public static void PatchMissingFields(RaceData target, RaceData patch)
        {
            foreach (var patchRacer in patch.Racers)
            {
                var targetRacer = GetRacerByQualifier("Lane", patchRacer.Lane, target.Racers);
                if (targetRacer == null) continue;

                targetRacer.Extras ??= new Dictionary<string, JToken>();
                foreach (var kvp in patchRacer.Extras)
                {
                    if (!targetRacer.Extras.ContainsKey(kvp.Key))
                        targetRacer.Extras[kvp.Key] = kvp.Value;
                }
            }

            foreach (var prop in patch.GenericData.GetType().GetProperties())
            {
                var value = prop.GetValue(patch.GenericData);
                var targetProp = target.GenericData.GetType().GetProperty(prop.Name);
                if (targetProp?.GetValue(target.GenericData) == null)
                    targetProp?.SetValue(target.GenericData, value);
            }
        }

        public static void DumpAllRacerExtras(RaceData raceData)
        {
            foreach (var racer in raceData.Racers)
            {
                Console.WriteLine($"Racer Lane: {racer.Lane}");
                if (racer.Extras != null && racer.Extras.Count > 0)
                {
                    foreach (var kvp in racer.Extras)
                    {
                        Console.WriteLine($"  Extras[{kvp.Key}] = {kvp.Value}");
                    }
                }
                else
                {
                    Console.WriteLine("  No extras found.");
                }
            }
        }

        private static void InjectGeneric(GenericData generic, string field)
        {
            var prop = generic.GetType().GetProperty(field);
            if (prop != null)
            {
                prop.SetValue(generic, $"Sample_{field}");
            }
        }

        private static void InjectRacer(Racer racer, string field)
        {
            var prop = racer.GetType().GetProperty(field);
            if (prop != null)
            {
                prop.SetValue(racer, $"R_{field}");
            }
            racer.Extras ??= new Dictionary<string, JToken>();
            racer.Extras[field] = $"R_{field}";

            // Add this line to verify injection at runtime
            if (field == "NextHeatNickname1")
                Console.WriteLine($"✅ Injected NextHeatNickname1 into Racer.Lane={racer.Lane}: {racer.Extras[field]}");
        }

        private static IEnumerable<FrameworkElement> GetAllNamedElements(FrameworkElement root)
        {
            var queue = new Queue<FrameworkElement>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!string.IsNullOrWhiteSpace(current.Name) || current.Tag is string)
                    yield return current;

                int count = VisualTreeHelper.GetChildrenCount(current);
                for (int i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(current, i) as FrameworkElement;
                    if (child != null)
                        queue.Enqueue(child);
                }
            }
        }
    }
}