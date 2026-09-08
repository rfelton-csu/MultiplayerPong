using TMPro;
using Unity.Netcode;
using UnityEngine;

/*
 * GameManager owns the local match rules: scoring, win checks, and ball resets.
 * In the starter project, everything happens in one Unity player. This is
 * the script you will convert so the server owns shared game state and the
 * score is synchronized to every client.
 */

public class GameManager : NetworkBehaviour
{
    [SerializeField]
    Transform ballSpawn;

    [SerializeField]
    GameObject ballPrefab;
    Transform ball;

    [SerializeField]
    float startSpeed = 3f;

    [SerializeField]
    Vector3 startPosition = new(0f, 0.25f, 0f);

    [SerializeField]
    TextMeshProUGUI leftPlayerScoreText;

    [SerializeField]
    TextMeshProUGUI rightPlayerScoreText;

    readonly NetworkVariable<int> _leftPlayerScore = new();
    readonly NetworkVariable<int> _rightPlayerScore = new();
    bool _gameStarted;
    TestSessionStateMachine _sessionStateMachine;

    const int ScoreToWin = 11;

    void Start()
    {
        UpdateScore();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        _leftPlayerScore.OnValueChanged += OnScoreChanged;
        _rightPlayerScore.OnValueChanged += OnScoreChanged;
        UpdateScore();

        if (!IsServer)
            return;

        _sessionStateMachine = FindFirstObjectByType<TestSessionStateMachine>();
    }

    public override void OnNetworkDespawn()
    {
        _leftPlayerScore.OnValueChanged -= OnScoreChanged;
        _rightPlayerScore.OnValueChanged -= OnScoreChanged;

        base.OnNetworkDespawn();
    }

    void Update()
    {
        if (!IsServer || _gameStarted)
            return;

        _sessionStateMachine ??= FindFirstObjectByType<TestSessionStateMachine>();
        if (
            _sessionStateMachine == null
            || _sessionStateMachine.CurrentPhase != TestSessionPhase.Playing
        )
            return;

        SpawnBallOnce();
        StartGame();
        _gameStarted = true;
    }

    public void StartGame()
    {
        if (
            !IsServer
            || _sessionStateMachine == null
            || _sessionStateMachine.CurrentPhase != TestSessionPhase.Playing
        )
            return;

        float direction = Random.value < 0.5f ? -1f : 1f;
        ResetBall(direction);
    }

    public void OnGoalScored(PaddleSide scoringSide)
    {
        // If the ball entered a goal area, increment the score, check for win, and reset the ball

        if (scoringSide == PaddleSide.Left)
        {
            _leftPlayerScore.Value++;
            Debug.Log($"Left player scored: {_leftPlayerScore.Value}");

            if (_leftPlayerScore.Value == ScoreToWin)
                Debug.Log("Left player wins!");
            else
                ResetBall(1f);
        }
        else if (scoringSide == PaddleSide.Right)
        {
            _rightPlayerScore.Value++;
            Debug.Log($"Right player scored: {_rightPlayerScore.Value}");

            if (_rightPlayerScore.Value == ScoreToWin)
                Debug.Log("Right player wins!");
            else
                ResetBall(-1f);
        }

        UpdateScore();
    }

    void UpdateScore()
    {
        rightPlayerScoreText.text = _rightPlayerScore.Value.ToString();
        leftPlayerScoreText.text = _leftPlayerScore.Value.ToString();
    }

    void OnScoreChanged(int previousValue, int newValue) => UpdateScore();

    void ResetBall(float directionSign)
    {
        if (ball == null)
            return;

        // Start the ball within 20 degrees off-center toward direction indicated by directionSign
        directionSign = Mathf.Sign(directionSign);
        Vector3 newVelocity = new Vector3(directionSign, 0f, 0f) * startSpeed;
        newVelocity = Quaternion.Euler(0f, Random.Range(-20f, 20f), 0f) * newVelocity;

        Rigidbody ballRigidbody = ball.GetComponent<Rigidbody>();
        ballRigidbody.position = ballSpawn.position;
        ballRigidbody.linearVelocity = newVelocity;
        ballRigidbody.angularVelocity = Vector3.zero;
    }

    void SpawnBallOnce()
    {
        if (ball != null)
            return;

        GameObject ballObject = Instantiate(ballPrefab, ballSpawn.position, ballSpawn.rotation);
        ball = ballObject.transform;

        if (!ballObject.TryGetComponent(out NetworkObject networkObject))
        {
            Debug.LogError("The ball prefab needs a NetworkObject component.", ballObject);
            return;
        }

        networkObject.Spawn();
    }
}
