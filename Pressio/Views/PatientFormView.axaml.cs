using Avalonia.Controls;
using Avalonia.Threading;
using Pressio.ViewModels;

namespace Pressio.Views;

public partial class PatientFormView : UserControl
{
    public PatientFormView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is PatientFormViewModel vm)
                vm.Shown += OnShown;
        };
    }

    private void OnShown()
    {
        Dispatcher.UIThread.Post(() =>
        {
            FormScroll.ScrollToHome();
            PatientNameInput.Focus();
        }, DispatcherPriority.Loaded);
    }
}
