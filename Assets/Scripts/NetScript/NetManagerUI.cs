using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class NetManagerUI : MonoBehaviour
{
    [SerializeField] private Button serverButton;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;

    private void Awake()
    {
        serverButton.onClick.AddListener(StartServer);
        hostButton.onClick.AddListener(StartHost);
        clientButton.onClick.AddListener(StartClient);
    }

    private void StartServer()
    {
        if (!NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.StartServer();
    }

    private void StartHost()
    {
        if (!NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.StartHost();
    }

    private void StartClient()
    {
        if (!NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.StartClient();
    }
}