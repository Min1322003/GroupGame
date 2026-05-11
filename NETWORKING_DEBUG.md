# Network Connection Debugging Checklist

## Your Symptom
- ✅ "pending client-0 connection approved" appears
- ❌ Then "no route to host" errors follow
- This means: **Initial connection succeeds, but communication fails immediately after**

---

## Root Cause Analysis

### Most Likely: IP Address Mismatch
1. **Server broadcasts IP via UDP discovery**: `GAMESERVER:192.168.x.x:7778`
2. **Client connects to that IP**: Connection succeeds (transport level)
3. **Client sends game traffic to that IP**: "No route to host" — IP is unreachable from client's perspective

**Why this happens:**
- Server broadcasts `LanAddressUtility.GetPrimaryIpv4()` which might be:
  - The loopback address (127.0.0.1)
  - A VPN adapter IP unreachable from the client's subnet
  - A virtual network adapter (Docker, VM, etc.)
  - An IP that's not globally routable from the other PC

---

## Debugging Steps

### Step 1: Check Server Broadcast
1. Add the debug script: **ConnectionDebugger.cs** (already added)
2. Start Server/Host
3. **Check Console Output** for:
   ```
   [SERVER BROADCAST DEBUG] Broadcasting: GAMESERVER:192.168.x.x:7778
   [CONNECTION DEBUG] SERVER STARTED
     Server Listen Address: 0.0.0.0
     Server Port: 7778
     Primary LAN IP: ???
   ```
4. **Verify**: Primary LAN IP should be reachable from the other PC
   - On Windows/Mac: `ipconfig` (Windows) or `ifconfig` (Mac)
   - Look for something like `192.168.1.x` or `10.x.x.x`
   - **NOT** `127.0.0.1` (loopback)
   - **NOT** `169.254.x.x` (link-local)

### Step 2: Check Client Discovery
1. Start Client on OTHER PC
2. **Check Console for**:
   ```
   [DISCOVERY DEBUG] Received packet #1: GAMESERVER:192.168.x.x:7778 from 192.168.x.x
   [DISCOVERY DEBUG] Valid server message parsed: IP=192.168.x.x, Port=7778
   [CLIENT CONNECTION DEBUG] Attempting to connect to 192.168.x.x:7778
   ```
3. **Verify**: The IP in the discovery packet matches what you expect

### Step 3: Can Ping Between Machines?
1. On Client PC, open Terminal/Command Prompt
2. Try: `ping 192.168.x.x` (replace with Server's IP from debug output)
3. **Expected**: Replies like "Reply from 192.168.x.x: bytes=32 time=<5ms"
4. **Problem**: If you get "Host unreachable" or timeout, that's your issue

---

## Solutions

### Solution A: Manual IP Override (Quick Test)
Instead of relying on auto-discovery:
1. On **Server PC**: Manually enter its actual LAN IP in connection settings
   - Get IP: `ipconfig` (Windows) or `ifconfig` (Mac)
   - Look for active network adapter (Wi‑Fi or Ethernet)
2. On **Client PC**: Manually enter Server's IP in connection settings
3. Click **Host** or **Server** on Server PC
4. Click **Client** on Client PC
5. Does it work? **Yes** → IP was wrong in discovery

### Solution B: Fix LanAddressUtility (Permanent)
If the wrong IP is being picked (e.g., VPN adapter):

In **LanAddressUtility.cs**, modify `ScoreLan()` to **exclude** certain adapters:
```csharp
private static int ScoreLan(string ip)
{
    // Skip VPN, Docker, etc.
    if (ip.StartsWith("172.17.")) return 999;  // Docker
    if (ip.StartsWith("192.168.")) return 0;    // Preferred
    if (ip.StartsWith("10.")) return 1;         // Also good
    if (ip.StartsWith("172.") && IsPrivateRange(ip)) return 2;
    
    return 10;  // Don't use
}
```

### Solution C: Force Listen Address
In **NetManagerUI.cs** `ApplyTransportSettingsFromUi()`:
- Toggle **"Listen on all interfaces"** toggle OFF
- Set Server Listen Address to your actual LAN IP (not 0.0.0.0)
- This is more explicit

---

## Firewall / NAT Issues

If manual IP works over LAN but fails over internet:
1. **Port Forwarding**: Forward port 7778 (TCP/UDP) on your router to Server PC's internal IP
2. **Firewall**: Allow Unity/your game through Windows Firewall
3. **UPnP**: Some routers support automatic port mapping (check UnityTransport.SetConnectionData() docs)

---

## Common Mistakes

❌ **Using localhost (127.0.0.1)** on different machines — each PC's localhost is itself  
❌ **Using public IP** instead of LAN IP — won't work on same network  
❌ **Port mismatch** — both must use same port  
❌ **Different network subnets** — PCs must be on same Wi‑Fi network  
❌ **Server crashes** after connection approved — check server logs for exceptions

---

## Next Steps

1. **Run the debug script** (ConnectionDebugger.cs added to your project)
2. **Check console output** at each step above
3. **Verify manual IP connection** works
4. **Paste console output here** if stuck — include:
   - Server's broadcast message
   - Client's discovery packets received
   - Exact "no route to host" error
   - IP addresses involved

