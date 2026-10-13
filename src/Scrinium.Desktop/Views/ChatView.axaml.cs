using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
        Messages.AddHandler(TappedEvent, OnMessageTapped, RoutingStrategies.Bubble);
    }

    private void Attach()
    {
        if (_subscribed is not null)
            _subscribed.ScrollRequested -= ScrollToEnd;

        _subscribed = DataContext as ChatViewModel;
        if (_subscribed is not null)
            _subscribed.ScrollRequested += ScrollToEnd;
    }

    private void ScrollToEnd() =>
        Dispatcher.UIThread.Post(() => Scroller.ScrollToEnd(), DispatcherPriority.Background);

    private void OnPromptKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            return;

        if (DataContext is ChatViewModel { SendCommand: var send } && send.CanExecute(null))
            send.Execute(null);

        e.Handled = true;
    }

    private void OnMessageTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not ChatViewModel chat || e.Source is not Control source)
            return;

        var message = source.FindAncestorOfType<ContentPresenter>()?.DataContext as ChatMessageViewModel
            ?? source.DataContext as ChatMessageViewModel;

        if (message is not null)
            chat.FocusMessageCommand.Execute(message);
    }
}
