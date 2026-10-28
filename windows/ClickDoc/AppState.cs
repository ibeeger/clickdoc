using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;

namespace ClickDoc;

/// The current tutorial and app-wide state. Lives on the UI thread.
sealed class AppState : INotifyPropertyChanged
{
    readonly DraftStore _store = new(AppPaths.Draft);
    readonly Settings _settings = Settings.Load();
    readonly DispatcherTimer _saveTimer;
    /// Draft writes run one after another off the UI thread.
    Task _saveChain = Task.CompletedTask;
    bool _isRecording, _hasUnexportedChanges;
    string? _lastError;

    public AppState(Dispatcher dispatcher)
    {
        Dispatcher = dispatcher;
        _saveTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(400), DispatcherPriority.Background, (_, _) => SaveSoon(), dispatcher);
        _saveTimer.Stop();
        RestoreDraft();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// Raised after any change to the tutorial's content, order or title.
    public event Action? TutorialChanged;

    public event Action? ShowEditorRequested;

    public Dispatcher Dispatcher { get; }

    public Tutorial Tutorial { get; private set; } = new();

    public bool IsRecording { get => _isRecording; set => Set(ref _isRecording, value); }

    public bool HasUnexportedChanges { get => _hasUnexportedChanges; set => Set(ref _hasUnexportedChanges, value); }

    public string? LastError { get => _lastError; set => Set(ref _lastError, value); }

    public CaptureScope Scope
    {
        get => _settings.Scope;
        set
        {
            if (_settings.Scope == value) return;
            _settings.Scope = value;
            _settings.Save();
            Raise();
        }
    }

    /// Set by App once the recorder exists.
    public Recording.Recorder? Recorder { get; set; }

    /// Marks the tutorial edited: it now has unexported changes and is saved shortly.
    public void Touch()
    {
        HasUnexportedChanges = true;
        Changed();
    }

    /// Notifies views and schedules a draft save without marking the tutorial dirty.
    public void Changed()
    {
        TutorialChanged?.Invoke();
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void ShowEditor() => ShowEditorRequested?.Invoke();

    public void StartRecording()
    {
        if (IsRecording || Recorder is null) return;
        if (!Recorder.Start()) return;
        LastError = null;
        IsRecording = true;
    }

    /// `fromTray`: stopped from the tray menu, so trailing taskbar clicks are dropped.
    public async void StopRecording(bool fromTray = false)
    {
        if (!IsRecording) return;
        if (Recorder is not null) await Recorder.Stop(fromTray);
        IsRecording = false;
        ShowEditor();
    }

    public void NewTutorial()
    {
        if (HasUnexportedChanges && !Tutorial.IsEmpty)
        {
            var answer = MessageBox.Show(L("当前教程尚未导出，新建将丢失这些步骤。确定继续？"), "ClickDoc",
                                         MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);
            if (answer != MessageBoxResult.OK) return;
        }
        Tutorial = new Tutorial();
        HasUnexportedChanges = false;
        Recorder?.ForgetLastClick();
        Changed();
    }

    /// Writes the draft now, waiting for queued saves; used on exit and logoff.
    public void FlushSave()
    {
        _saveTimer.Stop();
        try
        {
            _saveChain.Wait();
            _store.Save(Tutorial.Snapshot(), HasUnexportedChanges);
        }
        catch (Exception) { /* nothing left to report to on exit */ }
    }

    void SaveSoon()
    {
        _saveTimer.Stop();
        var snapshot = Tutorial.Snapshot();
        bool dirty = HasUnexportedChanges;
        _saveChain = _saveChain.ContinueWith(_ =>
        {
            try { _store.Save(snapshot, dirty); }
            catch (Exception e)
            {
                Dispatcher.BeginInvoke(() => LastError = L("自动保存草稿失败：%@", e.Message));
            }
        }, TaskScheduler.Default);
    }

    void RestoreDraft()
    {
        try
        {
            if (_store.Load() is { } draft)
            {
                Tutorial = draft.Tutorial;
                HasUnexportedChanges = draft.HasUnexportedChanges;
            }
        }
        catch (Exception)
        {
            // Keep the unreadable draft for manual recovery instead of overwriting it.
            try { LastError = L("上次的草稿无法读取，已另存到 %@", _store.MoveAside(DateTimeOffset.Now)); }
            catch (IOException) { LastError = L("上次的草稿无法读取，已另存到 %@", _store.Directory); }
        }
    }

    void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Raise(name);
    }

    void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
