using UnityEngine;

/// <summary>
/// Marks a runtime-created Mirror Image decoy entity.
/// The clone is a targetable battlefield unit that mirrors a real caster.
/// </summary>
public class MirrorImageClone : MonoBehaviour
{
    public CharacterController RealCaster { get; private set; }
    public int CloneIndex { get; private set; }
    public bool IsDissipated { get; private set; }

    public void Initialize(CharacterController realCaster, int cloneIndex)
    {
        RealCaster = realCaster;
        CloneIndex = Mathf.Max(1, cloneIndex);
        IsDissipated = false;
    }

    public void MarkDissipated()
    {
        IsDissipated = true;
    }
}
