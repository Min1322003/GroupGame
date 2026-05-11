using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

/// <summary>
/// Add this to your NetworkManager GameObject to debug connection issues.
/// Shows what address/port the server is listening on and what the client is connecting to.
/// </summary>
public class ConnectionDebugger : MonoBehaviour
{
    private NetworkManager networkManager;
    private UnityTransport transport;
    private float lastLogTime;
    private const float logInterval = 2f;

    private void Awake()
    {
        networkManager = GetComponent<NetworkManager>();
        transport = GetComponent<UnityTransport>();

        if (networkManager != null)
        {
            networkManager.OnServerStarted += OnServerStarted;
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void OnServerStarted()
    {
        Debug.LogError(
            $"[CONNECTION DEBUG] SERVER STARTED\n" +
            $"  Server Listen Address: {transport.ConnectionData.ServerListenAddress}\n" +
            $"  Server Port: {transport.ConnectionData.Port}\n" +
            $"  Primary LAN IP: {LanAddressUtility.GetPrimaryIpv4()}\n" +
            $"  All LAN IPs: {string.Join(", ", LanAddressUtility.GetAllLanIpv4())}"
        );
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.LogError(
            $"[CONNECTION DEBUG] CLIENT CONNECTED (ID: {clientId})\n" +
            $"  Client connecting to: {transport.ConnectionData.Address}:{transport.ConnectionData.Port}"
        );
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.LogError(
            $"[CONNECTION DEBUG] CLIENT DISCONNECTED (ID: {clientId})"
        );
    }

    private void Update()
    {
        if (Time.time - lastLogTime > logInterval)
        {
            lastLogTime = Time.time;

            if (networkManager != null && networkManager.IsListening)
            {
                string role = networkManager.IsHost ? "HOST" : networkManager.IsServer ? "SERVER" : "CLIENT";
                int connectedClients = networkManager.ConnectedClientsIds.Count;
                
                Debug.Log(
                    $"[CONNECTION DEBUG - {role}] Connected: {connectedClients} | " +
                    $"Address: {transport.ConnectionData.Address}:{transport.ConnectionData.Port}"
                );
            }
        }
    }
}
