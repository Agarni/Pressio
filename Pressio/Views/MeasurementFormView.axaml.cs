using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Pressio.Views;

public partial class MeasurementFormView : UserControl
{
    public MeasurementFormView()
    {
        InitializeComponent();
        this.GetObservable(Visual.IsVisibleProperty).Subscribe(OnVisibilityChanged);
    }

    private void OnVisibilityChanged(bool isVisible)
    {
        if (!isVisible)
            return;
        // Espera o controle entrar na árvore visual e ser renderizado antes de focar.
        Dispatcher.UIThread.Post(() => PressureInput.Focus(), DispatcherPriority.Loaded);
    }
}
