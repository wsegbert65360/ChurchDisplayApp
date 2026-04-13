using System;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Linq;

namespace ChurchDisplayApp.Services
{
    public sealed class StartupProfiler
    {
        private static readonly Lazy<StartupProfiler> _instance = new(() => new StartupProfiler());
        public static StartupProfiler Instance => _instance.Value;
        
        private readonly ConcurrentDictionary<string, PhaseTiming> _phases = new();
        private readonly Stopwatch _total = Stopwatch.StartNew();

        public void StartPhase(string name)
        {
            try
            {
                var timing = new PhaseTiming { StartTicks = _total.ElapsedTicks };
                _phases[name] = timing;
            }
            catch { }
        }

        public void EndPhase(string name)
        {
            try
            {
                if (_phases.TryGetValue(name, out var timing))
                {
                    timing.ElapsedTicks = _total.ElapsedTicks - timing.StartTicks;
                }
            }
            catch { }
        }

        public void DumpSummary()
        {
            try
            {
                _total.Stop();
                Debug.WriteLine("");
                Debug.WriteLine("[StartupProfiler] ===== Startup Timing Summary =====");
                Debug.WriteLine($"[StartupProfiler] Total startup: {_total.ElapsedMilliseconds}ms");
                
                var sortedPhases = _phases.OrderBy(p => p.Value.StartTicks);
                foreach (var phase in sortedPhases)
                {
                    long ms = phase.Value.ElapsedTicks * 1000 / Stopwatch.Frequency;
                    Debug.WriteLine($"[StartupProfiler] {phase.Key}: {ms}ms");
                }
                Debug.WriteLine("");
                
                // Write to console just in case we are looking at dotnet run output
                Console.WriteLine("");
                Console.WriteLine("[StartupProfiler] ===== Startup Timing Summary =====");
                Console.WriteLine($"[StartupProfiler] Total startup: {_total.ElapsedMilliseconds}ms");
                foreach (var phase in sortedPhases)
                {
                    long ms = phase.Value.ElapsedTicks * 1000 / Stopwatch.Frequency;
                    Console.WriteLine($"[StartupProfiler] {phase.Key}: {ms}ms");
                }
                Console.WriteLine("");
            }
            catch { }
        }

        private class PhaseTiming
        {
            public long StartTicks { get; set; }
            public long ElapsedTicks { get; set; }
        }
    }
}
