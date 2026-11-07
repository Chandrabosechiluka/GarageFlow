# Diagnostics lab: PerfMon, dotnet-counters, WinDbg, Fiddler, Wireshark

Goal: use the four tools named in the D. E. Shaw job description on GarageFlow, find a real cause with each one,
and be able to explain it in an interview. Plan for **3 evenings**:

| Evening | Labs | Tools |
|---|---|---|
| 1 | Setup, Lab 1 (memory leak), Lab 2 (CPU spike) | PerfMon, dotnet-counters, WinDbg |
| 2 | Lab 3 (thread-pool starvation), Lab 5 (exceptions) | dotnet-counters, WinDbg |
| 3 | Lab 4 (slow query) and HTTP/TCP traffic | Fiddler, Wireshark |

Fill in the **Findings** table at the end as you go. Those numbers are your interview story.

---

## 0. What was added to the code

`src/GarageFlow.Api/Controllers/DiagnosticsLabController.cs`: endpoints that create ONE problem each, on purpose.

| Endpoint | Problem it creates | Real-world version |
|---|---|---|
| `GET /api/lab/status` | none: shows memory, GC counts, thread-pool threads | a health/diagnostics page |
| `POST /api/lab/leak?entries=10` | adds 10 × 1 MB objects to a **static list** that is never cleared | a static cache or event handler that only grows |
| `DELETE /api/lab/leak` | the fix: clears the list and lets the GC take the memory back | |
| `GET /api/lab/cpu?milliseconds=5000` | burns one CPU core | an expensive loop or bad algorithm |
| `GET /api/lab/blocking?milliseconds=5000` | `Task.Delay(...).Wait()`: blocks a thread-pool thread | **sync-over-async**, `.Result`, `.Wait()` |
| `GET /api/lab/non-blocking?milliseconds=5000` | the fix: `await Task.Delay(...)` | |
| `GET /api/lab/slow-query?seconds=2` | SQL Server waits 2 s (`WAITFOR DELAY`) | a missing index, a lock wait |
| `GET /api/lab/exceptions?count=10000` | throws and catches 10 000 exceptions | exceptions used for normal control flow |

**Safety:** every lab endpoint returns **404** unless the app runs in Development **and** `DiagnosticsLab:Enabled`
is `true` (it is `false` in `appsettings.json`). Only an **Admin** may call them. `DiagnosticsLabTests` proves both.

---

## 1. One-time setup (about 30 minutes)

Open **PowerShell** as your normal user.

```powershell
# Tools
winget install Microsoft.WinDbg            # WinDbg (the modern one from Microsoft)
winget install WiresharkFoundation.Wireshark   # in the installer, KEEP "Install Npcap" ticked
#   In the Npcap installer tick "Support loopback traffic capture" (needed to see localhost traffic).
winget install Telerik.Fiddler.Classic     # free Fiddler Classic
winget install Microsoft.PowerShell        # PowerShell 7 (pwsh), needed for parallel requests below

dotnet tool install -g dotnet-counters
dotnet tool install -g dotnet-dump
dotnet tool install -g dotnet-sos
dotnet-sos install                         # puts SOS (the .NET extension for WinDbg) in %USERPROFILE%\.dotnet\sos
```

PerfMon is already in Windows (`Win + R` → `perfmon`).

Switch the lab on, **only on your machine** (user secrets are never committed):

```powershell
cd F:\projects\GarageFlow
dotnet user-secrets set "DiagnosticsLab:Enabled" "true" --project src/GarageFlow.Api
mkdir F:\dumps -ErrorAction SilentlyContinue
```

Start the database and the API (Release build, closer to production):

```powershell
docker compose up -d
dotnet run --project src/GarageFlow.Api -c Release
```

In a **second** window, open **pwsh** (PowerShell 7) and log in once. Keep this window for all labs:

```powershell
$base  = "http://localhost:5080"
$login = Invoke-RestMethod -Method Post -Uri "$base/api/auth/login" -ContentType "application/json" `
           -Body '{"email":"admin@garageflow.local","password":"Garage@12345"}'
$h = @{ Authorization = "Bearer $($login.accessToken)" }
Invoke-RestMethod "$base/api/lab/status" -Headers $h      # note the processId
```

If you get 404, the switch is off (check the user-secrets command and that the profile is Development).

---

## 2. Lab 1: memory leak (PerfMon → dotnet-counters → WinDbg)

**Symptom first, with PerfMon**
1. `Win + R` → `perfmon` → **Performance Monitor** → green **+**.
2. Under **Process**, select **Private Bytes**, **Working Set - Private**, **% Processor Time**, **Thread Count**,
   pick instance **GarageFlow.Api** → **Add** → **OK**. Right-click the graph → **Properties** → set the vertical scale so the lines fit.
3. Note: .NET 8 does not publish the old ".NET CLR Memory" PerfMon counters. PerfMon shows the *process*; for the
   *runtime* (GC heap, thread pool) we use dotnet-counters. Interviewers like hearing that you know this.

**Create the leak**

```powershell
1..30 | ForEach-Object { Invoke-RestMethod -Method Post "$base/api/lab/leak?entries=10" -Headers $h | Out-Null; Start-Sleep 1 }
Invoke-RestMethod "$base/api/lab/status" -Headers $h
```

Watch **Private Bytes** climb in steps of ~10 MB and never come down (≈300 MB after 30 calls).

**Confirm it is the managed heap, with dotnet-counters** (a third window):

```powershell
dotnet-counters monitor -n GarageFlow.Api --counters System.Runtime
```

Look at **GC Heap Size (MB)** rising with Private Bytes, and **Gen 2 GC Count** going up while the heap does not shrink.
That pattern (GC runs but frees nothing) = something still references the objects = a leak.

**Find the cause, with WinDbg**
1. Take a full dump while the memory is high:
   ```powershell
   dotnet-dump collect -n GarageFlow.Api --type Full -o F:\dumps\leak.dmp
   ```
   (Alternative: Task Manager → Details → right-click GarageFlow.Api.exe → Create memory dump file.)
2. Open WinDbg → **File → Open dump file** → `F:\dumps\leak.dmp`.
3. Load SOS if `!dumpheap` is not recognised:
   ```
   .load %USERPROFILE%\.dotnet\sos\sos.dll
   ```
4. Commands, in order:
   ```
   !eeheap -gc                          total size of the managed heap
   !dumpheap -stat                      every type, how many, how many bytes. Last lines = biggest.
   !dumpheap -type LeakyCacheEntry      the 300 objects and their addresses
   !do <address of one entry>           its fields: Key, CreatedUtc, Payload (System.Byte[])
   !gcroot <same address>               WHO keeps it alive
   ```
5. `!dumpheap -stat` shows **System.Byte[]** at the bottom with ~300 MB. `!gcroot` ends at a
   `List<LeakyCacheEntry>` held by a **static field** (`DiagnosticsLabController.LeakyCache`). That is the root cause:
   a static collection lives as long as the process, so the GC can never collect what is in it.

**Fix and verify**

```powershell
Invoke-RestMethod -Method Delete "$base/api/lab/leak" -Headers $h
```

Private Bytes and GC Heap Size drop back near the start value. In real code the fix is a bounded cache
(`IMemoryCache` with size limits/expiry), or unsubscribing event handlers.

---

## 3. Lab 2: CPU spike (PerfMon → WinDbg `!runaway`)

```powershell
Invoke-RestMethod "$base/api/lab/cpu?milliseconds=15000" -Headers $h
```

1. In PerfMon, **% Processor Time** for GarageFlow.Api jumps to ~100 (one core; on 8 cores the max is 800).
2. While it runs (you have 15 s), in another window:
   ```powershell
   dotnet-dump collect -n GarageFlow.Api --type Full -o F:\dumps\cpu.dmp
   ```
3. In WinDbg:
   ```
   !runaway              user-mode CPU time per thread; the top thread is the hot one
   ~<thread number>s     switch to it (the number on the left of !runaway's first line)
   !clrstack             the .NET call stack of that thread
   ```
   The stack shows `DiagnosticsLabController.IsPrime` called from `Cpu`: the method burning the CPU.
   If `!runaway` says thread times are not available in this dump, take the dump from Task Manager instead
   (it includes them), or run `~*e !clrstack` and look for the thread inside `IsPrime`.

---

## 4. Lab 3: thread-pool starvation (sync-over-async)

This is the most useful lab for a C# interview: it shows *why* async/await matters on a server.

**Baseline**

```powershell
Measure-Command { Invoke-RestMethod "$base/health" } | Select TotalMilliseconds    # a few ms
```

**The wrong way: 200 blocking requests at once**

```powershell
$job = Start-Job -ScriptBlock {
  param($base, $token)
  1..200 | ForEach-Object -Parallel {
    Invoke-RestMethod "$using:base/api/lab/blocking?milliseconds=5000" -Headers @{ Authorization = "Bearer $using:token" } | Out-Null
  } -ThrottleLimit 200
} -ArgumentList $base, $login.accessToken

Start-Sleep 3
Measure-Command { Invoke-RestMethod "$base/health" } | Select TotalMilliseconds    # now seconds, not ms
```

What to watch in `dotnet-counters monitor -n GarageFlow.Api --counters System.Runtime`:
- **ThreadPool Thread Count** keeps growing slowly (the pool adds threads gradually, not all at once).
- **ThreadPool Queue Length** is high: work is waiting because every thread is parked in `.Wait()`.
- CPU is LOW. Slow + low CPU + growing queue = starvation, not overload.

Take a dump while it is slow: `dotnet-dump collect -n GarageFlow.Api --type Full -o F:\dumps\starve.dmp`, then in WinDbg:

```
!threadpool           worker threads in use and the queued work items
~*e !clrstack         stacks of all threads: many end in Task.Wait inside DiagnosticsLabController.Blocking
```

**The right way**

```powershell
Wait-Job $job | Out-Null
$job2 = Start-Job -ScriptBlock {
  param($base, $token)
  1..200 | ForEach-Object -Parallel {
    Invoke-RestMethod "$using:base/api/lab/non-blocking?milliseconds=5000" -Headers @{ Authorization = "Bearer $using:token" } | Out-Null
  } -ThrottleLimit 200
} -ArgumentList $base, $login.accessToken

Start-Sleep 3
Measure-Command { Invoke-RestMethod "$base/health" } | Select TotalMilliseconds    # back to a few ms
```

Same 200 requests, same 5 s wait, but the thread count stays low and `/health` stays fast, because `await` hands the
thread back to the pool while waiting.

---

## 5. Lab 5: exception storm

```powershell
1..20 | ForEach-Object { Invoke-RestMethod "$base/api/lab/exceptions?count=10000" -Headers $h | Out-Null }
```

In dotnet-counters, **Exception Count** jumps. Exceptions are expensive (stack capture), which is why they must not
be used for normal control flow. In WinDbg on a dump: `!dumpheap -stat -type Exception`.

---

## 6. Lab 4 and HTTP: Fiddler

1. Start **Fiddler Classic**. It listens as a proxy on `127.0.0.1:8888`.
2. Fiddler does not see traffic to `localhost` by default. Two ways round it:
   - Browser: open `http://ipv4.fiddler:5080/swagger` instead of `localhost`.
   - PowerShell: add `-Proxy http://127.0.0.1:8888` to `Invoke-RestMethod`.
3. Things to do and look at (click a session, then **Inspectors** → Raw / JSON / Headers):
   - **Login**: the request body, the `200` response with `accessToken`. Copy the token to Fiddler's
     **TextWizard** (Base64 decode the middle part) to see the JWT claims: `sub`, `role`, `exp`.
   - **401 vs 200**: call `/api/appointments` without the `Authorization` header (401 problem-details JSON), then with it.
   - **429**: log in with a wrong password 11 times in a minute; the 11th returns **429 Too Many Requests** (rate limiting).
   - **Correlation ID**: every response carries an `X-Correlation-ID` header. Find the same ID in the API console log:
     that is how you match one user's failing request to the server log in production.
   - **Timing**: call `/api/lab/slow-query?seconds=2` and `/api/lab/non-blocking?milliseconds=100`. Select both →
     **Statistics** / **Timeline** tab: the slow one spends ~2 s waiting for the server's first byte.
   - **Composer**: drag a session into Composer, change a field (an invalid date, a missing field), **Execute**,
     and read the 400 validation response.
4. **File → Save → All Sessions** as `garageflow.saz`: evidence you can show.

---

## 7. Lab 4 and TCP: Wireshark

1. Start Wireshark → capture on **Adapter for loopback traffic capture** (that is Npcap's localhost adapter).
2. Filter `tcp.port == 5080`, then call `/health` once. You will see:
   - the **TCP three-way handshake**: `SYN`, `SYN, ACK`, `ACK`;
   - the HTTP request and response (right-click → **Follow → HTTP Stream** shows them as text: plain HTTP is readable);
   - the connection kept open (keep-alive) or closed with `FIN`.
3. Filter `tcp.port == 1433` (API ↔ SQL Server in Docker), then call `/api/lab/slow-query?seconds=2`.
   - Wireshark decodes the protocol as **TDS** (Tabular Data Stream, SQL Server's wire protocol).
   - The query text is **not** readable: the connection is encrypted with TLS inside TDS (the SqlClient default).
     Good interview point: encrypted DB traffic is why you trace queries with server-side tools, not packet captures.
   - **View → Time Display Format → Seconds Since Previous Displayed Packet**: the gap between the request packet
     and the response packet is ~2 s. That proves the time is spent in the database, not in the API or the network.
4. A failure to recognise: `docker stop garageflow-sqlserver`, call `/health/db`, and watch `SYN` followed by
   `RST, ACK` (connection refused) on port 1433. Then `docker start garageflow-sqlserver`.

---

## 8. Switch the lab off

```powershell
dotnet user-secrets remove "DiagnosticsLab:Enabled" --project src/GarageFlow.Api
```

---

## Findings (fill this in: these are your interview numbers)

| Lab | Tool | What I saw | Number |
|---|---|---|---|
| Leak | PerfMon | Private Bytes after 300 leaked entries | ___ MB (start ___ MB) |
| Leak | dotnet-counters | GC Heap Size / Gen 2 count | ___ MB / ___ |
| Leak | WinDbg | `!gcroot` root of `LeakyCacheEntry` | static field `LeakyCache` |
| Leak | PerfMon | Private Bytes after `DELETE /api/lab/leak` | ___ MB |
| CPU | WinDbg `!runaway` | hottest thread's method | `IsPrime` |
| Starvation | pwsh | `/health` time: normal / during blocking / during non-blocking | ___ / ___ / ___ ms |
| Starvation | dotnet-counters | ThreadPool threads: blocking vs non-blocking | ___ vs ___ |
| Slow query | Fiddler | time to first byte | ___ ms |
| Slow query | Wireshark | gap between TDS request and response | ___ s |

## The interview story (say it in this order)

1. **Symptom**: "Memory kept growing in PerfMon (Private Bytes from ___ to ___ MB) and never dropped."
2. **Narrow down**: "dotnet-counters showed the GC heap growing even after gen-2 collections, so something was still
   referencing the objects."
3. **Root cause**: "I took a full dump with dotnet-dump, opened it in WinDbg with SOS: `!dumpheap -stat` showed
   ~___ MB of byte arrays, and `!gcroot` traced them to a static list."
4. **Fix and proof**: "Clearing/bounding the cache brought memory back to ___ MB."

Same shape for starvation: "`/health` went from 5 ms to ___ s under 200 blocking calls; dotnet-counters showed the
thread-pool queue growing with low CPU; the dump showed threads parked in `Task.Wait`; switching to `await` kept it at ___ ms."
