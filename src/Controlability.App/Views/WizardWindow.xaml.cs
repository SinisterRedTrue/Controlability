using System.ComponentModel;
using Controlability.App.ViewModels;
using Wpf.Ui.Controls;

namespace Controlability.App.Views;

public partial class WizardWindow : FluentWindow
{
    private readonly WizardViewModel _viewModel;
    private bool _finished;

    public WizardWindow(WizardViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        viewModel.Completed += OnCompleted;
    }

    private void OnCompleted()
    {
        _finished = true;
        Close();
    }

    /// <summary>Closing the window with the X counts as skipping: the choices so far are kept and the wizard does not return.</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (!_finished)
        {
            _viewModel.Finish();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Completed -= OnCompleted;
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
