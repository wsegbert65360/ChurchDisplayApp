using System.Windows;

namespace ChurchDisplayApp;

public partial class MonitorSelectionWindow : Window
{
    public enum SelectionResult
    {
        Left,
        Right
    }

    public SelectionResult Result { get; private set; } = SelectionResult.Right;

    public MonitorSelectionWindow()
    {
        InitializeComponent();
    }

    private void LeftButton_Click(object sender, RoutedEventArgs e)
    {
        Result = SelectionResult.Left;
        DialogResult = true;
        Close();
    }

    private void RightButton_Click(object sender, RoutedEventArgs e)
    {
        Result = SelectionResult.Right;
        DialogResult = true;
        Close();
    }
}
