using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ChurchDisplayApp.Services;

/// <summary>
/// Manages window layout persistence and grid splitter operations.
/// Stores layout as proportions (0.0-1.0) rather than fixed pixels for proper responsive behavior.
/// </summary>
public class LayoutManager
{
    private readonly Grid _mainGrid;
    private readonly AppSettings _settings;

    public LayoutManager(Grid mainGrid, AppSettings settings)
    {
        _mainGrid = mainGrid ?? throw new ArgumentNullException(nameof(mainGrid));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Applies saved layout proportions to the grid.
    /// Uses proportional values to maintain responsive behavior across different window sizes.
    /// </summary>
    public void ApplyLayout()
    {
        try
        {
            // Apply column proportions
            if (_settings.MainWindowLeftColumnProportion.HasValue)
            {
                var proportion = Math.Clamp(_settings.MainWindowLeftColumnProportion.Value, 0.3, 0.8);
                var leftColumn = _mainGrid.ColumnDefinitions[0];
                var rightColumn = _mainGrid.ColumnDefinitions[2];
                
                // Set as star values to maintain proportional resizing
                leftColumn.Width = new GridLength(proportion, GridUnitType.Star);
                rightColumn.Width = new GridLength(1 - proportion, GridUnitType.Star);
            }

            // Apply row proportions
            if (_settings.MainWindowTopRowProportion.HasValue)
            {
                var proportion = Math.Clamp(_settings.MainWindowTopRowProportion.Value, 0.3, 0.8);
                var topRow = _mainGrid.RowDefinitions[0];
                
                // Top row uses star sizing for flexibility
                topRow.Height = new GridLength(proportion, GridUnitType.Star);
            }
        }
        catch (Exception ex)
        {
            // Log error but don't crash - use default layout
            System.Diagnostics.Debug.WriteLine($"Error applying layout: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves current layout proportions to settings.
    /// Calculates proportions based on actual widths/heights to preserve responsive behavior.
    /// </summary>
    public void SaveLayout()
    {
        try
        {
            // Calculate column proportions
            var totalColumnWidth = _mainGrid.ColumnDefinitions[0].ActualWidth + 
                                   _mainGrid.ColumnDefinitions[2].ActualWidth;
            
            if (totalColumnWidth > 0)
            {
                var leftProportion = _mainGrid.ColumnDefinitions[0].ActualWidth / totalColumnWidth;
                _settings.MainWindowLeftColumnProportion = leftProportion;
            }

            // Calculate row proportions
            var totalRowHeight = _mainGrid.RowDefinitions[0].ActualHeight;
            var windowContentHeight = _mainGrid.ActualHeight;
            
            if (windowContentHeight > 0)
            {
                var topProportion = totalRowHeight / windowContentHeight;
                _settings.MainWindowTopRowProportion = topProportion;
            }

            _settings.Save();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving layout: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles GridSplitter drag completion events.
    /// </summary>
    public void OnSplitterDragCompleted(object sender, DragCompletedEventArgs e)
    {
        SaveLayout();
    }
}
