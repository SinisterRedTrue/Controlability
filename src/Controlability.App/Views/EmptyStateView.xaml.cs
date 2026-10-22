using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace Controlability.App.Views;

/// <summary>A centered icon, title and hint, for pages and lists that have nothing to show yet.</summary>
public partial class EmptyStateView : UserControl
{
    public static readonly DependencyProperty SymbolProperty =
        DependencyProperty.Register(nameof(Symbol), typeof(SymbolRegular), typeof(EmptyStateView), new PropertyMetadata(SymbolRegular.Info24));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(EmptyStateView), new PropertyMetadata(""));

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(EmptyStateView), new PropertyMetadata(""));

    public EmptyStateView()
    {
        InitializeComponent();
    }

    public SymbolRegular Symbol
    {
        get => (SymbolRegular)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }
}
