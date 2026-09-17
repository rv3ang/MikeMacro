using Avalonia.Controls;
using Avalonia.Interactivity;
using MikeMacro.Core.Models;

namespace MikeMacro.Desktop;

public partial class ActionDialog : Window
{
    public MacroAction? Result { get; private set; }

    public ActionDialog()
    {
        InitializeComponent();
    }

    private void AddClick(object? sender, RoutedEventArgs args)
    {
        try
        {
            Result = BuildAction(TypeBox.SelectedIndex, ValueBox.Text ?? string.Empty);
            Close(Result);
        }
        catch (Exception error)
        {
            ValueBox.Text = error.Message;
        }
    }

    private void CancelClick(object? sender, RoutedEventArgs args) => Close(null);

    private static MacroAction BuildAction(int type, string value) => type switch
    {
        0 when !string.IsNullOrWhiteSpace(value) => new KeyAction(value.Trim()),
        1 when !string.IsNullOrEmpty(value) => new TextAction(value),
        2 => new MouseButtonAction(ParseMouseButton(value)),
        3 => ParseMouseMove(value),
        4 => new DelayAction(ParseDelay(value)).Validate(),
        _ => throw new ArgumentException("Enter a value for this action.")
    };

    private static MouseButton ParseMouseButton(string value) => value.Trim().ToUpperInvariant() switch
    {
        "LEFT" => MouseButton.Left,
        "RIGHT" => MouseButton.Right,
        "MIDDLE" => MouseButton.Middle,
        "BACK" => MouseButton.Back,
        "FORWARD" => MouseButton.Forward,
        _ => throw new ArgumentException("Mouse button must be Left, Right, Middle, Back, or Forward.")
    };

    private static MouseMoveAction ParseMouseMove(string value)
    {
        var values = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return values.Length == 2 && int.TryParse(values[0], out var x) && int.TryParse(values[1], out var y)
            ? new MouseMoveAction(x, y)
            : throw new ArgumentException("Mouse movement must be entered as X,Y.");
    }

    private static int ParseDelay(string value) => int.TryParse(value.Trim(), out var milliseconds)
        ? milliseconds
        : throw new ArgumentException("Delay must be an integer number of milliseconds.");
}