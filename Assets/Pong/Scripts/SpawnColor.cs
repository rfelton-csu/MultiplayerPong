using Unity.Netcode;
using UnityEngine;

public class SpawnColor : NetworkBehaviour
{
    readonly NetworkVariable<Color> _color = new(
        Color.white,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        _color.OnValueChanged += HandleColorChanged;

        if (IsServer)
            _color.Value = Random.ColorHSV(0f, 1f, 0.75f, 1f, 0.8f, 1f);

        ApplyBallColor();
    }

    public override void OnNetworkDespawn()
    {
        _color.OnValueChanged -= HandleColorChanged;
        base.OnNetworkDespawn();
    }

    void HandleColorChanged(Color previousColor, Color newColor)
    {
        ApplyBallColor();
    }

    void ApplyBallColor()
    {
        if (!TryGetComponent(out Renderer ballRenderer))
            return;

        ballRenderer.material.color = _color.Value;
    }
}
