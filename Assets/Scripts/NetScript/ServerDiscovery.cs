using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

/// <summary>
/// Handles server discovery over local network using UDP broadcast.
/// Server sends beacons with its IP and port.
/// Client listens for beacons and auto-connects.
/// </summary>
public class ServerDiscovery : MonoBehaviour
{
    [SerializeField] private ushort discoveryPort = 7779; // Different from game port
    [SerializeField] private ushort gamePort = 7778;
    [SerializeField] [Min(3f)] private float clientDiscoveryTimeoutSeconds = 22f;

    /// <summary>Must match the UDP port UnityTransport is listening on (call after host/server starts).</summary>
    public void SetAdvertisedGamePort(ushort port) => gamePort = port;
    
    private UdpClient udpClient;
    private bool isActive = false;
    private IPEndPoint remoteIpEndPoint;

    private static ServerDiscovery instance;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        StopDiscovery();
        if (instance == this)
            instance = null;
    }

    /// <summary>Does not create a GameObject. Use during shutdown so teardown does not spawn a new instance.</summary>
    public static bool TryGetExisting(out ServerDiscovery sd)
    {
        sd = instance;
        return sd != null;
    }

    /// <summary>Start server broadcasting its presence on the network.</summary>
    public void StartServerBroadcast()
    {
        if (isActive)
            return;

        try
        {
            udpClient = new UdpClient();
            udpClient.ExclusiveAddressUse = false;
            udpClient.EnableBroadcast = true;
            isActive = true;

            StartCoroutine(BroadcastServerRoutine());
            Debug.Log($"ServerDiscovery: Server broadcasting on port {discoveryPort}");
        }
        catch (Exception e)
        {
            Debug.LogError($"ServerDiscovery: Failed to start broadcast: {e.Message}");
        }
    }

    /// <summary>Start client listening for servers on the network.</summary>
    public void StartClientDiscovery(System.Action<string, ushort> onServerFound, System.Action onDiscoveryTimeout)
    {
        if (isActive)
            return;

        try
        {
            // Explicit bind + reuse helps some OSes receive Wi‑Fi broadcast frames reliably.
            udpClient = new UdpClient();
            udpClient.ExclusiveAddressUse = false;
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, discoveryPort));
            udpClient.EnableBroadcast = true;
            isActive = true;
            remoteIpEndPoint = new IPEndPoint(IPAddress.Any, discoveryPort);

            StartCoroutine(DiscoverServerRoutine(onServerFound, onDiscoveryTimeout));
            Debug.Log($"ServerDiscovery: Client listening on UDP {discoveryPort} for LAN beacons (timeout {clientDiscoveryTimeoutSeconds:0}s).");
        }
        catch (Exception e)
        {
            Debug.LogError($"ServerDiscovery: Failed to start discovery: {e.Message}");
            onDiscoveryTimeout?.Invoke();
        }
    }

    private IEnumerator BroadcastServerRoutine()
    {
        bool loggedTargetsOnce = false;

        while (isActive && udpClient != null)
        {
            string serverIp = LanAddressUtility.GetPrimaryIpv4();
            // Pipe delimiter so IPv4 is not split by mistake (colon breaks "192.168.x.x").
            string message = $"GAMESERVER|{serverIp}|{gamePort}";
            byte[] data = System.Text.Encoding.UTF8.GetBytes(message);

            try
            {
                var targets = LanAddressUtility.GetDiscoveryBroadcastEndpoints(discoveryPort);
                if (!loggedTargetsOnce)
                {
                    loggedTargetsOnce = true;
                    Debug.Log(
                        $"ServerDiscovery: sending discovery beacons to {targets.Count} address(es) " +
                        "(global + subnet broadcasts; subnet helps on many Wi‑Fi routers).");
                }

                foreach (var ep in targets)
                {
                    try
                    {
                        udpClient.Send(data, data.Length, ep);
                    }
                    catch (SocketException ex)
                    {
                        Debug.LogWarning($"ServerDiscovery: send to {ep} failed: {ex.SocketErrorCode} {ex.Message}");
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                yield break;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"ServerDiscovery: Broadcast failed: {e.Message}");
            }

            yield return new WaitForSeconds(1f);
        }
    }

    private IEnumerator DiscoverServerRoutine(System.Action<string, ushort> onServerFound, System.Action onDiscoveryTimeout)
    {
        float timeoutSeconds = clientDiscoveryTimeoutSeconds;
        float elapsedTime = 0f;
        int packetsReceived = 0;

        while (isActive && udpClient != null && elapsedTime < timeoutSeconds)
        {
            bool serverFound = false;
            string serverAddress = "";
            ushort serverGamePort = 0;

            try
            {
                if (udpClient.Available > 0)
                {
                    byte[] data = udpClient.Receive(ref remoteIpEndPoint);
                    string message = System.Text.Encoding.UTF8.GetString(data);
                    packetsReceived++;

                    Debug.Log($"ServerDiscovery: packet #{packetsReceived} from {remoteIpEndPoint.Address}: {message}");

                    if (TryParseGameServerBeacon(message, remoteIpEndPoint, out serverAddress, out serverGamePort))
                    {
                        serverFound = true;
                        Debug.Log($"ServerDiscovery: parsed server {serverAddress}:{serverGamePort}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"ServerDiscovery: Error during discovery: {e.Message}");
            }

            if (serverFound)
            {
                Debug.Log($"ServerDiscovery: Found server at {serverAddress}");
                Debug.Log($"ServerDiscovery: connecting to discovered server at {serverAddress}");
                StopDiscovery();
                onServerFound?.Invoke(serverAddress, serverGamePort);
                yield break;
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Timeout - server not found
        Debug.LogWarning($"ServerDiscovery: No server found within {timeoutSeconds} seconds");
        StopDiscovery();
        onDiscoveryTimeout?.Invoke();
    }

    public void StopDiscovery()
    {
        if (!isActive)
            return;

        isActive = false;
        StopAllCoroutines();

        if (udpClient != null)
        {
            udpClient.Close();
            udpClient.Dispose();
            udpClient = null;
        }

        Debug.Log("ServerDiscovery: Stopped");
    }

    /// <summary>Parses <c>GAMESERVER|ip|port</c> (current) or legacy <c>GAMESERVER:ip:port</c> when ip has no colons.</summary>
    private static bool TryParseGameServerBeacon(string message, IPEndPoint packetSource, out string address, out ushort gamePort)
    {
        address = null;
        gamePort = 0;

        if (string.IsNullOrEmpty(message))
            return false;

        if (message.StartsWith("GAMESERVER|", StringComparison.Ordinal))
        {
            string[] parts = message.Split('|');
            if (parts.Length >= 3 && ushort.TryParse(parts[parts.Length - 1].Trim(), out gamePort))
            {
                address = string.Join("|", parts, 1, parts.Length - 2).Trim();
                if (string.IsNullOrEmpty(address) && packetSource != null)
                    address = packetSource.Address.ToString();
                return !string.IsNullOrEmpty(address);
            }

            return false;
        }

        if (message.StartsWith("GAMESERVER:", StringComparison.Ordinal))
        {
            const string prefix = "GAMESERVER:";
            string rest = message.Substring(prefix.Length);
            int lastColon = rest.LastIndexOf(':');
            if (lastColon > 0 && lastColon < rest.Length - 1 &&
                ushort.TryParse(rest.Substring(lastColon + 1).Trim(), out gamePort))
            {
                address = rest.Substring(0, lastColon).Trim();
                if (string.IsNullOrEmpty(address) && packetSource != null)
                    address = packetSource.Address.ToString();
                return !string.IsNullOrEmpty(address);
            }
        }

        return false;
    }

    public static ServerDiscovery GetInstance()
    {
        if (instance == null)
        {
            GameObject obj = new GameObject("ServerDiscovery");
            instance = obj.AddComponent<ServerDiscovery>();
        }
        return instance;
    }
}
