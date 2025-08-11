using System.ComponentModel;
using Windows.UI.Xaml.Media.Imaging;
using NapcatUWP.Tools;

namespace NapcatUWP.Models
{
    public class GroupInfo : INotifyPropertyChanged
    {
        private BitmapImage _avatarImage;
        private bool _isLoadingAvatar;

        public long GroupId { get; set; }
        public string GroupName { get; set; }
        public string GroupRemark { get; set; }
        public int MemberCount { get; set; }
        public int MaxMemberCount { get; set; }
        public bool GroupAllShut { get; set; }

        /// <summary>
        /// 头像图片
        /// </summary>
        public BitmapImage AvatarImage
        {
            get => _avatarImage;
            set
            {
                _avatarImage = value;
                OnPropertyChanged(nameof(AvatarImage));
                OnPropertyChanged(nameof(HasAvatar));
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
                _isLoadingAvatar = value;
                OnPropertyChanged(nameof(IsLoadingAvatar));
            }
        }

        /// <summary>
        /// 是否有头像图片
        /// </summary>
        public bool HasAvatar => _avatarImage != null;

        /// <summary>
        /// 异步加载头像
        /// </summary>
        public async void LoadAvatarAsync()
        {
            if (IsLoadingAvatar || HasAvatar)
                return;

            IsLoadingAvatar = true;

            try
            {
                var avatarImage = await AvatarManager.GetAvatarAsync("group", GroupId);

                if (avatarImage != null)
                {
                    AvatarImage = avatarImage;
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载群组头像失败: {ex.Message}");
            }
            finally
            {
                IsLoadingAvatar = false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}