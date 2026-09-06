using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
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

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        var inputPane = TopLevel.GetTopLevel(this)?.InputPane;
        if (inputPane is not null) inputPane.StateChanged += OnInputPaneChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        var inputPane = TopLevel.GetTopLevel(this)?.InputPane;
        if (inputPane is not null) inputPane.StateChanged -= OnInputPaneChanged;
    }

    // Quando o teclado (input pane) abre, reduz a área do ScrollViewer e rola o campo focado
    // para que não fique coberto pelo teclado (Avalonia não ajusta isso automaticamente).
    private void OnInputPaneChanged(object? sender, InputPaneStateEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (e.NewState == InputPaneState.Open)
            {
                var occluded = Math.Max(0, e.EndRect.Height);
                FormScroll.Margin = new Thickness(0, 0, 0, occluded);
                if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Visual v)
                {
                    var top = v.TranslatePoint(default, FormScroll)?.Y ?? 0;
                    var height = (v as Control)?.Bounds.Height ?? 0;
                    var visible = Math.Max(0, FormScroll.Viewport.Height - occluded);
                    var overshoot = top + height - visible;
                    if (overshoot > 0) FormScroll.Offset = new Vector(0, FormScroll.Offset.Y + overshoot);
                }
            }
            else
            {
                FormScroll.Margin = new Thickness(0);
            }
        }, DispatcherPriority.Loaded);
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
