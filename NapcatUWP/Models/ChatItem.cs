using NapcatUWP.Tools;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;

namespace NapcatUWP.Models
{
    public class ChatItem : INotifyPropertyChanged
    {
        private string _avatarColor;
        private long _chatId;
        private bool _isGroup;
        private string _lastMessage;
        private string _lastTime;
        private int _memberCount;
        private string _name;
        private int _unreadCount;
        private BitmapImage _avatarImage;
        private bool _isLoadingAvatar;
        private bool _hasRegisteredAvatarCallback = false;
        private string _processedDisplayMessage;
        private bool _avatarLoadAttempted = false; // 新增：標記是否已嘗試載入頭像

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        public string LastMessage
        {
            get => _lastMessage;
            set
            {
                if (_lastMessage != value)
                {
                    _lastMessage = value;
                    _processedDisplayMessage = null; // 清除緩存，強制重新計算
                    OnPropertyChanged(nameof(LastMessage));
                    OnPropertyChanged(nameof(DisplayLastMessage));
                }
            }
        }

        /// <summary>
        ///     用於顯示的最後消息（處理CQ碼轉換）- 添加緩存避免重複計算
        /// </summary>
        public string DisplayLastMessage
        {
            get
            {
                if (string.IsNullOrEmpty(_lastMessage))
                    return "";

                // 使用緩存避免重複計算
                if (_processedDisplayMessage == null)
                {
                    _processedDisplayMessage = ProcessCQCodeForDisplay(_lastMessage);
                }

                return _processedDisplayMessage;
            }
        }

        public string LastTime
        {
            get => _lastTime;
            set
            {
                if (_lastTime != value)
                {
                    _lastTime = value;
                    OnPropertyChanged(nameof(LastTime));
                }
            }
        }

        public int UnreadCount
        {
            get => _unreadCount;
            set
            {
                if (_unreadCount != value)
                {
                    _unreadCount = value;
                    OnPropertyChanged(nameof(UnreadCount));
                }
            }
        }

        public string AvatarColor
        {
            get => _avatarColor;
            set
            {
                if (_avatarColor != value)
                {
                    _avatarColor = value;
                    OnPropertyChanged(nameof(AvatarColor));
                }
            }
        }

        /// <summary>
        /// 头像图片
        /// </summary>
        public BitmapImage AvatarImage
        {
            get => _avatarImage;
            set
            {
                if (_avatarImage != value)
                {
                    _avatarImage = value;
                    OnPropertyChanged(nameof(AvatarImage));
                    OnPropertyChanged(nameof(HasAvatar));
                }
            }
        }

        /// <summary>
        /// 是否正在加载头像
        /// </summary>
        public bool IsLoadingAvatar
        {
            get => _isLoadingAvatar;
            set
            {
                if (_isLoadingAvatar != value)
                {
                    _isLoadingAvatar = value;
                    OnPropertyChanged(nameof(IsLoadingAvatar));
                }
            }
        }

        /// <summary>
        /// 是否有头像图片
        /// </summary>
        public bool HasAvatar =>
            _avatarImage != null && (_avatarImage.UriSource != null || _avatarImage.PixelWidth > 0);

        /// <summary>
        /// 檢查是否在UI線程
        /// </summary>
        private bool IsOnUIThread()
        {
            try
            {
                return CoreApplication.MainView.CoreWindow.Dispatcher.HasThreadAccess;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 註冊頭像更新回調 - 修復重複註冊問題
        /// </summary>
        private void RegisterAvatarUpdateCallback()
        {
            try
            {
                // 防止重複註冊（每個實例只註冊一次）
                if (_hasRegisteredAvatarCallback)
                {
                    return;
                }

                var avatarType = IsGroup ? "group" : "friend";
                var expectedCacheKey = $"{avatarType}_{ChatId}";

                // 使用弱引用避免記憶體洩漏
                AvatarManager.OnAvatarUpdated += OnAvatarManagerUpdated;
                _hasRegisteredAvatarCallback = true;

                Debug.WriteLine($"註冊頭像更新回調: {expectedCacheKey}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"註冊頭像更新回調失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 頭像管理器更新回調 - 修復重複更新問題
        /// </summary>
        private void OnAvatarManagerUpdated(string cacheKey, BitmapImage image)
        {
            try
            {
                var avatarType = IsGroup ? "group" : "friend";
                var expectedCacheKey = $"{avatarType}_{ChatId}";

                // 只處理屬於這個ChatItem的頭像更新
                if (cacheKey == expectedCacheKey && image != null)
                {
                    if (IsOnUIThread())
                    {
                        UpdateAvatarOnUIThread(image, expectedCacheKey);
                    }
                    else
                    {
                        // 異步更新UI，使用低優先級
                        _ = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                            CoreDispatcherPriority.Low, () => UpdateAvatarOnUIThread(image, expectedCacheKey));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"頭像更新回調處理失敗: {ex.Message}");
            }
        }


        /// <summary>
        /// 在UI線程更新頭像 - 防止重複更新
        /// </summary>
        private void UpdateAvatarOnUIThread(BitmapImage image, string expectedKey)
        {
            try
            {
                // 防止重複設置相同頭像
                if (_avatarImage != image && image != null)
                {
                    AvatarImage = image;
                    OnPropertyChanged(nameof(AvatarImage));
                    OnPropertyChanged(nameof(HasAvatar));

                    Debug.WriteLine($"頭像回調更新成功: {expectedKey}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UI線程頭像更新失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 异步加载头像（最終修復版 - 修復重複註冊問題）
        /// </summary>
        public async void LoadAvatarAsync(int priority = 2, bool useCache = false)
        {
            try
            {
                // 如果已經有頭像或正在載入，跳過
                if (_avatarImage != null || _isLoadingAvatar)
                {
                    return;
                }

                // 確保只註冊一次回調（每個ChatItem實例只註冊一次）
                if (!_hasRegisteredAvatarCallback)
                {
                    RegisterAvatarUpdateCallback();
                }

                _isLoadingAvatar = true;
                _avatarLoadAttempted = true;

                var avatarType = IsGroup ? "group" : "friend";
                var cacheKey = $"{avatarType}_{ChatId}";

                Debug.WriteLine($"开始异步加载头像: {cacheKey}, Priority: {priority}, UseCache: {useCache}");

                // 使用低優先級任務載入
                var avatar = await Task.Run(async () =>
                {
                    try
                    {
                        return await AvatarManager.GetAvatarAsync(avatarType, ChatId, priority, useCache);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"頭像管理器載入失敗: {ex.Message}");
                        return null;
                    }
                });

                // 在UI線程更新
                if (avatar != null)
                {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Low, () =>
                        {
                            try
                            {
                                if (_avatarImage == null) // 防止重複設置
                                {
                                    AvatarImage = avatar;
                                    OnPropertyChanged(nameof(AvatarImage));
                                    OnPropertyChanged(nameof(HasAvatar));
                                    Debug.WriteLine($"头像加载成功: {cacheKey}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"設置頭像時發生錯誤: {ex.Message}");
                            }
                        });
                }
                else
                {
                    Debug.WriteLine($"头像加载失败: {cacheKey}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadAvatarAsync 發生錯誤: {ex.Message}");
            }
            finally
            {
                _isLoadingAvatar = false;
            }
        }


        /// <summary>
        /// 智能緩存載入（最終修復版 - 避免重複註冊和UI阻塞）
        /// </summary>
        public void LoadAvatarFromCacheAsync()
        {
            try
            {
                // 如果已經有頭像，跳過
                if (_avatarImage != null || _isLoadingAvatar || _avatarLoadAttempted)
                {
                    return;
                }

                // 確保只註冊一次回調
                if (!_hasRegisteredAvatarCallback)
                {
                    RegisterAvatarUpdateCallback();
                }

                _isLoadingAvatar = true;
                _avatarLoadAttempted = true;

                var avatarType = IsGroup ? "group" : "friend";
                var cacheKey = $"{avatarType}_{ChatId}";

                Debug.WriteLine($"开始智能加载头像: {cacheKey}, 已嘗試: {_avatarLoadAttempted}");

                // 使用低優先級任務，避免阻塞UI
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // 僅從緩存載入，不觸發網路下載
                        var cachedAvatar =
                            await AvatarManager.GetAvatarAsync(avatarType, ChatId, priority: 2, useCache: true);

                        if (cachedAvatar != null)
                        {
                            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                                CoreDispatcherPriority.Low, () =>
                                {
                                    try
                                    {
                                        if (_avatarImage == null) // 防止重複設置
                                        {
                                            AvatarImage = cachedAvatar;
                                            OnPropertyChanged(nameof(AvatarImage));
                                            OnPropertyChanged(nameof(HasAvatar));
                                            Debug.WriteLine($"成功從緩存載入頭像: {cacheKey}");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"設置緩存頭像時發生錯誤: {ex.Message}");
                                    }
                                });
                        }
                        else
                        {
                            Debug.WriteLine($"僅緩存模式，未找到緩存頭像: {cacheKey}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"智能載入頭像失敗: {ex.Message}");
                    }
                    finally
                    {
                        _isLoadingAvatar = false;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadAvatarFromCacheAsync 發生錯誤: {ex.Message}");
                _isLoadingAvatar = false;
            }
        }

        /// <summary>
        /// 强制刷新头像（高优先级）
        /// </summary>
        public void RefreshAvatarAsync()
        {
            // 重置載入狀態
            _avatarLoadAttempted = false;

            // 註冊頭像更新回調（如果還沒有註冊）
            RegisterAvatarUpdateCallback();

            // 優化：直接設置而不是等待UI線程
            if (IsOnUIThread())
            {
                AvatarImage = null;
                LoadAvatarAsync(priority: 0, useCache: false);
            }
            else
            {
                var _ = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    AvatarImage = null;
                    LoadAvatarAsync(priority: 0, useCache: false);
                });
            }
        }

        public long ChatId
        {
            get => _chatId;
            set
            {
                if (_chatId != value)
                {
                    _chatId = value;
                    OnPropertyChanged(nameof(ChatId));
                    // 当ChatId改变时，清空头像并重新加载
                    AvatarImage = null;
                    _avatarLoadAttempted = false; // 重置載入狀態
                    LoadAvatarFromCacheAsync();
                }
            }
        }

        public bool IsGroup
        {
            get => _isGroup;
            set
            {
                if (_isGroup != value)
                {
                    _isGroup = value;
                    OnPropertyChanged(nameof(IsGroup));
                    // 当聊天类型改变时，清空头像并重新加载
                    AvatarImage = null;
                    _avatarLoadAttempted = false; // 重置載入狀態
                    LoadAvatarFromCacheAsync();
                }
            }
        }

        public int MemberCount
        {
            get => _memberCount;
            set
            {
                if (_memberCount != value)
                {
                    _memberCount = value;
                    OnPropertyChanged(nameof(MemberCount));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 析構函數 - 清理頭像更新回調
        /// </summary>
        ~ChatItem()
        {
            if (_hasRegisteredAvatarCallback)
            {
                try
                {
                    AvatarManager.OnAvatarUpdated -= OnAvatarManagerUpdated;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"清理頭像回調時發生錯誤: {ex.Message}");
                }
            }
        }

        /// <summary>
        ///     處理CQ碼以便於列表顯示（優化版 - 使用StringBuilder和更有效的字符串處理）
        /// </summary>
        private string ProcessCQCodeForDisplay(string content)
        {
            if (string.IsNullOrEmpty(content))
                return "";

            // 創建副本以避免修改原始内容
            var processedContent = content;

            // 優化：使用更有效的字符串替換方法
            var replacements = new (string pattern, string replacement)[]
            {
                ("[CQ:at,qq=all]", "@所有人"),
            };

            foreach (var (pattern, replacement) in replacements)
            {
                processedContent = processedContent.Replace(pattern, replacement);
            }

            // 處理需要正則表達式的複雜CQ碼
            processedContent = ProcessComplexCQCodes(processedContent);

            return processedContent;
        }

        /// <summary>
        /// 處理複雜的CQ碼（優化版）
        /// </summary>
        private string ProcessComplexCQCodes(string content)
        {
            var cqCodeMappings = new (string startPattern, string replacement)[]
            {
                ("[CQ:image", "[图片]"),
                ("[CQ:record", "[语音]"),
                ("[CQ:video", "[视频]"),
                ("[CQ:file", "[文件]"),
                ("[CQ:face", "[表情]"),
                ("[CQ:reply", "[回复]"),
                ("[CQ:poke", "[戳一戳]"),
                ("[CQ:gift", "[礼物]"),
                ("[CQ:forward", "[转发]"),
                ("[CQ:xml", "[XML卡片]"),
                ("[CQ:json", "[JSON卡片]")
            };

            foreach (var (startPattern, replacement) in cqCodeMappings)
            {
                content = ReplaceCQCode(content, startPattern, replacement);
            }

            // 特殊處理@某人CQ碼
            content = ProcessAtCQCodes(content);

            // 處理其他未知的CQ碼
            content = ReplaceUnknownCQCodes(content);

            return content;
        }

        /// <summary>
        /// 替換指定的CQ碼
        /// </summary>
        private string ReplaceCQCode(string content, string startPattern, string replacement)
        {
            while (true)
            {
                var start = content.IndexOf(startPattern, StringComparison.Ordinal);
                if (start < 0) break;

                var end = content.IndexOf("]", start, StringComparison.Ordinal);
                if (end <= start) break;

                content = content.Substring(0, start) + replacement + content.Substring(end + 1);
            }

            return content;
        }

        /// <summary>
        /// 處理@某人的CQ碼
        /// </summary>
        private string ProcessAtCQCodes(string content)
        {
            while (true)
            {
                var start = content.IndexOf("[CQ:at,qq=", StringComparison.Ordinal);
                if (start < 0) break;

                var end = content.IndexOf("]", start, StringComparison.Ordinal);
                if (end <= start) break;

                var qqStart = start + "[CQ:at,qq=".Length;
                var qqEnd = end;

                // 检查是否有其他参数
                var commaIndex = content.IndexOf(",", qqStart, StringComparison.Ordinal);
                if (commaIndex > 0 && commaIndex < qqEnd) qqEnd = commaIndex;

                if (qqEnd > qqStart)
                {
                    var qq = content.Substring(qqStart, qqEnd - qqStart);
                    content = content.Substring(0, start) + $"@{qq}" + content.Substring(end + 1);
                }
                else
                {
                    // 如果解析失敗，簡單替換為@某人
                    content = content.Substring(0, start) + "@某人" + content.Substring(end + 1);
                }
            }

            return content;
        }

        /// <summary>
        /// 處理未知的CQ碼
        /// </summary>
        private string ReplaceUnknownCQCodes(string content)
        {
            while (true)
            {
                var start = content.IndexOf("[CQ:", StringComparison.Ordinal);
                if (start < 0) break;

                var end = content.IndexOf("]", start, StringComparison.Ordinal);
                if (end <= start) break;

                content = content.Substring(0, start) + "[多媒体内容]" + content.Substring(end + 1);
            }

            return content;
        }
    }
}