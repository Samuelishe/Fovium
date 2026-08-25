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

    [Fact]
    public void ControlDeleteRoundTripsAsClearMarkupGesture()
    {
        Assert.True(AvaloniaShortcutGestureAdapter.TryCreate(
            Key.Delete,
            KeyModifiers.Control,
            out var gesture));

        Assert.Equal(new ShortcutGesture("Delete", ShortcutModifiers.Control), gesture);
        var avalonia = Assert.IsType<KeyGesture>(AvaloniaShortcutGestureAdapter.ToAvalonia(gesture));
        Assert.Equal(Key.Delete, avalonia.Key);
        Assert.Equal(KeyModifiers.Control, avalonia.KeyModifiers);
    }

    [Theory]
    [InlineData((int)Key.OemOpenBrackets, (int)KeyModifiers.None, "OpenBracket")]
    [InlineData((int)Key.OemCloseBrackets, (int)KeyModifiers.None, "CloseBracket")]
    [InlineData((int)Key.OemOpenBrackets, (int)KeyModifiers.Control, "OpenBracket")]
    [InlineData((int)Key.OemCloseBrackets, (int)KeyModifiers.Control, "CloseBracket")]
    public void PhysicalOemBracketKeysNormalizeWithoutProducedCharacter(
        int keyValue,
        int modifiersValue,
        string expectedKey)
    {
        Assert.True(AvaloniaShortcutGestureAdapter.TryCreate(
            (Key)keyValue,
            (KeyModifiers)modifiersValue,
            out var gesture));

        Assert.Equal(expectedKey, gesture.Key);
        var expectedModifiers = ((KeyModifiers)modifiersValue).HasFlag(KeyModifiers.Control)
            ? ShortcutModifiers.Control
            : ShortcutModifiers.None;
        Assert.Equal(expectedModifiers, gesture.Modifiers);
        var roundTrip = Assert.IsType<KeyGesture>(AvaloniaShortcutGestureAdapter.ToAvalonia(gesture));
        Assert.Equal((Key)keyValue, roundTrip.Key);
        Assert.Equal((KeyModifiers)modifiersValue, roundTrip.KeyModifiers);
    }

    [Fact]
    public void BracketGesturesFormatAsFamiliarSymbols()
    {
        Assert.Equal("[", ShortcutGestureFormatter.Format(new ShortcutGesture("OpenBracket"), "none"));
        Assert.Equal(
            "Ctrl+]",
            ShortcutGestureFormatter.Format(
                new ShortcutGesture("CloseBracket", ShortcutModifiers.Control),
                "none"));
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
