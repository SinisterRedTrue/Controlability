using System.Windows.Controls;
using Controlability.App.ViewModels;

namespace Controlability.App.Views;

public partial class DevicesPage : Page
{
    public DevicesPage(DevicesViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        Loaded += (_, _) => viewModel.Refresh();
    }
}
