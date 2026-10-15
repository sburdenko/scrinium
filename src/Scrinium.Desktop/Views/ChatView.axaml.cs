using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Scrinium.Desktop.ViewModels;

namespace Scrinium.Desktop.Views;

public partial class ChatView : UserControl
{
    private ChatViewModel? _subscribed;

    public ChatView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
        Prompt.AddHandler(KeyDownEvent, OnPromptKeyDown, RoutingStrategies.Tunnel);
    }

    private void Attach()
    {
        if (_subscribed is not null)
        {
            _subscribed.ScrollRequested -= ScrollToEnd;
            _subscribed.FocusComposerRequested -= FocusComposer;
        }

        _subscribed = DataContext as ChatViewModel;
        if (_subscribed is not null)
        {
            _subscribed.ScrollRequested += ScrollToEnd;
            _subscribed.FocusComposerRequested += FocusComposer;
        }
    }

    private void ScrollToEnd() =>
        Dispatcher.UIThread.Post(() => Scroller.ScrollToEnd(), DispatcherPriority.Background);

    private void FocusComposer() =>
        Dispatcher.UIThread.Post(() => Prompt.Focus(), DispatcherPriority.Background);

    private void OnPromptKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            return;

        if (DataContext is ChatViewModel { SendCommand: var send } && send.CanExecute(null))
            send.Execute(null);

        e.Handled = true;
    }
}
