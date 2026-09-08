using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/*
 * TestSpawnPositionPlayer is Demo 04's server-assigned placement. The
 * server chooses the first free slot, not OwnerClientId, and replicates
 * that slot. Each peer derives the same position and sets
 * transform.position from it.
 */

public class TestSpawnPositionPlayer : NetworkBehaviour
{
    public string DisplayName => name;
    public bool HasSpawned => IsSpawned;
    public bool IsOwnedByLocalClient => IsOwner;
    public int SpawnSlot => _spawnSlot.Value;
    public string SpawnLabel => GetLabelForSlot(SpawnSlot);
    public Vector3 AssignedPosition => GetPositionForSlot(SpawnSlot);
    public GameObject leftPaddleSpawn;
    public GameObject rightPaddleSpawn;

    const float LeftX = -3f;
    const float RightX = 3f;
    const float StartZ = 0f;
    const float RowSpacing = 2f;
    const float SpawnY = 0.5f;

    readonly NetworkVariable<int> _spawnSlot = new(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        _spawnSlot.OnValueChanged += HandleSpawnSlotChanged;

        name = $"Spawn Player {NetworkObject.OwnerClientId}";
        ApplyOwnerColor();

        if (IsServer)
            AssignSpawnSlot();
        else
            ApplyAssignedPosition();

        Debug.Log(
            $"[SpawnPositions] {name} spawned | ownerClientId={NetworkObject.OwnerClientId} | "
                + $"slot={SpawnSlot} | label={SpawnLabel} | position={AssignedPosition} | "
                + $"isOwner={IsOwner} | isServer={IsServer} | isClient={IsClient} | isHost={IsHost}"
        );
    }

    public override void OnNetworkDespawn()
    {
        _spawnSlot.OnValueChanged -= HandleSpawnSlotChanged;

        Debug.Log(
            $"[SpawnPositions] {name} despawned | ownerClientId={NetworkObject.OwnerClientId} | "
                + $"slot={SpawnSlot} | label={SpawnLabel}"
        );

        base.OnNetworkDespawn();
    }

    Vector3 GetPositionForSlot(int slot)
    {
        if (slot < 0)
            return Vector3.zero;

        int column = slot % 2;
        int row = slot / 2;

        GameObject spawnPoint = column == 0 ? leftPaddleSpawn : rightPaddleSpawn;
        if (spawnPoint != null)
        {
            Vector3 position = spawnPoint.transform.position;
            position.z -= row * RowSpacing;
            return position;
        }

        float x = column == 0 ? LeftX : RightX;
        float z = StartZ - (row * RowSpacing);

        return new Vector3(x, SpawnY, z);
    }

    public static string GetLabelForSlot(int slot)
    {
        if (slot < 0)
            return "pending";

        string side = slot % 2 == 0 ? "left" : "right";
        int row = slot / 2;

        return $"{side} row {row}";
    }

    void AssignSpawnSlot()
    {
        Debug.Assert(IsServer);

        int slot = GetFirstAvailableSpawnSlot();
        _spawnSlot.Value = slot;
        ApplyAssignedPosition();

        Debug.Log(
            $"[SpawnPositions] Server assigned {name} | ownerClientId={NetworkObject.OwnerClientId} | "
                + $"slot={slot} | label={SpawnLabel} | position={transform.position}"
        );
    }

    int GetFirstAvailableSpawnSlot()
    {
        HashSet<int> occupiedSlots = new();

        foreach (
            var player in FindObjectsByType<TestSpawnPositionPlayer>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            )
        )
        {
            if (player == this || !player.HasSpawned)
                continue;
            if (player.SpawnSlot >= 0)
                occupiedSlots.Add(player.SpawnSlot);
        }

        int slot = 0;
        while (occupiedSlots.Contains(slot))
            slot++;

        return slot;
    }

    void HandleSpawnSlotChanged(int previousValue, int newValue)
    {
        ApplyAssignedPosition();

        Debug.Log(
            $"[SpawnPositions] {name} observed slot change | ownerClientId={NetworkObject.OwnerClientId} | "
                + $"slot={newValue} | label={SpawnLabel} | position={transform.position}"
        );
    }

    void ApplyAssignedPosition()
    {
        if (SpawnSlot < 0)
            return;

        GameObject spawnPoint = SpawnSlot % 2 == 0 ? leftPaddleSpawn : rightPaddleSpawn;
        if (spawnPoint != null)
        {
            Vector3 position = AssignedPosition;
            transform.SetPositionAndRotation(position, spawnPoint.transform.rotation);
            return;
        }

        transform.position = AssignedPosition;
    }

    void ApplyOwnerColor()
    {
        if (!TryGetComponent(out Renderer playerRenderer))
            return;

        playerRenderer.material.color = Color.HSVToRGB(
            (NetworkObject.OwnerClientId * 0.17f) % 1f,
            0.75f,
            1f
        );
    }
}
