using System.Threading.Tasks;

namespace AnnaMessager.Core.Services
{
    /// <summary>
    ///     用戶交互服務接口 - 處理對話框、確認框等
    /// </summary>
    public interface IUserInteractionService
    {
        /// <summary>
        ///     顯示警告對話框
        /// </summary>
        Task ShowAlertAsync(string title, string message, string buttonText = "確定");

        /// <summary>
        ///     顯示確認對話框
        /// </summary>
        Task<bool> ShowConfirmAsync(string title, string message, string confirmText = "確定", string cancelText = "取消");

        /// <summary>
        ///     顯示輸入對話框
        /// </summary>
        Task<string> ShowInputAsync(string title, string message, string placeholder = "", string defaultValue = "");

        /// <summary>
        ///     顯示選擇對話框
        /// </summary>
        Task<int> ShowActionSheetAsync(string title, string message, params string[] options);

        /// <summary>
        ///     顯示載入對話框
        /// </summary>
        Task ShowLoadingAsync(string message = "載入中...");

        /// <summary>
        ///     隱藏載入對話框
        /// </summary>
        Task HideLoadingAsync();
    }
}