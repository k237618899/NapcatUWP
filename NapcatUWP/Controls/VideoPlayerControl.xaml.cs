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
        /// 播放视频
        /// </summary>
        /// <param name="videoUrl">视频URL</param>
        /// <param name="title">视频标题</param>
        public void PlayVideo(string videoUrl, string title = "视频播放器")
        {
            try
            {
                Debug.WriteLine($"VideoPlayerControl: 开始播放视频 - URL: {videoUrl}, Title: {title}");

                TitleTextBlock.Text = title;
                LoadingRing.IsActive = true;
                LoadingRing.Visibility = Visibility.Visible;
                ErrorTextBlock.Visibility = Visibility.Collapsed;
                PlayPauseOverlay.Visibility = Visibility.Collapsed;

                // 设置视频源
                if (Uri.TryCreate(videoUrl, UriKind.Absolute, out var uri))
                {
                    VideoPlayer.Source = uri;
                    VideoPlayer.Play();
                }
                else
                {
                    ShowError("无效的视频URL");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VideoPlayerControl: 播放视频时发生错误: {ex.Message}");
                ShowError($"播放失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示错误信息
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
        /// 格式化时间显示
        /// </summary>
        private string FormatTime(TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 1)
                return timeSpan.ToString(@"h\:mm\:ss");
            else
                return timeSpan.ToString(@"m\:ss");
        }

        /// <summary>
        /// 更新播放/暂停按钮图标
        /// </summary>
        private void UpdatePlayPauseIcon()
        {
            var isPlaying = VideoPlayer.CurrentState == MediaElementState.Playing;
            PlayPauseButtonIcon.Text = isPlaying ? "⏸" : "▶";
            PlayPauseIcon.Text = isPlaying ? "⏸" : "▶";
        }

        /// <summary>
        /// 更新音量图标
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
        /// 显示/隐藏控制栏
        /// </summary>
        private void ToggleControlsVisibility()
        {
            _isControlsVisible = !_isControlsVisible;
            ControlsPanel.Visibility = _isControlsVisible ? Visibility.Visible : Visibility.Collapsed;

            if (_isControlsVisible)
                StartHideControlsTimer();
        }

        /// <summary>
        /// 启动自动隐藏控制栏定时器
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
        /// 启动进度更新定时器
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
        /// 停止进度更新定时器
        /// </summary>
        private void StopProgressUpdateTimer()
        {
            _progressUpdateTimer?.Cancel();
            _progressUpdateTimer = null;
        }

        /// <summary>
        /// 更新进度条
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

        #region 事件处理

        private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("VideoPlayerControl: 视频打开成功");

            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
            PlayPauseOverlay.Visibility = Visibility.Visible;

            // 设置进度条最大值
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

        // 修复MediaFailed事件的参数类型
        private void VideoPlayer_MediaFailed(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("VideoPlayerControl: 视频播放失败");
            ShowError("播放失败: 媒体错误");
            StopProgressUpdateTimer();
        }

        private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("VideoPlayerControl: 视频播放结束");
            UpdatePlayPauseIcon();
            ProgressSlider.Value = 0;
            CurrentTimeText.Text = "00:00";

            // 显示播放按钮覆盖层
            PlayPauseOverlay.Visibility = Visibility.Visible;
            _isControlsVisible = true;
            ControlsPanel.Visibility = Visibility.Visible;
            StopProgressUpdateTimer();
        }

        private void VideoPlayer_CurrentStateChanged(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"VideoPlayerControl: 播放状态变化 - {VideoPlayer.CurrentState}");
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

            // 重置自动隐藏定时器
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
                // 取消静音
                VideoPlayer.Volume = _previousVolume;
                _isMuted = false;
            }
            else
            {
                // 静音
                _previousVolume = VideoPlayer.Volume;
                VideoPlayer.Volume = 0;
                _isMuted = true;
            }

            UpdateVolumeIcon();

            // 重置自动隐藏定时器
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
            _hideControlsTimer?.Cancel(); // 拖拽时停止自动隐藏
        }

        private void ProgressSlider_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _isUserDragging = false;

            // 重新启动自动隐藏定时器
            if (_isControlsVisible && VideoPlayer.CurrentState == MediaElementState.Playing)
                StartHideControlsTimer();
        }

        private void VideoPlayer_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // 点击视频区域显示/隐藏控制栏
            ToggleControlsVisibility();
        }

        private void PlayerContainer_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // 阻止事件冒泡到背景层
            e.Handled = true;
        }

        private void OverlayGrid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // 点击背景区域关闭播放器
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
                Debug.WriteLine("VideoPlayerControl: 关闭播放器");

                // 停止播放
                VideoPlayer.Stop();
                VideoPlayer.Source = null;

                // 停止定时器
                _hideControlsTimer?.Cancel();
                StopProgressUpdateTimer();

                // 触发关闭事件
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VideoPlayerControl: 关闭播放器时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理返回键
        /// </summary>
        public bool HandleBackButton()
        {
            ClosePlayer();
            return true; // 表示已处理
        }
    }
}