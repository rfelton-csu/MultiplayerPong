using Unity.Netcode;
using UnityEngine;

public enum TestSessionPhase
{
    Lobby,
    Countdown,
    Playing,
    GameOver,
}

/*
 * TestSessionStateMachine is Demo 08's match phase. Clients may request
 * a change; only the server checks ready players, runs the countdown,
 * and writes the phase. Countdown can return to Lobby, and GameOver can
 * return to Lobby.
 */

public class TestSessionStateMachine : NetworkBehaviour
{
    [SerializeField, Min(1)]
    int requiredReadyPlayers = 2;

    [SerializeField, Min(0.25f)]
    float countdownSeconds = 3f;

    public TestSessionPhase CurrentPhase => _phase.Value;
    public int RequiredReadyPlayers => requiredReadyPlayers;
    public float CountdownRemaining => _countdownRemaining;
    public int ReadyPlayerCount => CountReadyPlayers();
    public bool CanStart =>
        _phase.Value == TestSessionPhase.Lobby
        && HasHostAndClient
        && CountReadyPlayers() >= requiredReadyPlayers;

    readonly NetworkVariable<TestSessionPhase> _phase = new(
        TestSessionPhase.Lobby,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    float _countdownRemaining;
    string _lastBlockedReason = "Waiting for ready players.";

    public override void OnNetworkSpawn()
    {
        if (_phase.Value == TestSessionPhase.Lobby)
            Debug.Log(
                $"[SessionState] Spawned | phase={_phase.Value} | isServer={IsServer} | isClient={IsClient}"
            );

        _phase.OnValueChanged += HandlePhaseChanged;

        if (!IsServer || NetworkManager == null)
            return;

        _phase.Value = TestSessionPhase.Lobby;
        _countdownRemaining = 0f;
        _lastBlockedReason = "Waiting for ready players.";
        NetworkManager.OnConnectionEvent += HandleConnectionEvent;
    }

    public override void OnNetworkDespawn()
    {
        _phase.OnValueChanged -= HandlePhaseChanged;

        if (IsServer && NetworkManager != null)
            NetworkManager.OnConnectionEvent -= HandleConnectionEvent;
    }

    void Update()
    {
        if (!IsServer)
            return;

        if (_phase.Value == TestSessionPhase.Lobby && CanStart)
        {
            TryStartCountdown("host and client connected");
            return;
        }

        if (_phase.Value == TestSessionPhase.Countdown)
        {
            if (CountReadyPlayers() < requiredReadyPlayers)
            {
                ChangePhase(
                    TestSessionPhase.Lobby,
                    "countdown cancelled because not enough ready players remain"
                );
                return;
            }

            _countdownRemaining -= Time.deltaTime;
            if (_countdownRemaining <= 0f)
                ChangePhase(TestSessionPhase.Playing, "countdown finished");
        }
    }

    public void RequestStart()
    {
        if (IsServer)
        {
            TryStartCountdown("local server control");
            return;
        }

        RequestStartRpc();
    }

    public void RequestGameOver()
    {
        if (IsServer)
        {
            TryEndGame("local server control");
            return;
        }

        RequestGameOverRpc();
    }

    public void RequestReturnToLobby()
    {
        if (IsServer)
        {
            TryReturnToLobby("local server reset");
            return;
        }

        RequestReturnToLobbyRpc();
    }

    [Rpc(SendTo.Server)]
    void RequestStartRpc(RpcParams rpcParams = default)
    {
        TryStartCountdown($"client request from {rpcParams.Receive.SenderClientId}");
    }

    [Rpc(SendTo.Server)]
    void RequestGameOverRpc(RpcParams rpcParams = default)
    {
        TryEndGame($"client request from {rpcParams.Receive.SenderClientId}");
    }

    [Rpc(SendTo.Server)]
    void RequestReturnToLobbyRpc(RpcParams rpcParams = default)
    {
        TryReturnToLobby($"client reset request from {rpcParams.Receive.SenderClientId}");
    }

    void TryStartCountdown(string reason)
    {
        if (_phase.Value != TestSessionPhase.Lobby)
        {
            Reject($"start rejected from {reason}; phase is {_phase.Value}");
            return;
        }

        if (!HasHostAndClient)
        {
            Reject($"start rejected from {reason}; waiting for a host and client");
            return;
        }

        int readyPlayers = CountReadyPlayers();
        if (readyPlayers < requiredReadyPlayers)
        {
            Reject(
                $"start rejected from {reason}; readyPlayers={readyPlayers}, "
                    + $"requiredReadyPlayers={requiredReadyPlayers}"
            );
            return;
        }

        _countdownRemaining = countdownSeconds;
        ChangePhase(TestSessionPhase.Countdown, reason);
    }

    bool HasHostAndClient =>
        NetworkManager != null
        && NetworkManager.IsHost
        && NetworkManager.ConnectedClientsIds.Count >= 2;

    void TryEndGame(string reason)
    {
        if (_phase.Value != TestSessionPhase.Playing)
        {
            Reject($"game over rejected from {reason}; phase is {_phase.Value}");
            return;
        }

        ChangePhase(TestSessionPhase.GameOver, reason);
    }

    void TryReturnToLobby(string reason)
    {
        if (_phase.Value != TestSessionPhase.GameOver)
        {
            Reject($"lobby reset rejected from {reason}; phase is {_phase.Value}");
            return;
        }

        ChangePhase(TestSessionPhase.Lobby, reason);
    }

    void HandleConnectionEvent(NetworkManager networkManager, ConnectionEventData eventData)
    {
        if (!IsServer)
            return;

        if (eventData.EventType == ConnectionEvent.ClientDisconnected)
            RecheckSessionCanContinue("client disconnected");
    }

    void RecheckSessionCanContinue(string reason)
    {
        if (_phase.Value == TestSessionPhase.Lobby || _phase.Value == TestSessionPhase.GameOver)
            return;

        if (CountReadyPlayers() >= requiredReadyPlayers)
            return;

        TestSessionPhase fallback =
            _phase.Value == TestSessionPhase.Playing
                ? TestSessionPhase.GameOver
                : TestSessionPhase.Lobby;
        ChangePhase(fallback, reason);
    }

    void ChangePhase(TestSessionPhase nextPhase, string reason)
    {
        if (!IsServer)
            return;
        if (_phase.Value == nextPhase)
            return;

        TestSessionPhase previousPhase = _phase.Value;
        if (!IsValidTransition(previousPhase, nextPhase))
        {
            Reject($"invalid transition {previousPhase} -> {nextPhase} from {reason}");
            return;
        }

        _phase.Value = nextPhase;
        _lastBlockedReason = string.Empty;
        if (nextPhase == TestSessionPhase.Lobby)
            _lastBlockedReason = "Waiting for ready players.";

        Debug.Log($"[SessionState] {previousPhase} -> {nextPhase} | reason={reason}");
    }

    void Reject(string reason)
    {
        _lastBlockedReason = reason;
        Debug.LogWarning($"[SessionState] {reason}");
    }

    void HandlePhaseChanged(TestSessionPhase previousPhase, TestSessionPhase currentPhase)
    {
        Debug.Log($"[SessionState] Observed phase change {previousPhase} -> {currentPhase}");
    }

    int CountReadyPlayers()
    {
        int count = 0;
        foreach (
            var player in FindObjectsByType<TestSpawnPositionPlayer>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            )
        )
        {
            if (!player.HasSpawned)
                continue;
            count++;
        }

        return count;
    }

    static bool IsValidTransition(TestSessionPhase current, TestSessionPhase next)
    {
        return current switch
        {
            TestSessionPhase.Lobby => next == TestSessionPhase.Countdown,
            TestSessionPhase.Countdown => next == TestSessionPhase.Playing
                || next == TestSessionPhase.Lobby,
            TestSessionPhase.Playing => next == TestSessionPhase.GameOver,
            TestSessionPhase.GameOver => next == TestSessionPhase.Lobby,
            _ => false,
        };
    }
}
