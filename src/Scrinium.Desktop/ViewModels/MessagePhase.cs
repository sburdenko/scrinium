using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Scrinium.Core.Chat;
using Scrinium.Core.Retrieval;

namespace Scrinium.Desktop.ViewModels;

public enum MessagePhase
{
    Searching,
    Composing,
    Writing,
    Done,
    Stopped,
    Failed
}
