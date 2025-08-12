using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Newtonsoft.Json;

namespace NapcatUWP.Controls
{
    /// <summary>
    ///     圖像查看器控件 - 修復版本，解決UI卡住問題，兼容 UWP 15063
    /// </summary>
    public sealed partial class ImageViewerControl : UserControl
    {
        private DispatcherTimer _closeTimeoutTimer; // 新增：關閉超時計時器
        private DispatcherTimer _controlsTimer;
        private BitmapImage _currentBitmapImage;
        private string _currentImageUrl;
        private bool _hasRequestedClose; // 新增：追蹤是否已請求關閉
        private bool _hasRetried; // 新增：追蹤是否已經重試過
        private bool _isClosing;
        private bool _isImageLoaded;
        private bool _isLoadingImage;
        private CancellationTokenSource _loadCancellationTokenSource;
        private string _originalFileId; // 新增：保存原始文件ID，用於重試

        public ImageViewerControl()
        {
            InitializeComponent();
            InitializeControlsTimer();
            InitializeCloseTimeoutTimer(); // 新增：初始化關閉超時計時器
            Loaded += ImageViewerControl_Loaded;
            SizeChanged += ImageViewerControl_SizeChanged;
        }

        public event EventHandler CloseRequested;

        /// <summary>
        ///     初始化控制面板自動隱藏計時器
        /// </summary>
        private void InitializeControlsTimer()
        {
            _controlsTimer = new DispatcherTimer();
            _controlsTimer.Interval = TimeSpan.FromSeconds(3); // 3秒後隱藏控制面板
            _controlsTimer.Tick += (s, e) =>
            {
                _controlsTimer.Stop();
                SafeStartStoryboard(ControlsFadeOutStoryboard);
            };
        }

        /// <summary>
        ///     初始化關閉超時計時器 - 新增
        /// </summary>
        private void InitializeCloseTimeoutTimer()
        {
            _closeTimeoutTimer = new DispatcherTimer();
            _closeTimeoutTimer.Interval = TimeSpan.FromSeconds(1); // 1秒超時
            _closeTimeoutTimer.Tick += (s, e) =>
            {
                _closeTimeoutTimer.Stop();
                Debug.WriteLine("ImageViewerControl: 關閉動畫超時，強制關閉");
                ForceClose();
            };
        }

        /// <summary>
        ///     安全執行Storyboard，避免UI異常
        /// </summary>
        private void SafeStartStoryboard(Storyboard storyboard)
        {
            try
            {
                if (storyboard != null && !_isClosing)
                {
                    storyboard.Begin();
                    Debug.WriteLine("ImageViewerControl: Storyboard 開始執行");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: 執行動畫時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     顯示圖片 - 修復版本，避免併發載入，支持文件ID重試
        /// </summary>
        /// <param name="imageUrl">圖片URL</param>
        /// <param name="fileId">可選的文件ID，用於API重試</param>
        public async void ShowImage(string imageUrl, string fileId = null)
        {
            // 防止重複載入
            if (_isLoadingImage || _isClosing)
            {
                Debug.WriteLine($"ImageViewerControl: 正在載入圖片或關閉中，忽略請求: {imageUrl}");
                return;
            }

            try
            {
                Debug.WriteLine($"ImageViewerControl: 開始顯示圖片 - URL: {imageUrl}, FileID: {fileId}");

                _isLoadingImage = true;
                _currentImageUrl = imageUrl;
                _originalFileId = fileId; // 保存文件ID用於重試
                _isImageLoaded = false;
                _isClosing = false;
                _hasRequestedClose = false; // 重置關閉請求狀態
                _hasRetried = false; // 重置重試狀態

                // 取消之前的載入操作
                if (_loadCancellationTokenSource != null)
                {
                    _loadCancellationTokenSource.Cancel();
                    _loadCancellationTokenSource.Dispose();
                }

                _loadCancellationTokenSource = new CancellationTokenSource();

                // 使用Dispatcher確保在UI執行緒上執行
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, async () =>
                    {
                        try
                        {
                            // 重置UI狀態
                            ResetUIState();

                            // 顯示載入狀態
                            ShowLoadingState();

                            // 確保查看器可見
                            Visibility = Visibility.Visible;

                            // 播放淡入動畫
                            SafeStartStoryboard(FadeInStoryboard);

                            if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
                                await LoadImageAsync(uri, _loadCancellationTokenSource.Token);
                            else
                                ShowError("無效的圖片URL");
                        }
                        catch (OperationCanceledException)
                        {
                            Debug.WriteLine("ImageViewerControl: 圖片載入被取消");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"ImageViewerControl: 顯示圖片時發生錯誤: {ex.Message}");
                            ShowError($"顯示失敗: {ex.Message}");
                        }
                        finally
                        {
                            _isLoadingImage = false;
                        }
                    });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: 顯示圖片外層錯誤: {ex.Message}");
                _isLoadingImage = false;
            }
        }

        /// <summary>
        ///     異步載入圖片 - 支持取消操作和錯誤重試
        /// </summary>
        private async Task LoadImageAsync(Uri imageUri, CancellationToken cancellationToken)
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                _currentBitmapImage = new BitmapImage();

                // 設置圖片選項以優化載入
                _currentBitmapImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

                // 設置載入超時
                var loadTask = Task.Run(async () =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal, () =>
                        {
                            if (!cancellationToken.IsCancellationRequested && !_isClosing)
                                try
                                {
                                    _currentBitmapImage.UriSource = imageUri;
                                    DisplayImage.Source = _currentBitmapImage;
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"ImageViewerControl: 設置圖片源時發生錯誤: {ex.Message}");
                                }
                        });
                }, cancellationToken);

                var timeoutTask = Task.Delay(10000, cancellationToken); // 10秒超時
                var completedTask = await Task.WhenAny(loadTask, timeoutTask);

                if (completedTask == timeoutTask && !cancellationToken.IsCancellationRequested)
                    throw new TimeoutException("圖片載入超時");
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("ImageViewerControl: 圖片載入被取消");
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: 載入圖片失敗 - {ex.Message}");
                if (!_isClosing)
                {
                    // 如果有文件ID且尚未重試，則使用 get_image API 重試
                    if (!string.IsNullOrEmpty(_originalFileId) && !_hasRetried)
                    {
                        Debug.WriteLine($"ImageViewerControl: 嘗試使用 get_image API 重試載入圖片，FileID: {_originalFileId}");
                        _hasRetried = true;
                        await RetryWithGetImageAPI(_originalFileId, cancellationToken);
                    }
                    else
                    {
                        ShowError($"載入失敗: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        ///     使用 get_image API 重試載入圖片 - 新增方法，通過 OneBotAPIHandler
        /// </summary>
        private async Task RetryWithGetImageAPI(string fileId, CancellationToken cancellationToken)
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                Debug.WriteLine($"ImageViewerControl: 使用 get_image API 請求圖片，FileID: {fileId}");

                // 更新載入狀態顯示
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, () =>
                    {
                        if (!_isClosing)
                        {
                            LoadingPanel.Visibility = Visibility.Visible;
                            LoadingRing.IsActive = true;
                            ErrorPanel.Visibility = Visibility.Collapsed;
                        }
                    });

                // 構建 get_image API 請求
                var requestData = new
                {
                    action = "get_image",
                    @params = new
                    {
                        file_id = fileId
                    },
                    echo = $"get_image_{fileId}_{Guid.NewGuid().ToString("N").Substring(0, 8)}"
                };

                var jsonString = JsonConvert.SerializeObject(requestData);

                // 發送API請求
                await MainPage.SocketClientStarter._socket.Send(jsonString);

                Debug.WriteLine($"ImageViewerControl: 已發送 get_image 請求: {jsonString}");

                // 設置超時處理
                SetupGetImageTimeout(requestData.echo, fileId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: get_image API 重試失敗: {ex.Message}");
                if (!_isClosing) ShowError($"重試載入失敗: {ex.Message}");
            }
        }

        /// <summary>
        ///     設置 get_image API 超時處理 - 新增方法
        /// </summary>
        private void SetupGetImageTimeout(string echo, string fileId)
        {
            try
            {
                // 設置超時計時器
                var timeoutTimer = new DispatcherTimer();
                timeoutTimer.Interval = TimeSpan.FromSeconds(15); // 15秒超時
                timeoutTimer.Tick += (s, e) =>
                {
                    timeoutTimer.Stop();
                    Debug.WriteLine($"ImageViewerControl: get_image API 請求超時，FileID: {fileId}");

                    Task.Run(async () =>
                    {
                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                            CoreDispatcherPriority.Normal, () =>
                            {
                                if (!_isClosing) ShowError("圖片載入超時，請重試");
                            });
                    });
                };
                timeoutTimer.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: 設置 get_image 超時處理失敗: {ex.Message}");
            }
        }

        /// <summary>
        ///     處理 get_image API 響應 - 新增方法，供 OneBotAPIHandler 調用
        /// </summary>
        public async void HandleGetImageResponse(string imageUrl)
        {
            try
            {
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    Debug.WriteLine($"ImageViewerControl: get_image API 成功，新的圖片URL: {imageUrl}");

                    // 使用新的URL重新載入圖片
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal, async () =>
                        {
                            try
                            {
                                if (!_isClosing && Uri.TryCreate(imageUrl, UriKind.Absolute, out var newUri))
                                {
                                    _currentImageUrl = imageUrl; // 更新當前URL

                                    // 重新載入圖片
                                    await LoadImageDirectAsync(newUri,
                                        _loadCancellationTokenSource?.Token ?? CancellationToken.None);
                                }
                                else
                                {
                                    ShowError("從 API 獲取的圖片 URL 無效");
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"ImageViewerControl: 使用 API 響應的 URL 載入圖片失敗: {ex.Message}");
                                ShowError($"載入 API 圖片失敗: {ex.Message}");
                            }
                        });
                }
                else
                {
                    Debug.WriteLine("ImageViewerControl: get_image API 響應中沒有有效的 URL");
                    ShowError("API 響應中沒有有效的圖片 URL");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: 處理 get_image API 響應時發生錯誤: {ex.Message}");
                ShowError($"處理 API 響應失敗: {ex.Message}");
            }
        }

        /// <summary>
        ///     直接載入圖片（不進行額外的重試邏輯）- 新增方法
        /// </summary>
        private async Task LoadImageDirectAsync(Uri imageUri, CancellationToken cancellationToken)
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                _currentBitmapImage = new BitmapImage();
                _currentBitmapImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

                var loadTask = Task.Run(async () =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal, () =>
                        {
                            if (!cancellationToken.IsCancellationRequested && !_isClosing)
                                try
                                {
                                    _currentBitmapImage.UriSource = imageUri;
                                    DisplayImage.Source = _currentBitmapImage;
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"ImageViewerControl: 直接載入圖片時設置圖片源發生錯誤: {ex.Message}");
                                }
                        });
                }, cancellationToken);

                var timeoutTask = Task.Delay(10000, cancellationToken);
                var completedTask = await Task.WhenAny(loadTask, timeoutTask);

                if (completedTask == timeoutTask && !cancellationToken.IsCancellationRequested)
                    throw new TimeoutException("圖片直接載入超時");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: 直接載入圖片失敗 - {ex.Message}");
                if (!_isClosing) ShowError($"載入失敗: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        ///     重置UI狀態
        /// </summary>
        private void ResetUIState()
        {
            try
            {
                // 清理舊資源
                if (DisplayImage.Source != null) DisplayImage.Source = null;

                // 安全重置縮放
                SafeResetZoom();

                // 隱藏錯誤面板和資訊面板
                ErrorPanel.Visibility = Visibility.Collapsed;
                InfoPanel.Visibility = Visibility.Collapsed;

                // 顯示控制面板
                ControlsContainer.Opacity = 1;
                SafeStartStoryboard(ControlsFadeInStoryboard);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: 重置UI狀態時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     安全重置縮放
        /// </summary>
        private void SafeResetZoom()
        {
            try
            {
                if (ImageScrollViewer != null && !_isClosing) ImageScrollViewer.ZoomToFactor(1.0f);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: 重置縮放時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     顯示載入狀態
        /// </summary>
        private void ShowLoadingState()
        {
            try
            {
                if (!_isClosing)
                {
                    LoadingPanel.Visibility = Visibility.Visible;
                    LoadingRing.IsActive = true;
                    ErrorPanel.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: 顯示載入狀態時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     圖片載入成功處理 - 防護版本
        /// </summary>
        private async void DisplayImage_ImageOpened(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isClosing || _loadCancellationTokenSource?.Token.IsCancellationRequested == true)
                    return;

                Debug.WriteLine("ImageViewerControl: 圖片載入成功");

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, () =>
                    {
                        try
                        {
                            if (_isClosing) return;

                            _isImageLoaded = true;
                            LoadingPanel.Visibility = Visibility.Collapsed;
                            LoadingRing.IsActive = false;

                            // 延遲執行縮放操作，避免UI卡頓
                            var _ = Task.Delay(100).ContinueWith(async __ =>
                            {
                                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                                    CoreDispatcherPriority.Low, () =>
                                    {
                                        try
                                        {
                                            if (!_isClosing && _isImageLoaded)
                                            {
                                                // 默認以窗口寬度為基準適配
                                                SafeFitImageToWidth();
                                                SafeUpdateZoomFactorText();

                                                // 顯示圖片資訊
                                                SafeShowImageInfo();

                                                // 開始控制面板自動隱藏計時器
                                                StartControlsTimer();
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.WriteLine($"ImageViewerControl: 延遲處理圖片載入時發生錯誤: {ex.Message}");
                                        }
                                    });
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"ImageViewerControl: 圖片打開處理失敗 - {ex.Message}");
                        }
                    });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: DisplayImage_ImageOpened 發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     圖片載入失敗處理 - 增加重試邏輯
        /// </summary>
        private void DisplayImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            Debug.WriteLine($"ImageViewerControl: 圖片載入失敗 - {e.ErrorMessage}");

            // 如果有文件ID且尚未重試，則嘗試使用 get_image API
            if (!string.IsNullOrEmpty(_originalFileId) && !_hasRetried && !_isClosing)
            {
                Debug.WriteLine($"ImageViewerControl: 圖片載入失敗，嘗試使用 get_image API 重試，FileID: {_originalFileId}");
                _hasRetried = true;

                Task.Run(async () =>
                {
                    try
                    {
                        await RetryWithGetImageAPI(_originalFileId,
                            _loadCancellationTokenSource?.Token ?? CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"ImageViewerControl: get_image API 重試異常: {ex.Message}");
                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                            CoreDispatcherPriority.Normal, () => { ShowError($"圖片載入失敗: {e.ErrorMessage}"); });
                    }
                });
            }
            else
            {
                ShowError($"圖片載入失敗: {e.ErrorMessage}");
            }
        }

        /// <summary>
        ///     顯示錯誤訊息
        /// </summary>
        private void ShowError(string message)
        {
            try
            {
                if (!_isClosing)
                {
                    LoadingPanel.Visibility = Visibility.Collapsed;
                    LoadingRing.IsActive = false;
                    ErrorTextBlock.Text = message;
                    ErrorPanel.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: 顯示錯誤訊息時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     安全顯示圖片資訊
        /// </summary>
        private void SafeShowImageInfo()
        {
            try
            {
                if (_currentBitmapImage != null && !_isClosing)
                {
                    var width = _currentBitmapImage.PixelWidth;
                    var height = _currentBitmapImage.PixelHeight;

                    if (width > 0 && height > 0)
                    {
                        ImageSizeText.Text = $"{width} × {height}";

                        // 估算檔案大小（簡化版本）
                        var estimatedSize = width * height * 4 / (1024 * 1024.0);
                        ImageFileSizeText.Text = $"約 {estimatedSize:F1} MB";

                        InfoPanel.Visibility = Visibility.Visible;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: 顯示圖片資訊失敗 - {ex.Message}");
            }
        }

        /// <summary>
        ///     安全適合圖片到窗口寬度
        /// </summary>
        private void SafeFitImageToWidth()
        {
            try
            {
                if (!_isImageLoaded || _currentBitmapImage == null || _isClosing) return;

                var availableWidth = ActualWidth - 80; // 減去邊距
                var availableHeight = ActualHeight - 200; // 減去控制面板和邊距

                if (availableWidth <= 0 || availableHeight <= 0) return;

                var imageWidth = _currentBitmapImage.PixelWidth;
                var imageHeight = _currentBitmapImage.PixelHeight;

                if (imageWidth == 0 || imageHeight == 0) return;

                // 計算基於寬度的縮放比例
                var widthScale = availableWidth / imageWidth;
                var heightScale = availableHeight / imageHeight;

                // 優先以寬度為基準，但確保不超過可視高度
                var scale = Math.Min(widthScale, heightScale);

                // 如果圖片比窗口小，最多放大到實際大小
                if (scale > 1.0 && imageWidth < availableWidth && imageHeight < availableHeight) scale = 1.0;

                // 限制縮放範圍
                scale = Math.Max(0.1, Math.Min(scale, 3.0));

                SafeZoomToFactor((float)scale);

                Debug.WriteLine($"ImageViewerControl: 適合寬度顯示，縮放比例: {scale:F2}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: 適合寬度失敗 - {ex.Message}");
            }
        }

        /// <summary>
        ///     安全縮放操作
        /// </summary>
        private void SafeZoomToFactor(float scale)
        {
            try
            {
                if (ImageScrollViewer != null && !_isClosing && scale > 0) ImageScrollViewer.ZoomToFactor(scale);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: 縮放操作失敗 - {ex.Message}");
            }
        }

        /// <summary>
        ///     安全適合圖片到整個窗口
        /// </summary>
        private void SafeFitImageToWindow()
        {
            try
            {
                if (!_isImageLoaded || _currentBitmapImage == null || _isClosing) return;

                var availableWidth = ActualWidth - 80;
                var availableHeight = ActualHeight - 200;

                if (availableWidth <= 0 || availableHeight <= 0) return;

                var imageWidth = _currentBitmapImage.PixelWidth;
                var imageHeight = _currentBitmapImage.PixelHeight;

                if (imageWidth == 0 || imageHeight == 0) return;

                var widthScale = availableWidth / imageWidth;
                var heightScale = availableHeight / imageHeight;

                // 使用較小的縮放比例以確保整個圖片可見
                var scale = Math.Min(widthScale, heightScale);
                scale = Math.Max(0.1, Math.Min(scale, 3.0));

                SafeZoomToFactor((float)scale);

                Debug.WriteLine($"ImageViewerControl: 適合窗口，縮放比例: {scale:F2}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: 適合窗口失敗 - {ex.Message}");
            }
        }

        /// <summary>
        ///     安全更新縮放文字顯示
        /// </summary>
        private void SafeUpdateZoomFactorText()
        {
            try
            {
                if (ImageScrollViewer != null && ZoomFactorText != null && !_isClosing)
                {
                    var zoomFactor = ImageScrollViewer.ZoomFactor;
                    ZoomFactorText.Text = $"{(int)(zoomFactor * 100)}%";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: 更新縮放文字失敗 - {ex.Message}");
            }
        }

        /// <summary>
        ///     開始控制面板自動隱藏計時器
        /// </summary>
        private void StartControlsTimer()
        {
            try
            {
                if (_controlsTimer != null && !_isClosing)
                {
                    _controlsTimer.Stop();
                    _controlsTimer.Start();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: 啟動控制面板計時器失敗 - {ex.Message}");
            }
        }

        /// <summary>
        ///     顯示控制面板
        /// </summary>
        private void ShowControls()
        {
            try
            {
                if (ControlsContainer != null && !_isClosing)
                {
                    if (ControlsContainer.Opacity < 1) SafeStartStoryboard(ControlsFadeInStoryboard);
                    StartControlsTimer();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: 顯示控制面板失敗 - {ex.Message}");
            }
        }

        /// <summary>
        ///     關閉圖片查看器 - 修復版本，增加強制關閉機制
        /// </summary>
        private void CloseViewer()
        {
            try
            {
                if (_isClosing || _hasRequestedClose)
                {
                    Debug.WriteLine("ImageViewerControl: 已在關閉中或已請求關閉，直接返回");
                    return;
                }

                _isClosing = true;
                _hasRequestedClose = true;

                Debug.WriteLine("ImageViewerControl: 開始關閉圖片查看器");

                // 取消正在進行的載入操作
                if (_loadCancellationTokenSource != null) _loadCancellationTokenSource.Cancel();

                // 停止計時器
                if (_controlsTimer != null) _controlsTimer.Stop();

                // 啟動關閉超時計時器
                _closeTimeoutTimer?.Start();

                // 播放淡出動畫，如果失敗則直接關閉
                try
                {
                    if (FadeOutStoryboard != null)
                    {
                        Debug.WriteLine("ImageViewerControl: 開始播放淡出動畫");
                        FadeOutStoryboard.Begin();
                    }
                    else
                    {
                        Debug.WriteLine("ImageViewerControl: 淡出動畫為空，直接強制關閉");
                        ForceClose();
                    }
                }
                catch (Exception animationEx)
                {
                    Debug.WriteLine($"ImageViewerControl: 播放淡出動畫失敗: {animationEx.Message}，強制關閉");
                    ForceClose();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: 關閉圖片查看器時發生錯誤: {ex.Message}");
                // 出現異常時強制關閉
                ForceClose();
            }
        }

        /// <summary>
        ///     強制關閉圖片查看器 - 新增方法
        /// </summary>
        private void ForceClose()
        {
            try
            {
                Debug.WriteLine("ImageViewerControl: 強制關閉圖片查看器");

                // 停止所有計時器
                _controlsTimer?.Stop();
                _closeTimeoutTimer?.Stop();

                // 清理資源
                if (DisplayImage?.Source != null) DisplayImage.Source = null;
                _currentBitmapImage = null;

                // 清理取消令牌
                if (_loadCancellationTokenSource != null)
                {
                    _loadCancellationTokenSource.Dispose();
                    _loadCancellationTokenSource = null;
                }

                // 隱藏UI
                Visibility = Visibility.Collapsed;

                // 重置狀態
                _isClosing = false;
                _hasRequestedClose = false;
                _isImageLoaded = false;
                _isLoadingImage = false;
                _hasRetried = false; // 重置重試狀態
                _originalFileId = null; // 清理文件ID

                // 觸發關閉事件
                Debug.WriteLine("ImageViewerControl: 觸發關閉事件");
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: 強制關閉時發生錯誤: {ex.Message}");
                // 確保至少觸發關閉事件
                try
                {
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception eventEx)
                {
                    Debug.WriteLine($"ImageViewerControl: 觸發關閉事件失敗: {eventEx.Message}");
                }
            }
        }

        /// <summary>
        ///     處理返回鍵
        /// </summary>
        public bool HandleBackButton()
        {
            CloseViewer();
            return true;
        }

        #region 事件處理器

        private void ImageViewerControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Focus(FocusState.Programmatic);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: Loaded事件處理失敗 - {ex.Message}");
            }
        }

        private async void ImageViewerControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                if (_isImageLoaded && !_isClosing)
                {
                    // 延遲執行縮放調整，避免頻繁操作導致卡頓
                    await Task.Delay(100);

                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Low, () =>
                        {
                            if (_isImageLoaded && !_isClosing)
                            {
                                SafeFitImageToWidth();
                                SafeUpdateZoomFactorText();
                            }
                        });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: SizeChanged事件處理失敗 - {ex.Message}");
            }
        }

        private void OverlayGrid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            try
            {
                // 只有點擊背景區域才關閉
                if (e.OriginalSource == OverlayGrid) CloseViewer();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: OverlayGrid_Tapped處理失敗 - {ex.Message}");
            }
        }

        private void OverlayGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            try
            {
                // 滑鼠移動時顯示控制面板
                ShowControls();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: PointerMoved處理失敗 - {ex.Message}");
            }
        }

        private void ImageScrollViewer_Tapped(object sender, TappedRoutedEventArgs e)
        {
            try
            {
                if (_isClosing) return;

                // 點擊圖片時顯示/隱藏控制面板
                if (ControlsContainer.Opacity > 0.5)
                {
                    SafeStartStoryboard(ControlsFadeOutStoryboard);
                    if (_controlsTimer != null) _controlsTimer.Stop();
                }
                else
                {
                    ShowControls();
                }

                e.Handled = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: ImageScrollViewer_Tapped處理失敗 - {ex.Message}");
            }
        }

        private void ImageScrollViewer_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            try
            {
                if (_isClosing) return;

                // 雙擊在實際大小和適合寬度之間切換
                var currentZoom = ImageScrollViewer.ZoomFactor;
                if (Math.Abs(currentZoom - 1.0f) < 0.1f)
                    SafeFitImageToWidth();
                else
                    SafeZoomToFactor(1.0f);

                SafeUpdateZoomFactorText();
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: DoubleTapped處理失敗 - {ex.Message}");
            }
        }

        private void ImageScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            try
            {
                if (!e.IsIntermediate && !_isClosing) SafeUpdateZoomFactorText();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: ViewChanged處理失敗 - {ex.Message}");
            }
        }

        private void FadeOutStoryboard_Completed(object sender, object e)
        {
            try
            {
                Debug.WriteLine("ImageViewerControl: 淡出動畫完成");

                // 停止超時計時器
                _closeTimeoutTimer?.Stop();

                // 執行清理和關閉
                ForceClose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: 淡出完成處理失敗 - {ex.Message}");
                // 確保即使發生異常也能關閉
                ForceClose();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("ImageViewerControl: 點擊關閉按鈕");
            CloseViewer();
        }

        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            // 重置重試狀態，允許再次重試
            _hasRetried = false;

            if (!string.IsNullOrEmpty(_currentImageUrl) && !_isLoadingImage)
                ShowImage(_currentImageUrl, _originalFileId);
        }

        private void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isClosing) return;

                var currentZoom = ImageScrollViewer.ZoomFactor;
                var newZoom = Math.Min(currentZoom * 1.5f, ImageScrollViewer.MaxZoomFactor);
                SafeZoomToFactor(newZoom);
                SafeUpdateZoomFactorText();
                StartControlsTimer();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: ZoomIn處理失敗 - {ex.Message}");
            }
        }

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isClosing) return;

                var currentZoom = ImageScrollViewer.ZoomFactor;
                var newZoom = Math.Max(currentZoom / 1.5f, ImageScrollViewer.MinZoomFactor);
                SafeZoomToFactor(newZoom);
                SafeUpdateZoomFactorText();
                StartControlsTimer();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: ZoomOut處理失敗 - {ex.Message}");
            }
        }

        private void FitToWidthButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isClosing) return;

                SafeFitImageToWidth();
                SafeUpdateZoomFactorText();
                StartControlsTimer();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: FitToWidth處理失敗 - {ex.Message}");
            }
        }

        private void FitToWindowButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isClosing) return;

                SafeFitImageToWindow();
                SafeUpdateZoomFactorText();
                StartControlsTimer();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: FitToWindow處理失敗 - {ex.Message}");
            }
        }

        private void ActualSizeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isClosing) return;

                SafeZoomToFactor(1.0f);
                SafeUpdateZoomFactorText();
                StartControlsTimer();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: ActualSize處理失敗 - {ex.Message}");
            }
        }

        #endregion
    }

    /// <summary>
    ///     圖片查看事件參數 - 增加文件ID支持
    /// </summary>
    public class ImageViewEventArgs : EventArgs
    {
        public ImageViewEventArgs(string imageUrl)
        {
            ImageUrl = imageUrl;
        }

        public ImageViewEventArgs(string imageUrl, string fileId) : this(imageUrl)
        {
            FileId = fileId;
        }

        public string ImageUrl { get; }
        public string FileId { get; }
    }
}