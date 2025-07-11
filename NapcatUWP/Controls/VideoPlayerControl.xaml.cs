using System;
using System.Diagnostics;
using Windows.Media.Core;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace NapcatUWP.Controls
{
    public sealed partial class VideoPlayerControl : UserControl
    {
        private bool _isUserDragging = false;
        private bool _isMuted = false;
        private double _previousVolume = 1.0;
        private ThreadPoolTimer _hideControlsTimer;
        private ThreadPoolTimer _progressUpdateTimer;
        private bool _isControlsVisible = true;

        public event EventHandler CloseRequested;

        public VideoPlayerControl()
        {
            this.InitializeComponent();
            VideoPlayer.Volume = 0.8; // 默认音量80%
            _previousVolume = VideoPlayer.Volume;
        }

        /// <summary>
        /// 播放視頻
        /// </summary>
        /// <param name="videoUrl">視頻URL</param>
        /// <param name="title">視頻標題</param>
        public void PlayVideo(string videoUrl, string title = "視頻播放器")
        {
            try
            {
                Debug.WriteLine($"VideoPlayerControl: 開始播放視頻 - URL: {videoUrl}, Title: {title}");

                TitleTextBlock.Text = title;
                LoadingRing.IsActive = true;
                LoadingRing.Visibility = Visibility.Visible;
                ErrorTextBlock.Visibility = Visibility.Collapsed;
                PlayPauseOverlay.Visibility = Visibility.Collapsed;

                // 設置視頻源
                if (Uri.TryCreate(videoUrl, UriKind.Absolute, out var uri))
                {
                    VideoPlayer.Source = uri;
                    VideoPlayer.Play();
                }
                else
                {
                    ShowError("無效的視頻URL");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VideoPlayerControl: 播放視頻時發生錯誤: {ex.Message}");
                ShowError($"播放失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 顯示錯誤信息
        /// </summary>
        private void ShowError(string message)
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
            ErrorTextBlock.Text = message;
            ErrorTextBlock.Visibility = Visibility.Visible;
            PlayPauseOverlay.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// 格式化時間顯示
        /// </summary>
        private string FormatTime(TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 1)
                return timeSpan.ToString(@"h\:mm\:ss");
            else
                return timeSpan.ToString(@"m\:ss");
        }

        /// <summary>
        /// 更新播放/暫停按鈕圖標
        /// </summary>
        private void UpdatePlayPauseIcon()
        {
            var isPlaying = VideoPlayer.CurrentState == MediaElementState.Playing;
            PlayPauseButtonIcon.Text = isPlaying ? "⏸" : "▶";
            PlayPauseIcon.Text = isPlaying ? "⏸" : "▶";
        }

        /// <summary>
        /// 更新音量圖標
        /// </summary>
        private void UpdateVolumeIcon()
        {
            if (_isMuted || VideoPlayer.Volume == 0)
                VolumeIcon.Text = "🔇";
            else if (VideoPlayer.Volume < 0.5)
                VolumeIcon.Text = "🔉";
            else
                VolumeIcon.Text = "🔊";
        }

        /// <summary>
        /// 顯示/隱藏控制欄
        /// </summary>
        private void ToggleControlsVisibility()
        {
            _isControlsVisible = !_isControlsVisible;
            ControlsPanel.Visibility = _isControlsVisible ? Visibility.Visible : Visibility.Collapsed;

            if (_isControlsVisible)
                StartHideControlsTimer();
        }

        /// <summary>
        /// 啟動自動隱藏控制欄定時器
        /// </summary>
        private void StartHideControlsTimer()
        {
            _hideControlsTimer?.Cancel();
            _hideControlsTimer = ThreadPoolTimer.CreateTimer(async (timer) =>
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    if (_isControlsVisible && VideoPlayer.CurrentState == MediaElementState.Playing)
                    {
                        _isControlsVisible = false;
                        ControlsPanel.Visibility = Visibility.Collapsed;
                    }
                });
            }, TimeSpan.FromSeconds(3));
        }

        /// <summary>
        /// 啟動進度更新定時器
        /// </summary>
        private void StartProgressUpdateTimer()
        {
            _progressUpdateTimer?.Cancel();
            _progressUpdateTimer = ThreadPoolTimer.CreatePeriodicTimer(
                async (timer) =>
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { UpdateProgress(); });
                }, TimeSpan.FromMilliseconds(500));
        }

        /// <summary>
        /// 停止進度更新定時器
        /// </summary>
        private void StopProgressUpdateTimer()
        {
            _progressUpdateTimer?.Cancel();
            _progressUpdateTimer = null;
        }

        /// <summary>
        /// 更新進度條
        /// </summary>
        private void UpdateProgress()
        {
            if (!_isUserDragging && VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                var position = VideoPlayer.Position;
                var duration = VideoPlayer.NaturalDuration.TimeSpan;

                if (duration.TotalSeconds > 0)
                {
                    ProgressSlider.Value = position.TotalSeconds;
                    CurrentTimeText.Text = FormatTime(position);
                }
            }
        }

        #region 事件處理

        private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("VideoPlayerControl: 視頻打開成功");

            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
            PlayPauseOverlay.Visibility = Visibility.Visible;

            // 設置進度條最大值
            if (VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                var duration = VideoPlayer.NaturalDuration.TimeSpan;
                ProgressSlider.Maximum = duration.TotalSeconds;
                TotalTimeText.Text = FormatTime(duration);
            }

            UpdatePlayPauseIcon();
            UpdateVolumeIcon();
            StartHideControlsTimer();
            StartProgressUpdateTimer();
        }

        // 修復MediaFailed事件的參數類型
        private void VideoPlayer_MediaFailed(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("VideoPlayerControl: 視頻播放失敗");
            ShowError("播放失敗: 媒體錯誤");
            StopProgressUpdateTimer();
        }

        private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("VideoPlayerControl: 視頻播放結束");
            UpdatePlayPauseIcon();
            ProgressSlider.Value = 0;
            CurrentTimeText.Text = "00:00";

            // 顯示播放按鈕覆蓋層
            PlayPauseOverlay.Visibility = Visibility.Visible;
            _isControlsVisible = true;
            ControlsPanel.Visibility = Visibility.Visible;
            StopProgressUpdateTimer();
        }

        private void VideoPlayer_CurrentStateChanged(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"VideoPlayerControl: 播放狀態變化 - {VideoPlayer.CurrentState}");
            UpdatePlayPauseIcon();

            if (VideoPlayer.CurrentState == MediaElementState.Playing)
            {
                StartHideControlsTimer();
                StartProgressUpdateTimer();
            }
            else
            {
                StopProgressUpdateTimer();
            }
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (VideoPlayer.CurrentState == MediaElementState.Playing)
            {
                VideoPlayer.Pause();
            }
            else
            {
                VideoPlayer.Play();
                PlayPauseOverlay.Visibility = Visibility.Collapsed;
            }

            // 重置自動隱藏定時器
            if (_isControlsVisible)
                StartHideControlsTimer();
        }

        private void PlayPauseOverlay_Click(object sender, RoutedEventArgs e)
        {
            VideoPlayer.Play();
            PlayPauseOverlay.Visibility = Visibility.Collapsed;
            StartHideControlsTimer();
        }

        private void VolumeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isMuted)
            {
                // 取消靜音
                VideoPlayer.Volume = _previousVolume;
                _isMuted = false;
            }
            else
            {
                // 靜音
                _previousVolume = VideoPlayer.Volume;
                VideoPlayer.Volume = 0;
                _isMuted = true;
            }

            UpdateVolumeIcon();

            // 重置自動隱藏定時器
            if (_isControlsVisible)
                StartHideControlsTimer();
        }

        private void ProgressSlider_ValueChanged(object sender,
            Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_isUserDragging && VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                var newPosition = TimeSpan.FromSeconds(e.NewValue);
                VideoPlayer.Position = newPosition;
                CurrentTimeText.Text = FormatTime(newPosition);
            }
        }

        private void ProgressSlider_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _isUserDragging = true;
            _hideControlsTimer?.Cancel(); // 拖拽時停止自動隱藏
        }

        private void ProgressSlider_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _isUserDragging = false;

            // 重新啟動自動隱藏定時器
            if (_isControlsVisible && VideoPlayer.CurrentState == MediaElementState.Playing)
                StartHideControlsTimer();
        }

        private void VideoPlayer_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // 點擊視頻區域顯示/隱藏控制欄
            ToggleControlsVisibility();
        }

        private void PlayerContainer_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // 阻止事件冒泡到背景層
            e.Handled = true;
        }

        private void OverlayGrid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // 點擊背景區域關閉播放器
            ClosePlayer();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ClosePlayer();
        }

        #endregion

        /// <summary>
        /// 关闭播放器
        /// </summary>
        private void ClosePlayer()
        {
            try
            {
                Debug.WriteLine("VideoPlayerControl: 關閉播放器");

                // 停止播放
                VideoPlayer.Stop();
                VideoPlayer.Source = null;

                // 停止定時器
                _hideControlsTimer?.Cancel();
                StopProgressUpdateTimer();

                // 觸發關閉事件
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VideoPlayerControl: 關閉播放器時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        /// 處理返回鍵
        /// </summary>
        public bool HandleBackButton()
        {
            ClosePlayer();
            return true; // 表示已處理
        }
    }
}