using Avalonia.Controls;
using Avalonia.Threading;
using Pressio.ViewModels;

namespace Pressio.Views;

public partial class MeasurementFormView : UserControl
{
    public MeasurementFormView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MeasurementFormViewModel vm)
            {
                vm.Shown -= OnShown;
                vm.Shown += OnShown;
                // Na 1ª abertura a View é criada após o comando disparar `Shown`;
                // chamamos aqui para cobrir esse caso.
                OnShown();
            }
        };
    }

    private void OnShown()
    {
        // A cada abertura: volta ao topo e foca o campo de pressão
        // (o ScrollViewer preserva o offset entre abrir/fechar).
        Dispatcher.UIThread.Post(() =>
        {
            FormScroll.ScrollToHome();
            PressureInput.Focus();
        }, DispatcherPriority.Loaded);
    }
}
