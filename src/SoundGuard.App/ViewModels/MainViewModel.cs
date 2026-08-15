using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using SoundGuard.Core.Engine;

namespace SoundGuard.App.ViewModels;

/// <summary>
/// View model for the main window. Holds the meter values, thresholds and status, and exposes
/// commands that <see cref="Services.AppController"/> wires to the engine.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ProtectionConfig _config;

    private double _momentaryLufs = double.NegativeInfinity;
    private double _shortTermLufs = double.NegativeInfinity;
    private double _samplePeakDb = -120;
    private double _truePeakDb = -120;
    private double _gainReductionDb;
    private ProtectionState _state = ProtectionState.Safe;
    private string _dominantStage = "—";
    private bool _isBypass;
    private bool _isGameMode;
    private string _statusText = "安全";
    private Brush _statusBrush = SafeBrush;
    private string _formatText = "—";
    private long _droppedFrames;

    private static readonly SolidColorBrush SafeBrush = new(Color.FromRgb(0x3D, 0xD6, 0x8C));
    private static readonly SolidColorBrush LimitingBrush = new(Color.FromRgb(0xF2, 0xC1, 0x4E));
    private static readonly SolidColorBrush MutedBrush = new(Color.FromRgb(0xF0, 0x65, 0x5A));
    private static readonly SolidColorBrush BypassBrush = new(Color.FromRgb(0x8A, 0x93, 0xA6));

    public MainViewModel(ProtectionConfig config)
    {
        _config = config;
        ToggleBypassCommand = new RelayCommand(_ => { });
        MuteCommand = new RelayCommand(_ => { });
        RecoverCommand = new RelayCommand(_ => { });
        SaveSettingsCommand = new RelayCommand(_ => { });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public double MomentaryLufs { get => _momentaryLufs; private set => Set(ref _momentaryLufs, value); }
    public double ShortTermLufs { get => _shortTermLufs; private set => Set(ref _shortTermLufs, value); }
    public double SamplePeakDb { get => _samplePeakDb; private set => Set(ref _samplePeakDb, value); }
    public double TruePeakDb { get => _truePeakDb; private set => Set(ref _truePeakDb, value); }
    public double GainReductionDb { get => _gainReductionDb; private set => Set(ref _gainReductionDb, value); }
    public string DominantStage { get => _dominantStage; private set => Set(ref _dominantStage, value); }
    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }
    public Brush StatusBrush { get => _statusBrush; private set => Set(ref _statusBrush, value); }
    public string FormatText { get => _formatText; set => Set(ref _formatText, value); }
    public long DroppedFrames { get => _droppedFrames; private set => Set(ref _droppedFrames, value); }

    public bool IsBypass
    {
        get => _isBypass;
        set => Set(ref _isBypass, value);
    }

    public bool IsGameMode
    {
        get => _isGameMode;
        set => Set(ref _isGameMode, value);
    }

    // Thresholds are bound two-way; writing updates the shared config object immediately,
    // and the engine re-reads them every block.
    public double LufsThreshold
    {
        get => _config.LufsThreshold;
        set { _config.LufsThreshold = value; OnPropertyChanged(); }
    }

    public double PeakThresholdDb
    {
        get => _config.PeakThresholdDb;
        set { _config.PeakThresholdDb = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 压限幅度，以百分比展示（0–100）。50% = 中性（全额压限），可下调（更轻）或上调（更强）。
    /// 内部映射到 <see cref="ProtectionConfig.LimiterStrength"/>：factor = percent / 50，范围 0.0–2.0。
    /// </summary>
    public double LimiterStrengthPercent
    {
        get => _config.LimiterStrength * 50.0;
        set { _config.LimiterStrength = Math.Clamp(value / 50.0, 0.0, 2.0); OnPropertyChanged(); }
    }

    public ICommand ToggleBypassCommand { get; set; }
    public ICommand MuteCommand { get; set; }
    public ICommand RecoverCommand { get; set; }
    public ICommand SaveSettingsCommand { get; set; }

    /// <summary>Apply an analysis snapshot (must be called on the UI thread).</summary>
    public void Apply(AnalysisResult r)
    {
        MomentaryLufs = r.MomentaryLufs;
        ShortTermLufs = r.ShortTermLufs;
        SamplePeakDb = r.SamplePeakDb;
        TruePeakDb = r.TruePeakDb;
        GainReductionDb = r.GainReductionDb;
        DominantStage = r.DominantStage == LimiterStage.Peak ? "峰值" :
                        r.DominantStage == LimiterStage.Loudness ? "响度" : "—";
        DroppedFrames = r.DroppedFrames;
        State = r.State;
    }

    /// <summary>Immediately reflect a protection-state transition (e.g. manual mute/recover), independent of the analysis loop.</summary>
    public void SetProtectionState(ProtectionState state) => State = state;

    private ProtectionState State
    {
        set
        {
            if (_state == value) return;
            _state = value;
            OnPropertyChanged();
            (StatusText, StatusBrush) = value switch
            {
                ProtectionState.Safe => ("安全", SafeBrush),
                ProtectionState.Limiting => ("压限中", LimitingBrush),
                ProtectionState.Muted => ("静音保护", MutedBrush),
                ProtectionState.Bypass => ("直通", BypassBrush),
                _ => ("安全", SafeBrush),
            };
        }
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(name);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
