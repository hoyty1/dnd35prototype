using System;
using DND35e.Identifiers;

/// <summary>
/// Runtime metadata attached to an Entangled condition created by Web.
/// Tracks escape DC/source so UI and AI can route escape actions correctly.
/// </summary>
[Serializable]
public sealed class WebEntangledConditionData
{
    public CharacterController Caster;
    public CharacterController Target;
    public int EscapeDC = 20;
    public string SourceSpellId = SpellNames.WEB;
    public string SourceSpellName = "Web";
}
