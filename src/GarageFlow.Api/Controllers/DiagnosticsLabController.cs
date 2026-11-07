using System.Diagnostics;
using GarageFlow.Api.Security;
using GarageFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Api.Controllers;

/// <summary>
/// A practice lab for production debugging tools (PerfMon, dotnet-counters, WinDbg, Fiddler, Wireshark).
/// Each endpoint creates ONE kind of problem on purpose: a memory leak, a CPU spike, thread-pool starvation,
/// a slow SQL query or an exception storm, so you can watch it in the tools and then find the cause.
///
/// Safety: every endpoint answers 404 unless BOTH are true: the app runs in Development AND
/// DiagnosticsLab:Enabled is true. Only an Admin may call it. It never ships switched on.
/// </summary>
[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/lab")]
public class DiagnosticsLabController(
    IConfiguration configuration,
    IWebHostEnvironment environment,
    GarageFlowDbContext db,
    ILogger<DiagnosticsLabController> logger) : ControllerBase
{
    // The "leak": a static list lives as long as the process, so the garbage collector can never free what is in it.
    // This is the classic real-world leak: a static cache or event handler that only ever grows.
    private static readonly List<LeakyCacheEntry> LeakyCache = new();
    private static readonly object LeakLock = new();

    private bool LabOff => !environment.IsDevelopment() || !configuration.GetValue<bool>("DiagnosticsLab:Enabled");

    /// <summary>What the process looks like right now: memory, GC counts and thread-pool threads.</summary>
    [HttpGet("status")]
    public ActionResult<object> Status()
    {
        if (LabOff) return NotFound();

        int leakedEntries;
        lock (LeakLock) leakedEntries = LeakyCache.Count;

        ThreadPool.GetAvailableThreads(out var availableWorkers, out _);
        ThreadPool.GetMaxThreads(out var maxWorkers, out _);
        using var process = Process.GetCurrentProcess();

        return Ok(new
        {
            processId = Environment.ProcessId,
            leakedEntries,
            leakedMegabytes = leakedEntries * LeakyCacheEntry.PayloadKilobytes / 1024.0,
            managedHeapMegabytes = Math.Round(GC.GetTotalMemory(forceFullCollection: false) / 1024.0 / 1024.0, 1),
            workingSetMegabytes = Math.Round(process.WorkingSet64 / 1024.0 / 1024.0, 1),
            gcCollections = new { gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2) },
            threadPool = new { threads = ThreadPool.ThreadCount, busyWorkers = maxWorkers - availableWorkers, queued = ThreadPool.PendingWorkItemCount }
        });
    }

    /// <summary>LAB 1, memory leak: every call adds entries (1 MB each) to a static list that is never cleared.</summary>
    [HttpPost("leak")]
    public ActionResult<object> Leak([FromQuery] int entries = 10)
    {
        if (LabOff) return NotFound();
        entries = Math.Clamp(entries, 1, 200);

        int total;
        lock (LeakLock)
        {
            for (var i = 0; i < entries; i++)
                LeakyCache.Add(new LeakyCacheEntry($"customer-report-{LeakyCache.Count}"));
            total = LeakyCache.Count;
        }

        logger.LogWarning("Lab: leaked {Entries} more entries, {Total} in total", entries, total);
        return Ok(new { added = entries, totalEntries = total, approxMegabytes = total * LeakyCacheEntry.PayloadKilobytes / 1024 });
    }

    /// <summary>The fix for LAB 1: empty the list, then let the garbage collector take the memory back.</summary>
    [HttpDelete("leak")]
    public ActionResult<object> FreeLeak()
    {
        if (LabOff) return NotFound();
        lock (LeakLock) LeakyCache.Clear();
        GC.Collect(); // only ever call this in a lab: in real code the runtime decides when to collect
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return Ok(new { cleared = true });
    }

    /// <summary>LAB 2, CPU spike: burns one core with prime counting for the given time.</summary>
    [HttpGet("cpu")]
    public ActionResult<object> Cpu([FromQuery] int milliseconds = 3000)
    {
        if (LabOff) return NotFound();
        milliseconds = Math.Clamp(milliseconds, 100, 15000);

        var stopwatch = Stopwatch.StartNew();
        long primes = 0;
        for (long n = 2; stopwatch.ElapsedMilliseconds < milliseconds; n++)
            if (IsPrime(n)) primes++;

        return Ok(new { milliseconds = stopwatch.ElapsedMilliseconds, primesFound = primes });
    }

    /// <summary>
    /// LAB 3a, the WRONG way to wait: sync-over-async. .Wait() parks a thread-pool thread for the whole delay.
    /// Fire 100 of these at once and the pool runs out of threads: even /health becomes slow (thread-pool starvation).
    /// </summary>
    [HttpGet("blocking")]
    public ActionResult<object> Blocking([FromQuery] int milliseconds = 2000)
    {
        if (LabOff) return NotFound();
        milliseconds = Math.Clamp(milliseconds, 100, 10000);

        var stopwatch = Stopwatch.StartNew();
        Task.Delay(milliseconds).Wait(); // deliberately blocking: never do this in real code
        return Ok(new { style = "blocking", milliseconds = stopwatch.ElapsedMilliseconds, threadPoolThreads = ThreadPool.ThreadCount });
    }

    /// <summary>LAB 3b, the RIGHT way: await gives the thread back to the pool while waiting.</summary>
    [HttpGet("non-blocking")]
    public async Task<ActionResult<object>> NonBlocking([FromQuery] int milliseconds = 2000, CancellationToken ct = default)
    {
        if (LabOff) return NotFound();
        milliseconds = Math.Clamp(milliseconds, 100, 10000);

        var stopwatch = Stopwatch.StartNew();
        await Task.Delay(milliseconds, ct);
        return Ok(new { style = "non-blocking", milliseconds = stopwatch.ElapsedMilliseconds, threadPoolThreads = ThreadPool.ThreadCount });
    }

    /// <summary>LAB 4, slow database call: SQL Server waits on purpose, so the time shows up in Fiddler and Wireshark.</summary>
    [HttpGet("slow-query")]
    public async Task<ActionResult<object>> SlowQuery([FromQuery] int seconds = 2, CancellationToken ct = default)
    {
        if (LabOff) return NotFound();
        seconds = Math.Clamp(seconds, 1, 10);

        var delay = TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss"); // "00:00:02"
        var stopwatch = Stopwatch.StartNew();
        await db.Database.ExecuteSqlAsync($"WAITFOR DELAY {delay}", ct); // sent as a parameter, not pasted into the SQL
        return Ok(new { sqlMilliseconds = stopwatch.ElapsedMilliseconds });
    }

    /// <summary>LAB 5, exception storm: throws and catches many exceptions. Watch the exception rate in dotnet-counters.</summary>
    [HttpGet("exceptions")]
    public ActionResult<object> Exceptions([FromQuery] int count = 1000)
    {
        if (LabOff) return NotFound();
        count = Math.Clamp(count, 1, 100000);

        var caught = 0;
        for (var i = 0; i < count; i++)
        {
            try { throw new InvalidOperationException($"Lab exception {i}"); }
            catch (InvalidOperationException) { caught++; } // exceptions are expensive: never use them for normal control flow
        }

        return Ok(new { caught });
    }

    private static bool IsPrime(long n)
    {
        if (n < 2) return false;
        for (long d = 2; d * d <= n; d++)
            if (n % d == 0) return false;
        return true;
    }

    /// <summary>A named type, so it is easy to find in a memory dump: !dumpheap -type LeakyCacheEntry</summary>
    private sealed class LeakyCacheEntry(string key)
    {
        public const int PayloadKilobytes = 1024;

        public string Key { get; } = key;
        public DateTime CreatedUtc { get; } = DateTime.UtcNow;
        public byte[] Payload { get; } = CreatePayload();

        // Filled with data so Windows really commits the memory (an all-zero array may not show in Working Set).
        private static byte[] CreatePayload()
        {
            var bytes = new byte[PayloadKilobytes * 1024];
            Random.Shared.NextBytes(bytes);
            return bytes;
        }
    }
}
