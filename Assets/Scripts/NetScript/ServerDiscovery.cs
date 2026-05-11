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
    public void StartClientDiscovery(System.Action<string> onServerFound, System.Action onDiscoveryTimeout)
    {
        if (isActive)
            return;

        try
        {
            udpClient = new UdpClient(discoveryPort);
            udpClient.EnableBroadcast = true;
            isActive = true;
            remoteIpEndPoint = new IPEndPoint(IPAddress.Any, discoveryPort);

            StartCoroutine(DiscoverServerRoutine(onServerFound, onDiscoveryTimeout));
            Debug.Log("ServerDiscovery: Client listening for server broadcast...");
        }
        catch (Exception e)
        {
            Debug.LogError($"ServerDiscovery: Failed to start discovery: {e.Message}");
            onDiscoveryTimeout?.Invoke();
        }
    }

    private IEnumerator BroadcastServerRoutine()
    {
        string serverIp = LanAddressUtility.GetPrimaryIpv4();
        string message = $"GAMESERVER:{serverIp}:{gamePort}";
        byte[] data = System.Text.Encoding.UTF8.GetBytes(message);

        while (isActive)
        {
            try
            {
                // Broadcast to 255.255.255.255 on the discovery port
                udpClient.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, discoveryPort));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"ServerDiscovery: Broadcast failed: {e.Message}");
            }

            yield return new WaitForSeconds(1f); // Broadcast every second
        }
    }

    private IEnumerator DiscoverServerRoutine(System.Action<string> onServerFound, System.Action onDiscoveryTimeout)
    {
        float timeoutSeconds = 10f;
        float elapsedTime = 0f;

        while (isActive && elapsedTime < timeoutSeconds)
        {
            bool serverFound = false;
            string serverAddress = "";

            try
            {
                if (udpClient.Available > 0)
                {
                    byte[] data = udpClient.Receive(ref remoteIpEndPoint);
                    string message = System.Text.Encoding.UTF8.GetString(data);

                    if (message.StartsWith("GAMESERVER:"))
                    {
                        string[] parts = message.Split(':');
                        if (parts.Length >= 3 && ushort.TryParse(parts[2], out ushort port))
                        {
                            serverAddress = parts[1];
                            serverFound = true;
                        }
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
                StopDiscovery();
                onServerFound?.Invoke(serverAddress);
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
