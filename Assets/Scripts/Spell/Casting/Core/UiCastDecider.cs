/// <summary>The PC decider: the existing spell menu, choice dialogs and AoO prompt answer for the player.</summary>
public sealed class UiCastDecider : ICastDecider
{
    public static readonly UiCastDecider Instance = new UiCastDecider();

    private UiCastDecider() { }

    public bool IsSynchronous => false;
}
