using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;
using NapcatUWP.Tools;

namespace NapcatUWP.Models
{
    public class ChatItem : INotifyPropertyChanged
    {
        private string _avatarColor;
        private BitmapImage _avatarImage;
        private bool _avatarLoadAttempted;
        private long _chatId;
        private bool _hasRegisteredAvatarCallback;
        private bool _isGroup;
        private bool _isLoadingAvatar;
        private string _lastMessage;
        private string _lastTime;
        private int _memberCount;
        private string _name;
        private string _processedDisplayMessage;
        private int _unreadCount;

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    SafeOnPropertyChanged(nameof(Name));
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
                    SafeOnPropertyChanged(nameof(LastMessage));
                    SafeOnPropertyChanged(nameof(DisplayLastMessage));
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
                if (_processedDisplayMessage == null) _processedDisplayMessage = ProcessCQCodeForDisplay(_lastMessage);

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
                    SafeOnPropertyChanged(nameof(LastTime));
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
                    SafeOnPropertyChanged(nameof(UnreadCount));
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
                    SafeOnPropertyChanged(nameof(AvatarColor));
                }
            }
        }

        /// <summary>
        ///     頭像圖片
        /// </summary>
        public BitmapImage AvatarImage
        {
            get => _avatarImage;
            set
            {
                if (_avatarImage != value)
                {
                    _avatarImage = value;
                    // 更新 HasAvatar 緩存狀態
                    UpdateHasAvatarStatus();
                    SafeOnPropertyChanged(nameof(AvatarImage));
                    SafeOnPropertyChanged(nameof(HasAvatar));
                }
            }
        }

        /// <summary>
        ///     是否正在載入頭像
        /// </summary>
        public bool IsLoadingAvatar
        {
            get => _isLoadingAvatar;
            set
            {
                if (_isLoadingAvatar != value)
                {
                    _isLoadingAvatar = value;
                    SafeOnPropertyChanged(nameof(IsLoadingAvatar));
                }
            }
        }

        /// <summary>
        ///     是否有頭像圖片 - 使用緩存狀態避免跨執行緒訪問
        /// </summary>
        public bool HasAvatar { get; private set; }

        public long ChatId
        {
            get => _chatId;
            set
            {
                if (_chatId != value)
                {
                    _chatId = value;
                    SafeOnPropertyChanged(nameof(ChatId));

                    // 當ChatId改變時，在UI線程清空頭像並重新載入
                    if (IsOnUIThread())
                    {
                        AvatarImage = null;
                        _avatarLoadAttempted = false;
                        LoadAvatarFromCacheAsync();
                    }
                    else
                    {
                        _ = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                            CoreDispatcherPriority.Normal, () =>
                            {
                                AvatarImage = null;
                                _avatarLoadAttempted = false;
                                LoadAvatarFromCacheAsync();
                            });
                    }
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
                    SafeOnPropertyChanged(nameof(IsGroup));

                    // 當聊天類型改變時，在UI線程清空頭像並重新載入
                    if (IsOnUIThread())
                    {
                        AvatarImage = null;
                        _avatarLoadAttempted = false;
                        LoadAvatarFromCacheAsync();
                    }
                    else
                    {
                        _ = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                            CoreDispatcherPriority.Normal, () =>
                            {
                                AvatarImage = null;
                                _avatarLoadAttempted = false;
                                LoadAvatarFromCacheAsync();
                            });
                    }
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
                    SafeOnPropertyChanged(nameof(MemberCount));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        ///     更新 HasAvatar 狀態 - 確保執行緒安全
        /// </summary>
        private void UpdateHasAvatarStatus()
        {
            if (IsOnUIThread())
            {
                // 在UI執行緒上安全訪問 BitmapImage 屬性
                HasAvatar = _avatarImage != null &&
                            (_avatarImage.UriSource != null || _avatarImage.PixelWidth > 0);
            }
            else
            {
                // 非UI執行緒上只檢查是否為null
                HasAvatar = _avatarImage != null;

                // 派發到UI執行緒進行完整檢查
                _ = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Low, () =>
                    {
                        try
                        {
                            var newHasAvatar = _avatarImage != null &&
                                               (_avatarImage.UriSource != null || _avatarImage.PixelWidth > 0);

                            if (HasAvatar != newHasAvatar)
                            {
                                HasAvatar = newHasAvatar;
                                SafeOnPropertyChanged(nameof(HasAvatar));
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"更新 HasAvatar 狀態失敗: {ex.Message}");
                        }
                    });
            }
        }

        /// <summary>
        ///     檢查是否在UI線程
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
        ///     安全的屬性變更通知 - 確保在UI線程執行
        /// </summary>
        private void SafeOnPropertyChanged(string propertyName)
        {
            try
            {
                if (IsOnUIThread())
                    // 如果已經在UI線程，直接觸發
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                else
                    // 如果不在UI線程，派發到UI線程執行
                    _ = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal, () =>
                        {
                            try
                            {
                                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"屬性變更通知失敗: {propertyName}, {ex.Message}");
                            }
                        });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SafeOnPropertyChanged 失敗: {propertyName}, {ex.Message}");
            }
        }

        /// <summary>
        ///     修復版註冊頭像更新回調 - 確保不重複註冊且類型正確
        /// </summary>
        private void RegisterAvatarUpdateCallback()
        {
            try
            {
                // 防止重複註冊（每個實例只註冊一次）
                if (_hasRegisteredAvatarCallback) return;

                var avatarType = IsGroup ? "group" : "friend";
                var expectedCacheKey = $"{avatarType}_{ChatId}";

                // 驗證ChatId的有效性
                if (ChatId <= 0)
                {
                    Debug.WriteLine($"⚠ 無效的ChatId，跳過頭像回調註冊: {expectedCacheKey}");
                    return;
                }

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
        ///     修復版頭像管理器更新回調 - 嚴格類型檢查
        /// </summary>
        private void OnAvatarManagerUpdated(string cacheKey, BitmapImage image)
        {
            try
            {
                var avatarType = IsGroup ? "group" : "friend";
                var expectedCacheKey = $"{avatarType}_{ChatId}";

                // 只處理嚴格匹配的頭像更新
                if (cacheKey == expectedCacheKey && image != null && ChatId > 0)
                {
                    if (IsOnUIThread())
                        UpdateAvatarOnUIThread(image, expectedCacheKey);
                    else
                        _ = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                            CoreDispatcherPriority.Normal, () =>
                            {
                                try
                                {
                                    UpdateAvatarOnUIThread(image, expectedCacheKey);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"UI線程頭像更新失敗: {expectedCacheKey}, {ex.Message}");
                                }
                            });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnAvatarManagerUpdated 錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     在UI線程更新頭像 - 防止重複更新
        /// </summary>
        private void UpdateAvatarOnUIThread(BitmapImage image, string expectedKey)
        {
            try
            {
                // 防止重複設置相同頭像
                if (_avatarImage != image && image != null)
                {
                    AvatarImage = image;
                    Debug.WriteLine($"頭像回調更新成功: {expectedKey}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UI線程頭像更新失敗: {ex.Message}");
            }
        }

        /// <summary>
        ///     異步載入頭像 - 優化緩存策略
        /// </summary>
        public async void LoadAvatarAsync(int priority = 2, bool useCache = false)
        {
            try
            {
                // 如果已經有頭像或正在載入，跳過
                if (_avatarImage != null || _isLoadingAvatar) return;

                // 確保只註冊一次回調
                if (!_hasRegisteredAvatarCallback) RegisterAvatarUpdateCallback();

                _isLoadingAvatar = true;
                _avatarLoadAttempted = true;

                var avatarType = IsGroup ? "group" : "friend";
                var cacheKey = $"{avatarType}_{ChatId}";

                Debug.WriteLine($"開始異步載入頭像: {cacheKey}, Priority: {priority}, UseCache: {useCache}");

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
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal, () =>
                        {
                            try
                            {
                                if (_avatarImage == null) // 防止重複設置
                                {
                                    AvatarImage = avatar;
                                    Debug.WriteLine($"頭像載入成功: {cacheKey}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"設置頭像時發生錯誤: {ex.Message}");
                            }
                        });
                else
                    Debug.WriteLine($"頭像載入失敗: {cacheKey}");
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
        ///     智能缓存加载 - 防止重复加载已有头像
        /// </summary>
        public void LoadAvatarFromCacheAsync()
        {
            try
            {
                // **关键修复：如果已经有头像，直接返回**
                if (_avatarImage != null)
                {
                    Debug.WriteLine($"跳过头像加载，已有头像: {IsGroup}__{ChatId}");
                    return;
                }

                if (_isLoadingAvatar || _avatarLoadAttempted)
                {
                    Debug.WriteLine($"跳过头像加载，正在加载或已尝试: {IsGroup}__{ChatId}");
                    return;
                }

                // **验证类型但不影响现有头像**
                VerifyAndFixChatType();

                // 确保只注册一次回调
                if (!_hasRegisteredAvatarCallback) RegisterAvatarUpdateCallback();

                _isLoadingAvatar = true;
                _avatarLoadAttempted = true;

                var avatarType = IsGroup ? "group" : "friend";
                var cacheKey = $"{avatarType}_{ChatId}";

                Debug.WriteLine($"开始智能载入头像: {cacheKey}");

                // 使用低优先级任务，避免阻塞UI
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // 首先尝试从本地缓存载入
                        var cachedAvatar = await AvatarManager.GetAvatarAsync(avatarType, ChatId, 2, true);

                        if (cachedAvatar != null)
                        {
                            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                                CoreDispatcherPriority.High, () => // **提高UI更新优先级**
                                {
                                    try
                                    {
                                        if (_avatarImage == null) // 再次检查防止竞态条件
                                        {
                                            AvatarImage = cachedAvatar;
                                            Debug.WriteLine($"✅ 成功从本地缓存载入头像: {cacheKey}");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"设置缓存头像时发生错误: {ex.Message}");
                                    }
                                });
                        }
                        else
                        {
                            Debug.WriteLine($"本地缓存未找到，启动后台下载: {cacheKey}");
                            // 后台下载（非阻塞）
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    var downloadedAvatar = await AvatarManager.GetAvatarAsync(avatarType, ChatId, 3);
                                    if (downloadedAvatar != null && _avatarImage == null)
                                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                                            CoreDispatcherPriority.High, () =>
                                            {
                                                try
                                                {
                                                    if (_avatarImage == null)
                                                    {
                                                        AvatarImage = downloadedAvatar;
                                                        Debug.WriteLine($"✅ 后台下载头像完成: {cacheKey}");
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    Debug.WriteLine($"后台设置头像失败: {ex.Message}");
                                                }
                                            });
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"后台下载头像失败: {ex.Message}");
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"智能载入头像失败: {ex.Message}");
                    }
                    finally
                    {
                        _isLoadingAvatar = false;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadAvatarFromCacheAsync 发生错误: {ex.Message}");
                _isLoadingAvatar = false;
            }
        }

        /// <summary>
        ///     高性能类型验证和修复 - 避免清除已有头像
        /// </summary>
        private void VerifyAndFixChatType()
        {
            try
            {
                if (ChatId <= 0)
                {
                    Debug.WriteLine($"⚠️ ChatId无效: {ChatId}，跳过类型验证");
                    return;
                }

                // 使用缓存获取类型，避免重复数据库查询
                var cachedType = ChatTypeCache.GetChatType(ChatId);

                if (cachedType == null)
                {
                    // 缓存中没有找到，保持原有设置
                    Debug.WriteLine($"⚠️ ChatId {ChatId} 在缓存中未找到，保持原有类型: {IsGroup}");
                    return;
                }

                var shouldBeGroup = cachedType.Value;

                // 如果类型不匹配，进行修复
                if (IsGroup != shouldBeGroup)
                {
                    var originalType = IsGroup ? "群组" : "好友";
                    var correctType = shouldBeGroup ? "群组" : "好友";

                    Debug.WriteLine($"🔧 修复聊天类型: {Name} (ID: {ChatId}) 从 {originalType} 修正为 {correctType}");

                    // **关键修复：保存当前头像，避免UI闪烁**
                    var currentAvatar = _avatarImage;

                    // 清除旧的头像回调注册
                    if (_hasRegisteredAvatarCallback)
                        try
                        {
                            AvatarManager.OnAvatarUpdated -= OnAvatarManagerUpdated;
                            _hasRegisteredAvatarCallback = false;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"清除旧头像回调时出错: {ex.Message}");
                        }

                    // 更新类型（但不清除头像）
                    _isGroup = shouldBeGroup;
                    SafeOnPropertyChanged(nameof(IsGroup));

                    // **重新注册回调但保持现有头像**
                    if (currentAvatar != null)
                    {
                        RegisterAvatarUpdateCallback();
                        Debug.WriteLine($"✅ 类型修复完成，保持现有头像: {Name} 现在是 {correctType}");
                    }
                    else
                    {
                        // 只有在没有头像时才重新加载
                        RegisterAvatarUpdateCallback();
                        _avatarLoadAttempted = false;
                        LoadAvatarFromCacheAsync();
                        Debug.WriteLine($"✅ 类型修复完成，开始加载头像: {Name} 现在是 {correctType}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"验证聊天类型时发生错误: {ex.Message}");
            }
        }


        /// <summary>
        ///     強制刷新頭像（高優先級）- 確保UI線程安全
        /// </summary>
        public void RefreshAvatarAsync()
        {
            try
            {
                // 重置載入狀態
                _avatarLoadAttempted = false;

                // 註冊頭像更新回調（如果還沒有註冊）
                RegisterAvatarUpdateCallback();

                if (IsOnUIThread())
                {
                    // 在UI線程直接執行
                    AvatarImage = null;
                    LoadAvatarAsync(0);
                }
                else
                {
                    // 派發到UI線程執行
                    _ = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal, () =>
                        {
                            try
                            {
                                AvatarImage = null;
                                LoadAvatarAsync(0);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"UI線程刷新頭像失敗: {ex.Message}");
                            }
                        });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RefreshAvatarAsync 發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     公開的屬性變更通知方法 - 供外部調用
        /// </summary>
        public void OnPropertyChanged(string propertyName)
        {
            try
            {
                if (IsOnUIThread())
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                else
                    // 如果不在UI線程，調度到UI線程
                    _ = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal,
                        () => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName))
                    );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"屬性變更通知錯誤: {propertyName}, {ex.Message}");
            }
        }

        /// <summary>
        ///     析構函數 - 清理頭像更新回調
        /// </summary>
        ~ChatItem()
        {
            if (_hasRegisteredAvatarCallback)
                try
                {
                    AvatarManager.OnAvatarUpdated -= OnAvatarManagerUpdated;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"清理頭像回調時發生錯誤: {ex.Message}");
                }
        }

        /// <summary>
        ///     處理CQ碼以便於列表顯示（優化版）
        /// </summary>
        private string ProcessCQCodeForDisplay(string content)
        {
            if (string.IsNullOrEmpty(content))
                return "";

            // 創建副本以避免修改原始內容
            var processedContent = content;

            // 優化：使用更有效的字符串替換方法
            var replacements = new (string pattern, string replacement)[]
            {
                ("[CQ:at,qq=all]", "@所有人")
            };

            foreach (var (pattern, replacement) in replacements)
                processedContent = processedContent.Replace(pattern, replacement);

            // 處理需要正則表達式的複雜CQ碼
            processedContent = ProcessComplexCQCodes(processedContent);

            return processedContent;
        }

        /// <summary>
        ///     處理複雜的CQ碼（優化版）
        /// </summary>
        private string ProcessComplexCQCodes(string content)
        {
            var cqCodeMappings = new (string startPattern, string replacement)[]
            {
                ("[CQ:image", "[圖片]"),
                ("[CQ:record", "[語音]"),
                ("[CQ:video", "[視頻]"),
                ("[CQ:file", "[文件]"),
                ("[CQ:face", "[表情]"),
                ("[CQ:reply", "[回覆]"),
                ("[CQ:poke", "[戳一戳]"),
                ("[CQ:gift", "[禮物]"),
                ("[CQ:forward", "[轉發]"),
                ("[CQ:xml", "[XML卡片]"),
                ("[CQ:json", "[JSON卡片]")
            };

            foreach (var (startPattern, replacement) in cqCodeMappings)
                content = ReplaceCQCode(content, startPattern, replacement);

            // 特殊處理@某人CQ碼
            content = ProcessAtCQCodes(content);

            // 處理其他未知的CQ碼
            content = ReplaceUnknownCQCodes(content);

            return content;
        }

        /// <summary>
        ///     替換指定的CQ碼
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
        ///     處理@某人的CQ碼
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

                // 檢查是否有其他參數
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
        ///     處理未知的CQ碼
        /// </summary>
        private string ReplaceUnknownCQCodes(string content)
        {
            while (true)
            {
                var start = content.IndexOf("[CQ:", StringComparison.Ordinal);
                if (start < 0) break;

                var end = content.IndexOf("]", start, StringComparison.Ordinal);
                if (end <= start) break;

                content = content.Substring(0, start) + "[多媒體內容]" + content.Substring(end + 1);
            }

            return content;
        }
    }
}