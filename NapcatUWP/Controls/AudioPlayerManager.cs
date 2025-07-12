using System;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.ApplicationModel.Core;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Core;

namespace NapcatUWP.Controls
{
    /// <summary>
    ///     音頻播放事件參數
    /// </summary>
    public class AudioPlayEventArgs : EventArgs
    {
        public AudioPlayEventArgs(string audioUrl, string title = "音頻", bool isPlaying = false)
        {
            AudioUrl = audioUrl;
            Title = title;
            IsPlaying = isPlaying;
        }

        public string AudioUrl { get; }
        public string Title { get; }
        public bool IsPlaying { get; }
    }

    /// <summary>
    ///     音頻播放管理器 - 單例模式
    /// </summary>
    public class AudioPlayerManager
    {
        private static AudioPlayerManager _instance;
        private static readonly object _lock = new object();
        private readonly Dictionary<string, bool> _audioStates = new Dictionary<string, bool>();
        private string _currentAudioUrl;
        private bool _isPlaying;

        private MediaPlayer _mediaPlayer;

        private AudioPlayerManager()
        {
            InitializeMediaPlayer();
        }

        public static AudioPlayerManager Instance
        {
            get
            {
                if (_instance == null)
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new AudioPlayerManager();
                    }

                return _instance;
            }
        }

        public event EventHandler<AudioPlayEventArgs> PlaybackStateChanged;

        private void InitializeMediaPlayer()
        {
            try
            {
                _mediaPlayer = new MediaPlayer();
                _mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
                _mediaPlayer.MediaFailed += MediaPlayer_MediaFailed;
                _mediaPlayer.CurrentStateChanged += MediaPlayer_CurrentStateChanged;
                Debug.WriteLine("AudioPlayerManager: MediaPlayer 初始化成功");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AudioPlayerManager: MediaPlayer 初始化失敗: {ex.Message}");
            }
        }

        /// <summary>
        ///     播放或暫停音頻
        /// </summary>
        /// <param name="audioUrl">音頻URL</param>
        /// <param name="title">音頻標題</param>
        public void PlayOrPauseAudio(string audioUrl, string title = "音頻")
        {
            try
            {
                Debug.WriteLine($"AudioPlayerManager: 播放或暫停音頻 - URL: {audioUrl}");

                if (_mediaPlayer == null)
                {
                    Debug.WriteLine("AudioPlayerManager: MediaPlayer 未初始化");
                    return;
                }

                // 如果是同一個音頻文件
                if (_currentAudioUrl == audioUrl)
                {
                    if (_isPlaying)
                    {
                        // 暫停當前播放
                        _mediaPlayer.Pause();
                        _isPlaying = false;
                        Debug.WriteLine("AudioPlayerManager: 暫停當前音頻");
                    }
                    else
                    {
                        // 繼續播放
                        _mediaPlayer.Play();
                        _isPlaying = true;
                        Debug.WriteLine("AudioPlayerManager: 繼續播放音頻");
                    }
                }
                else
                {
                    // 播放新的音頻文件
                    StopCurrentAudio();
                    PlayNewAudio(audioUrl, title);
                }

                // 更新狀態記錄
                _audioStates[audioUrl] = _isPlaying;

                // 觸發狀態改變事件
                PlaybackStateChanged?.Invoke(this, new AudioPlayEventArgs(audioUrl, title, _isPlaying));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AudioPlayerManager: 播放音頻時發生錯誤: {ex.Message}");
            }
        }

        private void PlayNewAudio(string audioUrl, string title)
        {
            try
            {
                if (Uri.TryCreate(audioUrl, UriKind.Absolute, out var uri))
                {
                    var mediaSource = MediaSource.CreateFromUri(uri);
                    _mediaPlayer.Source = mediaSource;
                    _mediaPlayer.Play();

                    _currentAudioUrl = audioUrl;
                    _isPlaying = true;

                    Debug.WriteLine($"AudioPlayerManager: 開始播放新音頻 - {title}");
                }
                else
                {
                    Debug.WriteLine("AudioPlayerManager: 無效的音頻URL");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AudioPlayerManager: 播放新音頻時發生錯誤: {ex.Message}");
            }
        }

        private void StopCurrentAudio()
        {
            try
            {
                if (_mediaPlayer != null && !string.IsNullOrEmpty(_currentAudioUrl))
                {
                    _mediaPlayer.Pause();
                    if (_audioStates.ContainsKey(_currentAudioUrl))
                        _audioStates[_currentAudioUrl] = false;
                    _isPlaying = false;
                    Debug.WriteLine("AudioPlayerManager: 停止當前音頻");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AudioPlayerManager: 停止音頻時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     停止所有音頻播放
        /// </summary>
        public void StopAll()
        {
            try
            {
                if (_mediaPlayer != null) _mediaPlayer.Pause();
                _currentAudioUrl = null;
                _isPlaying = false;
                _audioStates.Clear();
                Debug.WriteLine("AudioPlayerManager: 停止所有音頻播放");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AudioPlayerManager: 停止所有音頻時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     獲取指定音頻的播放狀態
        /// </summary>
        public bool IsAudioPlaying(string audioUrl)
        {
            return _currentAudioUrl == audioUrl && _isPlaying;
        }

        /// <summary>
        ///     釋放資源
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Dispose();
                    _mediaPlayer = null;
                }

                _audioStates.Clear();
                Debug.WriteLine("AudioPlayerManager: 資源已釋放");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AudioPlayerManager: 釋放資源時發生錯誤: {ex.Message}");
            }
        }

        #region MediaPlayer 事件處理

        private async void MediaPlayer_MediaEnded(MediaPlayer sender, object args)
        {
            try
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Debug.WriteLine("AudioPlayerManager: 音頻播放結束");

                    if (!string.IsNullOrEmpty(_currentAudioUrl))
                    {
                        if (_audioStates.ContainsKey(_currentAudioUrl))
                            _audioStates[_currentAudioUrl] = false;
                        var audioUrl = _currentAudioUrl;

                        _isPlaying = false;

                        // 觸發狀態改變事件
                        PlaybackStateChanged?.Invoke(this, new AudioPlayEventArgs(audioUrl));
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AudioPlayerManager: 處理播放結束事件時發生錯誤: {ex.Message}");
            }
        }

        private async void MediaPlayer_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            try
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Debug.WriteLine($"AudioPlayerManager: 音頻播放失敗 - {args.ErrorMessage}");

                    if (!string.IsNullOrEmpty(_currentAudioUrl))
                    {
                        if (_audioStates.ContainsKey(_currentAudioUrl))
                            _audioStates[_currentAudioUrl] = false;
                        _isPlaying = false;

                        // 觸發狀態改變事件
                        PlaybackStateChanged?.Invoke(this, new AudioPlayEventArgs(_currentAudioUrl));
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AudioPlayerManager: 處理播放失敗事件時發生錯誤: {ex.Message}");
            }
        }

        private async void MediaPlayer_CurrentStateChanged(MediaPlayer sender, object args)
        {
            try
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Debug.WriteLine($"AudioPlayerManager: 播放狀態改變 - {sender.CurrentState}");

                    var isCurrentlyPlaying = sender.CurrentState == MediaPlayerState.Playing;

                    if (_isPlaying != isCurrentlyPlaying)
                    {
                        _isPlaying = isCurrentlyPlaying;

                        if (!string.IsNullOrEmpty(_currentAudioUrl))
                        {
                            if (_audioStates.ContainsKey(_currentAudioUrl))
                                _audioStates[_currentAudioUrl] = _isPlaying;

                            // 觸發狀態改變事件
                            PlaybackStateChanged?.Invoke(this,
                                new AudioPlayEventArgs(_currentAudioUrl, "音頻", _isPlaying));
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AudioPlayerManager: 處理狀態改變事件時發生錯誤: {ex.Message}");
            }
        }

        #endregion
    }
}