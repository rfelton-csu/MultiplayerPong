using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/*
 * SessionManager is intentionally light in the starter project.
 * Local Pong starts immediately. The GameManager, NetworkManager, and button
 * references mark where you will start a Host or Client session.
 */

public class SessionManager : NetworkBehaviour
{
    [Header("Multiplayer")]
    [SerializeField]
    GameManager gameManager;

    // This does not already exist in the scene, you need to add it and reference it
    [SerializeField]
    NetworkManager networkManager;

    [Header("Multiplayer UI")]
    [SerializeField]
    Button startHostButton;

    [SerializeField]
    Button startClientButton;

    [SerializeField]
    Button disconnectButton;

    [SerializeField]
    Canvas sessionUI;

    public bool IsConnected => NetworkManager!.IsClient || NetworkManager!.IsServer;
    public int PlayerCount => _playerCount.Value;
    public string LocalRole
    {
        get
        {
            if (NetworkManager!.IsHost)
                return "Host";
            if (NetworkManager!.IsServer)
                return "Server";
            if (NetworkManager!.IsClient)
                return "Client";

            return "Disconnected";
        }
    }
    readonly NetworkVariable<int> _playerCount = new();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer)
            return;

        UpdatePlayerCount();
        NetworkManager.OnConnectionEvent += HandleConnectionEvent;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (!NetworkManager!.IsServer)
            return;

        NetworkManager.OnConnectionEvent -= HandleConnectionEvent;
        _playerCount.Value = 0;
    }

    public void StartHost() => NetworkManager!.StartHost();

    public void StartClient() => NetworkManager!.StartClient();

    public void Disconnect() => NetworkManager!.Shutdown();

    void HandleConnectionEvent(NetworkManager networkManager, ConnectionEventData eventData)
    {
        Debug.Assert(IsServer);
        UpdatePlayerCount();
    }

    void UpdatePlayerCount()
    {
        // Only the server writes NetworkVariables. Clients receive the updated value.
        _playerCount.Value = NetworkManager.ConnectedClientsIds.Count;
    }

    void Awake()
    {
        // Hide Host/Client until you are ready to wire the session.
        // sessionUI.gameObject.SetActive(false);

        startHostButton.onClick.AddListener(() =>
        {
            StartHost();
            startHostButton.gameObject.SetActive(false);
            startClientButton.gameObject.SetActive(false);
            disconnectButton.gameObject.SetActive(true);
        });
        startClientButton.onClick.AddListener(() =>
        {
            StartClient();
            startHostButton.gameObject.SetActive(false);
            startClientButton.gameObject.SetActive(false);
            disconnectButton.gameObject.SetActive(true);
        });
        disconnectButton.onClick.AddListener(() =>
        {
            Disconnect();
            startHostButton.gameObject.SetActive(true);
            startClientButton.gameObject.SetActive(true);
            disconnectButton.gameObject.SetActive(false);
        });
        disconnectButton.gameObject.SetActive(false);
    }

    const float Margin = 16f;
    const float StatusWidth = 220f;
    const float StatusHeight = 54f;
    const float ControlsWidth = 320f;
    const float ControlsHeight = 40f;
    const float ButtonWidth = 140f;
    const float ButtonHeight = 30f;

    static readonly GUILayoutOption[] ButtonSize =
    {
        GUILayout.Width(ButtonWidth),
        GUILayout.Height(ButtonHeight),
    };

    void OnGUI()
    {
        DrawStatus();
    }

    void DrawStatus()
    {
        GUILayout.BeginArea(new Rect(Margin, Margin, StatusWidth, StatusHeight));
        GUILayout.Label(this.IsConnected ? $"Connected: {this.LocalRole}" : "Disconnected");

        if (this.IsConnected)
            GUILayout.Label($"Players: {this.PlayerCount}");

        GUILayout.EndArea();
    }
}
