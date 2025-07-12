using System;
using System.Diagnostics;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;

namespace NapcatUWP.Controls
{
    public sealed partial class ImageViewerControl : UserControl
    {
        public ImageViewerControl()
        {
            InitializeComponent();
        }

        public event EventHandler CloseRequested;

        /// <summary>
        ///     顯示圖片
        /// </summary>
        /// <param name="imageUrl">圖片URL</param>
        public void ShowImage(string imageUrl)
        {
            try
            {
                Debug.WriteLine($"ImageViewerControl: 開始顯示圖片 - URL: {imageUrl}");

                LoadingRing.IsActive = true;
                LoadingRing.Visibility = Visibility.Visible;
                ErrorTextBlock.Visibility = Visibility.Collapsed;
                DisplayImage.Source = null;

                if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
                {
                    var bitmapImage = new BitmapImage(uri);

                    // 處理圖片加載事件
                    bitmapImage.ImageOpened += BitmapImage_ImageOpened;
                    bitmapImage.ImageFailed += BitmapImage_ImageFailed;

                    DisplayImage.Source = bitmapImage;
                }
                else
                {
                    ShowError("無效的圖片URL");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: 顯示圖片時發生錯誤: {ex.Message}");
                ShowError($"顯示失敗: {ex.Message}");
            }
        }

        private void BitmapImage_ImageOpened(object sender, RoutedEventArgs e)
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;

            // 重置縮放
            ImageScrollViewer.ZoomToFactor(1.0f);
            UpdateZoomFactorText();

            Debug.WriteLine("ImageViewerControl: 圖片加載成功");
        }

        private void BitmapImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            Debug.WriteLine($"ImageViewerControl: 圖片加載失敗 - {e.ErrorMessage}");
            ShowError("圖片加載失敗");
        }

        private void ShowError(string message)
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
            ErrorTextBlock.Text = message;
            ErrorTextBlock.Visibility = Visibility.Visible;
        }

        private void UpdateZoomFactorText()
        {
            var zoomFactor = ImageScrollViewer.ZoomFactor;
            ZoomFactorText.Text = $"{(int)(zoomFactor * 100)}%";
        }

        /// <summary>
        ///     關閉圖片查看器
        /// </summary>
        private void CloseViewer()
        {
            try
            {
                Debug.WriteLine("ImageViewerControl: 關閉圖片查看器");

                // 清理圖片資源
                DisplayImage.Source = null;

                // 觸發關閉事件
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageViewerControl: 關閉圖片查看器時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     處理返回鍵
        /// </summary>
        public bool HandleBackButton()
        {
            CloseViewer();
            return true; // 表示已處理
        }

        #region 事件處理

        private void OverlayGrid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // 只有點擊背景區域才關閉（不是圖片區域）
            if (e.OriginalSource == OverlayGrid) CloseViewer();
        }

        private void ImageScrollViewer_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // 阻止事件冒泡到背景層
            e.Handled = true;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseViewer();
        }

        private void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            var currentZoom = ImageScrollViewer.ZoomFactor;
            var newZoom = Math.Min(currentZoom * 1.2f, ImageScrollViewer.MaxZoomFactor);
            ImageScrollViewer.ZoomToFactor(newZoom);
            UpdateZoomFactorText();
        }

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            var currentZoom = ImageScrollViewer.ZoomFactor;
            var newZoom = Math.Max(currentZoom / 1.2f, ImageScrollViewer.MinZoomFactor);
            ImageScrollViewer.ZoomToFactor(newZoom);
            UpdateZoomFactorText();
        }

        private void ResetZoomButton_Click(object sender, RoutedEventArgs e)
        {
            ImageScrollViewer.ZoomToFactor(1.0f);
            UpdateZoomFactorText();
        }

        #endregion
    }
}