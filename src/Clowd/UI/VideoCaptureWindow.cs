﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Clowd.Config;
using Clowd.PlatformUtil;
using Clowd.UI.Controls;
using Clowd.UI.Helpers;
using Clowd.UI.Unmanaged;
using Clowd.Util;
using Clowd.Video;
using Clowd.Video.FFmpeg;
using NLog;

namespace Clowd.UI
{


    internal sealed class VideoCaptureWindow : IVideoCapturePage
    {
        public event EventHandler Closed;
        public bool IsRecording { get; private set; }

        private CaptureToolButton _btnClowd;
        private CaptureToolButton _btnReload;
        private CaptureToolButton _btnStart;
        private CaptureToolButton _btnStop;
        private CaptureToolButton _btnMicrophone;
        private CaptureToolButton _btnSpeaker;
        private CaptureToolButton _btnOutput;
        private CaptureToolButton _btnSettings;
        private CaptureToolButton _btnDraw;
        private CaptureToolButton _btnCancel;

        private bool _opened;
        private bool _disposed;
        private bool _hasStarted;
        private bool _obsStarting;
        private bool _obsValid;

        private ScreenRect _selection;
        private ObsCapturer _capturer;
        private SettingsVideo _settings = SettingsRoot.Current.Video;
        private string _fileName;
        private FloatingButtonWindow _floating;

        private IAudioLevelListener _speakerLevel;
        private IAudioLevelListener _microphoneLevel;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public VideoCaptureWindow()
        {
            if (!Directory.Exists(_settings.OutputDirectory))
                _settings.OutputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

            _fileName = Path.Combine(_settings.OutputDirectory, PathConstants.GetDatedFileName("recording", "mp4"));

            _btnClowd = new CaptureToolButton
            {
                Primary = true,
                Text = "WAIT",
                IconPath = AppStyles.GetIconElement(ResourceIcon.IconToolNone),
                IsDragHandle = true,
            };

            _btnReload = new CaptureToolButton
            {
                Primary = true,
                Text = "Reload",
                IconPath = AppStyles.GetIconElement(ResourceIcon.IconUndo),
                Executed = (s, e) => StartObs(),
                Visibility = Visibility.Collapsed,
            };

            _btnStart = new CaptureToolButton
            {
                Text = "Start",
                IconPath = AppStyles.GetIconElement(ResourceIcon.IconPlay),
                Executed = (s, e) => StartRecording(),
                IsEnabled = false,
            };

            _btnStop = new CaptureToolButton
            {
                Primary = true,
                Text = "Finish",
                IconPath = AppStyles.GetIconElement(ResourceIcon.IconStop),
                Executed = (s, e) => StopRecording(),
                Visibility = Visibility.Collapsed,
            };

            _btnMicrophone = new CaptureToolButton
            {
                IconPath = AppStyles.GetIconElement(ResourceIcon.IconMicrophoneDisabled),
                IconPathAlternate = AppStyles.GetIconElement(ResourceIcon.IconMicrophoneEnabled),
                Executed = OnMicrophoneToggle,
                Text = "Mic",
            };
            _btnMicrophone.SetBinding(
                CaptureToolButton.ShowAlternateIconProperty,
                new Binding(nameof(SettingsVideo.CaptureMicrophone)) { Source = _settings, Mode = BindingMode.OneWay });

            _btnSpeaker = new CaptureToolButton
            {
                IconPath = AppStyles.GetIconElement(ResourceIcon.IconSpeakerDisabled),
                IconPathAlternate = AppStyles.GetIconElement(ResourceIcon.IconSpeakerEnabled),
                Executed = OnSpeakerToggle,
                Text = "Spk",
            };
            _btnSpeaker.SetBinding(
                CaptureToolButton.ShowAlternateIconProperty,
                new Binding(nameof(SettingsVideo.CaptureSpeaker)) { Source = _settings, Mode = BindingMode.OneWay });

            _btnOutput = new CaptureToolButton
            {
                Text = "Output",
                Executed = OnChangeOutput,
                IconPath = _settings.OutputMode switch
                {
                    // VideoOutputType.MKV => AppStyles.GetIconElement(ResourceIcon.IconVideoMKV),
                    VideoOutputType.MP4 => AppStyles.GetIconElement(ResourceIcon.IconVideoMP4),
                    VideoOutputType.GIF => AppStyles.GetIconElement(ResourceIcon.IconVideoGIF),
                    _ => throw new ArgumentOutOfRangeException()
                },
            };

            _btnSettings = new CaptureToolButton
            {
                Text = "Settings",
                IconPath = AppStyles.GetIconElement(ResourceIcon.IconSettings),
                Executed = OnSettings,
            };

            _btnDraw = new CaptureToolButton
            {
                Text = "Draw",
                IconPath = AppStyles.GetIconElement(ResourceIcon.IconDrawing),
                Executed = OnDraw,
            };

            _btnCancel = new CaptureToolButton
            {
                Text = "Cancel",
                IconPath = AppStyles.GetIconElement(ResourceIcon.IconClose),
                Executed = OnCancel,
            };

            _floating = FloatingButtonWindow.Create(
                new[] { _btnClowd, _btnReload, _btnStart, _btnStop, _btnMicrophone, _btnSpeaker, _btnOutput, _btnSettings, _btnDraw, _btnCancel });

            _settings.PropertyChanged += SettingChanged;
        }

        private async void StartObs()
        {
            if (_hasStarted || _obsStarting || !_opened) return;
            _obsStarting = _obsValid = true;

            _btnReload.Visibility = Visibility.Collapsed;
            _btnStart.Visibility = Visibility.Visible;
            _btnStart.IsEnabled = false;
            _btnStart.PulseBackground = false;
            _btnStart.Primary = false;
            _btnClowd.Text = "WAIT";

            BorderWindow.SetText("Please Wait");

            if (_capturer != null)
            {
                await _capturer.DisposeAsync();
                _capturer = null;
            }

            try
            {
                var capturer = new ObsCapturer();
                capturer.CriticalError += SynchronizationContextEventHandler.CreateDelegate<VideoCriticalErrorEventArgs>(CapturerCriticalError);
                capturer.StatusReceived += SynchronizationContextEventHandler.CreateDelegate<VideoStatusEventArgs>(CapturerStatusReceived);
                await capturer.Initialize(_fileName, _selection, _settings);

                if (_obsValid)
                {
                    _capturer = capturer;
                    _btnStart.IsEnabled = true;
                    _btnStart.PulseBackground = true;
                    _btnStart.Primary = true;
                    _btnClowd.Text = "READY";
                    BorderWindow.SetText("Press Start");
                }
                else
                {
                    await capturer.DisposeAsync();
                }

                _obsStarting = false;
            }
            catch (Exception ex)
            {
                CapturerCriticalError(this, new VideoCriticalErrorEventArgs(ex.ToString()));
            }
        }

        private async void InvalidateObs()
        {
            if (_hasStarted || !_opened) return;

            _obsValid = false;
            _btnReload.Visibility = Visibility.Visible;
            _btnStart.Visibility = Visibility.Collapsed;
            _btnClowd.Text = "Clowd";

            BorderWindow.SetText("Reload");

            if (_capturer != null)
            {
                await _capturer.DisposeAsync();
                _capturer = null;
            }
        }

        private async void CapturerCriticalError(object sender, VideoCriticalErrorEventArgs e)
        {
            this.Close();

            var capt = (sender as ObsCapturer) ?? _capturer;
            if (capt != null)
            {
                try
                {
                    var filename = "capture_error_log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
                    if (_settings.OutputDirectory != null && Directory.Exists(_settings.OutputDirectory))
                    {
                        filename = Path.Combine(Path.GetFullPath(_settings.OutputDirectory), filename);
                    }
                    else
                    {
                        filename = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), filename);
                    }

                    capt.WriteLogToFile(filename);
                    File.AppendAllText(filename, Environment.NewLine + "--" + Environment.NewLine + e.Error);

                    _log.ForErrorEvent()
                        .Message("The capturer has crashed.")
                        .Property<string>("obs.log", File.ReadAllText(filename))
                        .Log();

                    if (await NiceDialog.ShowPromptAsync(null,
                            NiceDialogIcon.Error,
                            e.Error + Environment.NewLine + "A log file has been created in your video output directory for more information.",
                            "An unexpected error was encountered while recording.",
                            "Open Error Log"))
                    {
                        Process.Start("notepad.exe", filename);
                    }

                    return;
                }
                catch { }
            }

            // if we reached here, we were unable to write a log file
            _log.Error(e.Error);

            NiceDialog.ShowNoticeAsync(null,
                NiceDialogIcon.Error,
                e.Error,
                "An unexpected error was encountered while recording.");
        }

        private void CapturerStatusReceived(object sender, VideoStatusEventArgs e)
        {
            if (e.AvgFps != 0 && (e.TotalTime == default(TimeSpan) || DateTime.Now.Ticks / (4 * TimeSpan.TicksPerSecond) % 2 == 0))
            {
                _btnClowd.Text = e.AvgFps + " FPS";
            }
            else if (e.TotalTime != default(TimeSpan))
            {
                _btnClowd.Text = $"{((int)e.TotalTime.TotalMinutes):D2}:{((int)e.TotalTime.Seconds):D2}";
            }
        }

        public void Open(ScreenRect captureArea)
        {
            if (_opened || _disposed)
                throw new InvalidOperationException("Video capture can only be opened once");

            _opened = true;
            _selection = captureArea;

            RefreshListeners();
            BorderWindow.Show(AppStyles.AccentColor, captureArea);

            _floating.ShowPanel(captureArea);
            Task.Delay(100).ContinueWith((t) => { StartObs(); }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        public async Task StartRecording()
        {
            if (_disposed)
                throw new ObjectDisposedException("This object is disposed.");

            if (_hasStarted)
                throw new InvalidOperationException("StartRecording can only be called once");

            _hasStarted = true;
            _btnStart.IsEnabled = false;

            BorderWindow.SetText(null);
            _btnClowd.Text = "Starting";

            try
            {
                _capturer.SetMicrophoneMute(!_settings.CaptureMicrophone);
                _capturer.SetSpeakerMute(!_settings.CaptureSpeaker);
                await _capturer.StartAsync();
                IsRecording = true;
            }
            catch (Exception ex)
            {
                CapturerCriticalError(this, new VideoCriticalErrorEventArgs(ex.ToString()));
            }

            _btnClowd.Text = "Started";
            _btnClowd.IconPath = AppStyles.GetIconElement(ResourceIcon.IconClowd);
            _btnStart.Visibility = Visibility.Collapsed;
            _btnStop.Visibility = Visibility.Visible;
        }

        public async Task StopRecording()
        {
            if (_disposed)
                throw new ObjectDisposedException("This object is disposed.");

            var wasRecording = IsRecording;

            BorderWindow.Hide();
            _floating.Hide();

            if (IsRecording)
            {
                IsRecording = false;
                await _capturer.StopAsync();
            }

            this.Close();

            if (wasRecording)
            {
                try
                {
                    // move file to user's desired location/name
                    var pattern = String.IsNullOrWhiteSpace(_settings.FilenamePattern) ? "yyyy-MM-dd HH-mm-ss" : Path.GetFileNameWithoutExtension(_settings.FilenamePattern);
                    var newPath = Path.Combine(_settings.OutputDirectory, PathConstants.GetFreePatternFileName(_settings.OutputDirectory, pattern)) + ".mp4";
                    File.Move(_fileName, newPath);
                    _fileName = newPath;
                }
                catch (Exception ex)
                {
                    await NiceDialog.ShowNoticeAsync(null, NiceDialogIcon.Error,
                        $"It was saved to {_fileName} instead.{Environment.NewLine}{Environment.NewLine}Error:{Environment.NewLine}{ex.Message}",
                        "Unable to save video to desired location.");
                }

                if (_settings.OutputMode == VideoOutputType.GIF)
                {
                    _fileName = await EncodeGif(_fileName);
                }

                if (_settings.OpenFinishedInExplorer)
                {
                    // this method of selecting a file will re-use an existing windows explorer window instead of opening a new one
                    Platform.Current.RevealFileOrFolder(File.Exists(_fileName) ? _fileName : _settings.OutputDirectory);
                }
            }
        }

        private string GetNewVideoName()
        {
            if (String.IsNullOrWhiteSpace(_settings.OutputDirectory))
                throw new Exception("OutputDirectory must not be null");

            if (!Directory.Exists(_settings.OutputDirectory))
                Directory.CreateDirectory(_settings.OutputDirectory);

            var pattern = String.IsNullOrWhiteSpace(_settings.FilenamePattern) ? "yyyy-MM-dd HH-mm-ss" : Path.GetFileNameWithoutExtension(_settings.FilenamePattern);
            return Path.Combine(_settings.OutputDirectory, PathConstants.GetFreePatternFileName(_settings.OutputDirectory, pattern)) + ".mp4";
        }

        private void SettingChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_disposed) return;

            if (e.PropertyName is nameof(SettingsVideo.OpenFinishedInExplorer) or nameof(SettingsVideo.FilenamePattern) or nameof(SettingsVideo.OutputDirectory))
            {
                // do nothing
            }
            else if (e.PropertyName is nameof(SettingsVideo.OutputMode))
            {
                UpdateOutputIcon();
            }
            else if (e.PropertyName is nameof(SettingsVideo.CaptureSpeakerDevice) or nameof(SettingsVideo.CaptureMicrophoneDevice))
            {
                RefreshListeners();
            }
            else if (e.PropertyName is nameof(SettingsVideo.CaptureSpeaker) or nameof(SettingsVideo.CaptureMicrophone))
            {
                _capturer?.SetMicrophoneMute(!_settings.CaptureMicrophone);
                _capturer?.SetSpeakerMute(!_settings.CaptureSpeaker);
            }
            else
            {
                InvalidateObs();
            }
        }

        private void RefreshListeners()
        {
            if (_disposed) return;

            if (_settings.CaptureMicrophoneDevice == null || !_settings.CaptureMicrophone)
            {
                _microphoneLevel?.Dispose();
                _microphoneLevel = null;
            }
            else if (_settings.CaptureMicrophoneDevice != _microphoneLevel?.Device)
            {
                _microphoneLevel?.Dispose();
                _microphoneLevel = AudioDeviceManager.GetAudioListener(_settings.CaptureMicrophoneDevice);
                _btnMicrophone.Overlay = GetLevelVisual(_microphoneLevel, nameof(_settings.CaptureMicrophone));
            }

            if (_settings.CaptureSpeakerDevice == null || !_settings.CaptureSpeaker)
            {
                _speakerLevel?.Dispose();
                _speakerLevel = null;
            }
            else if (_settings.CaptureSpeakerDevice != _speakerLevel?.Device)
            {
                _speakerLevel?.Dispose();
                _speakerLevel = AudioDeviceManager.GetAudioListener(_settings.CaptureSpeakerDevice);
                _btnSpeaker.Overlay = GetLevelVisual(_speakerLevel, nameof(_settings.CaptureSpeaker));
            }
        }

        private ProgressBar GetLevelVisual(IAudioLevelListener listener, string enabledPath)
        {
            var prog = new ProgressBar { Style = AppStyles.AudioLevelProgressBarStyle };

            var valueBinding = new Binding(nameof(IAudioLevelListener.PeakLevel));
            valueBinding.Source = listener;
            valueBinding.Mode = BindingMode.OneWay;
            prog.SetBinding(ProgressBar.ValueProperty, valueBinding);

            var visibilityBinding = new Binding(enabledPath);
            visibilityBinding.Source = _settings;
            visibilityBinding.Mode = BindingMode.OneWay;
            visibilityBinding.Converter = new Converters.BoolToVisibilityConverter2();
            prog.SetBinding(ProgressBar.VisibilityProperty, visibilityBinding);

            return prog;
        }

        private Task<string> EncodeGif(string filePath)
        {
            return Task.Run(() =>
            {
                var task = PageManager.Current.Tasks.CreateTask($"Encode GIF ({Path.GetFileName(filePath)})");
                task.SetStatus("Preparing...");

                var ffmpeg = new FFMpegConverter();
                ffmpeg.ConvertProgress += (s, e) =>
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        task.SetProgress((int)e.Processed.TotalSeconds, (int)e.TotalDuration.TotalSeconds, false);
                    });
                };

                task.Show();

                // ffmpeg -ss 30 -t 3 -i input.mp4 -vf "fps=10,scale=320:-1:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse" -loop 0 output.gif
                var gifPath = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + ".gif");
                ffmpeg.Invoke($"-i \"{filePath}\" -vf \"fps=15\" \"{gifPath}\"");

                task.Hide();
                return gifPath;
            });
        }

        private void OnChangeOutput(object sender, EventArgs e)
        {
            switch (_settings.OutputMode)
            {
                case VideoOutputType.MP4:
                    _settings.OutputMode = VideoOutputType.GIF;
                    break;
                default:
                    _settings.OutputMode = VideoOutputType.MP4;
                    break;
            }
            UpdateOutputIcon();
        }

        private void UpdateOutputIcon()
        {
            switch (_settings.OutputMode)
            {
                case VideoOutputType.GIF:
                    _btnOutput.IconPath = AppStyles.GetIconElement(ResourceIcon.IconVideoGIF);
                    break;
                default:
                    _btnOutput.IconPath = AppStyles.GetIconElement(ResourceIcon.IconVideoMP4);
                    break;
            }
        }

        private void OnSpeakerToggle(object sender, EventArgs e)
        {
            _settings.CaptureSpeaker = !_settings.CaptureSpeaker;
        }

        private void OnMicrophoneToggle(object sender, EventArgs e)
        {
            _settings.CaptureMicrophone = !_settings.CaptureMicrophone;
        }

        private void OnSettings(object sender, EventArgs e)
        {
            PageManager.Current.GetSettingsPage().Open(SettingsPageTab.SettingsVideo);
        }

        private void OnDraw(object sender, EventArgs e)
        {
            PageManager.Current.GetLiveDrawPage().Open();
        }

        private async void OnCancel(object sender, EventArgs e)
        {
            BorderWindow.Hide();
            _floating.Hide();
            if (IsRecording)
            {
                IsRecording = false;
                await _capturer.StopAsync();
            }

            this.Close();

            await Task.Delay(4 * 1000);
            if (File.Exists(_fileName))
                File.Delete(_fileName);
        }

        public void Close()
        {
            if (_disposed)
                return;

            _disposed = true;
            _settings.PropertyChanged -= SettingChanged;
            BorderWindow.Hide();
            _speakerLevel?.Dispose();
            _microphoneLevel?.Dispose();
            _floating.Close();
            _capturer?.Dispose();
            Closed?.Invoke(this, new EventArgs());
        }
    }
}
