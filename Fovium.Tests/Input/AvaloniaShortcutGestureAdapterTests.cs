using Avalonia.Input;
using Fovium.Input;

namespace Fovium.Tests.Input;

public sealed class AvaloniaShortcutGestureAdapterTests
{
    [Theory]
    [InlineData((int)Key.Add, (int)KeyModifiers.None, "Plus")]
    [InlineData((int)Key.OemPlus, (int)KeyModifiers.Shift, "Plus")]
    [InlineData((int)Key.Subtract, (int)KeyModifiers.None, "Minus")]
    [InlineData((int)Key.OemMinus, (int)KeyModifiers.None, "Minus")]
    [InlineData((int)Key.D0, (int)KeyModifiers.None, "0")]
    [InlineData((int)Key.NumPad0, (int)KeyModifiers.None, "0")]
    [InlineData((int)Key.D1, (int)KeyModifiers.None, "1")]
    [InlineData((int)Key.NumPad1, (int)KeyModifiers.None, "1")]
    public void MainAndNumpadKeysNormalizeToStableIdentity(
        int keyValue,
        int modifierValue,
        string expectedKey)
    {
        var accepted = AvaloniaShortcutGestureAdapter.TryCreate(
            (Key)keyValue,
            (KeyModifiers)modifierValue,
            out var gesture);

        Assert.True(accepted);
        Assert.Equal(expectedKey, gesture.Key);
        Assert.Equal(ShortcutModifiers.None, gesture.Modifiers);
    }

    [Fact]
    public void EscapeIsNotRepresentableAsConfigurableGesture()
    {
        Assert.False(AvaloniaShortcutGestureAdapter.TryCreate(Key.Escape, KeyModifiers.None, out _));
    }

    [Theory]
    [InlineData((int)Key.Z, "Z")]
    [InlineData((int)Key.C, "C")]
    [InlineData((int)Key.NumPad1, "1")]
    public void PrimaryKeyIdentityDoesNotDependOnModifierState(int keyValue, string expected)
    {
        Assert.True(AvaloniaShortcutGestureAdapter.TryGetPrimaryKey((Key)keyValue, out var primaryKey));
        Assert.Equal(expected, primaryKey);
    }
}
