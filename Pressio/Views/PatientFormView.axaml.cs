using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Pressio.Views;

public partial class PatientFormView : UserControl
{
    public PatientFormView()
    {
        InitializeComponent();
        this.GetObservable(Visual.IsVisibleProperty).Subscribe(OnVisibilityChanged);
    }

    private void OnVisibilityChanged(bool isVisible)
    {
        if (!isVisible)
            return;
        Dispatcher.UIThread.Post(() => PatientNameInput.Focus(), DispatcherPriority.Loaded);
    }
}
