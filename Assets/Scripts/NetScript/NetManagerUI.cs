using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;

public class NetManagerUI : MonoBehaviour
{
    [Tooltip("Dedicated server: no player on this machine. Other PC uses Client with this PC's LAN IP.")]
    [SerializeField] private Button serverButton;
    [Tooltip("Best for two lab PCs: runs server + player here. Other PC uses Client with this PC's LAN IP.")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;

    [Header("Connection settings popup")]
    [Tooltip("Root panel for address/port (disabled until opened).")]
    [SerializeField] private GameObject connectionSettingsPopup;
    [Tooltip("Opens the popup so the player can edit address and port.")]
    [SerializeField] private Button openConnectionSettingsButton;
    [SerializeField] private Button connectionSettingsApplyButton;
    [SerializeField] private Button connectionSettingsCancelButton;

    [Header("Inputs on the popup (TMP or legacy InputField)")]
    [SerializeField] private InputField addressInput;
    [SerializeField] private TMP_InputField addressInputTmp;
    [SerializeField] private InputField portInput;
    [SerializeField] private TMP_InputField portInputTmp;
    [Tooltip("If assigned, server/host can listen on all interfaces (0.0.0.0).")]
    [SerializeField] private Toggle listenOnAllInterfacesToggle;
    [Tooltip("Used when the toggle above is not assigned.")]
    [SerializeField] private bool serverListenOnAllInterfaces = true;

    [Header("LAN / school lab (optional UI)")]
    [Tooltip("Shows this PC's Wi‑Fi/LAN IPs so the other computer can paste the right address. Rich Text optional for TMP.")]
    [SerializeField] private TMP_Text lanInfoText;
    [SerializeField] private Text lanInfoTextLegacy;
    [Tooltip("Sets the Address field to this PC's LAN IP (use on the machine that runs Host or Server).")]
    [SerializeField] private Button fillAddressWithThisPcsLanButton;

    [Header("Port defaults (7777 is often busy)")]
    [Tooltip("Used when the port field is empty, and as the first port to try. 7778 avoids clashing with another NGO build on 7777.")]
    [SerializeField] private ushort defaultGamePort = 7778;
    [Tooltip("If Host/Server fails to bind, try defaultGamePort+1, +2, … and update the UI so clients can match.")]
    [SerializeField] private bool tryAlternatePortsWhenListenFails = true;
    [SerializeField] private int maxAlternatePortTries = 32;

    private void Awake()
    {
        serverButton.onClick.AddListener(StartServer);
        hostButton.onClick.AddListener(StartHost);
        clientButton.onClick.AddListener(StartClient);

        if (openConnectionSettingsButton != null)
            openConnectionSettingsButton.onClick.AddListener(OpenConnectionSettings);
        if (connectionSettingsApplyButton != null)
            connectionSettingsApplyButton.onClick.AddListener(OnConnectionSettingsApply);
        if (connectionSettingsCancelButton != null)
            connectionSettingsCancelButton.onClick.AddListener(OnConnectionSettingsCancel);
        if (fillAddressWithThisPcsLanButton != null)
            fillAddressWithThisPcsLanButton.onClick.AddListener(OnFillAddressWithThisPcsLan);

        if (connectionSettingsPopup != null)
            connectionSettingsPopup.SetActive(false);
    }

    /// <summary>Call from a UI Button via inspector, or use <see cref="openConnectionSettingsButton"/>.</summary>
    public void OpenConnectionSettings()
    {
        if (connectionSettingsPopup != null)
            connectionSettingsPopup.SetActive(true);

        SyncFieldsFromTransport();
        RefreshLanHintText();
    }

    private void OnFillAddressWithThisPcsLan()
    {
        string lan = LanAddressUtility.GetPrimaryIpv4();
        if (string.IsNullOrEmpty(lan))
        {
            Debug.LogWarning("NetManagerUI: No LAN IPv4 found on this computer.");
            return;
        }

        SetAddressUi(lan);
        RefreshLanHintText();
    }

    private void RefreshLanHintText()
    {
        ushort portForHint = ReadPortUi();
        string msg = LanAddressUtility.BuildLanHintForUi(portForHint);
        if (lanInfoText != null)
            lanInfoText.text = msg;
        if (lanInfoTextLegacy != null)
            lanInfoTextLegacy.text = msg;
    }

    private void CloseConnectionSettings()
    {
        if (connectionSettingsPopup != null)
            connectionSettingsPopup.SetActive(false);
    }

    private void OnConnectionSettingsApply()
    {
        ApplyTransportSettingsFromUi(GetListenOnAllInterfaces());
        CloseConnectionSettings();
    }

    private void OnConnectionSettingsCancel()
    {
        SyncFieldsFromTransport();
        CloseConnectionSettings();
    }

    private bool GetListenOnAllInterfaces()
    {
        if (listenOnAllInterfacesToggle != null)
            return listenOnAllInterfacesToggle.isOn;
        return serverListenOnAllInterfaces;
    }

    private void SyncFieldsFromTransport()
    {
        string address = "127.0.0.1";
        string portText = defaultGamePort.ToString();
        bool listenAll = serverListenOnAllInterfaces;

        if (NetworkManager.Singleton != null)
        {
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                var cd = transport.ConnectionData;
                if (!string.IsNullOrWhiteSpace(cd.Address))
                    address = cd.Address;
                portText = cd.Port.ToString();
                if (!string.IsNullOrEmpty(cd.ServerListenAddress))
                    listenAll = cd.ServerListenAddress == "0.0.0.0";
            }
        }

        SetAddressUi(address);
        SetPortUi(portText);
        if (listenOnAllInterfacesToggle != null)
            listenOnAllInterfacesToggle.isOn = listenAll;
    }

    private void SetAddressUi(string value)
    {
        if (addressInputTmp != null)
            addressInputTmp.text = value;
        else if (addressInput != null)
            addressInput.text = value;
    }

    private void SetPortUi(string value)
    {
        if (portInputTmp != null)
            portInputTmp.text = value;
        else if (portInput != null)
            portInput.text = value;
    }

    private string ReadAddressUi()
    {
        string raw = addressInputTmp != null ? addressInputTmp.text : addressInput != null ? addressInput.text : "";
        return string.IsNullOrWhiteSpace(raw) ? "127.0.0.1" : raw.Trim();
    }

    private ushort ReadPortUi()
    {
        string raw = portInputTmp != null ? portInputTmp.text : portInput != null ? portInput.text : "";
        if (!string.IsNullOrWhiteSpace(raw) && ushort.TryParse(raw.Trim(), out ushort parsed))
            return parsed;

        // Empty field: use inspector default, not the UnityTransport asset (often still 7777).
        return defaultGamePort;
    }

    /// <summary>
    /// Writes address/port into <see cref="UnityTransport"/>.
    /// Uses forceOverride so values beat -ip / -port CLI.
    /// </summary>
    private void ApplyTransportSettingsFromUi(bool useWideBindForListen)
    {
        ApplyTransportSettingsFromUi(useWideBindForListen, ReadPortUi());
    }

    private void ApplyTransportSettingsFromUi(bool useWideBindForListen, ushort port)
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetManagerUI: No NetworkManager in scene.");
            return;
        }

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError("NetManagerUI: NetworkManager needs a UnityTransport component.");
            return;
        }

        string address = ReadAddressUi();
        const bool forceOverrideCommandLine = true;

        if (useWideBindForListen)
            transport.SetConnectionData(forceOverrideCommandLine, address, port, "0.0.0.0");
        else
            transport.SetConnectionData(forceOverrideCommandLine, address, port);
    }

    /// <summary>Tries Host or Server on an increasing port range if the UDP bind fails (e.g. 7777 already in use).</summary>
    private bool TryStartListen(bool asHost)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.IsListening)
            return false;

        ushort firstPort = ReadPortUi();
        bool wide = GetListenOnAllInterfaces();
        int tries = tryAlternatePortsWhenListenFails ? Mathf.Clamp(maxAlternatePortTries, 1, 256) : 1;

        for (int i = 0; i < tries; i++)
        {
            int sum = firstPort + i;
            if (sum > ushort.MaxValue)
                break;
            ushort port = (ushort)sum;

            ApplyTransportSettingsFromUi(wide, port);

            bool ok = asHost ? nm.StartHost() : nm.StartServer();
            if (ok)
            {
                if (i > 0)
                {
                    Debug.Log(
                        $"NetManagerUI: Listening on port {port} (starting from {firstPort}, earlier port(s) were busy). " +
                        "Use this same port on the Client.");
                }

                SetPortUi(port.ToString());
                RefreshLanHintText();
                return true;
            }

            nm.Shutdown();
        }

        Debug.LogError(
            $"NetManagerUI: Could not start {(asHost ? "Host" : "Server")} after {tries} attempt(s) from port {firstPort}. " +
            "Pick another port in connection settings or stop the other program using this port.");
        return false;
    }

    private void StartServer()
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.IsListening)
            return;

        TryStartListen(asHost: false);
    }

    private void StartHost()
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.IsListening)
            return;

        TryStartListen(asHost: true);
    }

    private void StartClient()
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.IsListening)
            return;

        ApplyTransportSettingsFromUi(false);
        NetworkManager.Singleton.StartClient();
    }
}
