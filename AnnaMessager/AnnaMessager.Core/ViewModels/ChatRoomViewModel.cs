using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AnnaMessager.Core.ViewModels
{
    /// <summary>
    /// Line 風格聊天室專用 ViewModel (目前直接繼承 ChatViewModel，後續可擴充表情 / 附件 / 轉發等 UI 狀態屬性)
    /// </summary>
    // [System.Obsolete("Use ChatViewModel instead")] 
    // public class ChatRoomViewModel : ChatViewModel
    // {
    //     public ChatRoomViewModel() : base() { }

    //     public override async Task Initialize()
    //     {
    //         Debug.WriteLine("[ChatRoomViewModel] Initialize 進入, ChatId=" + ChatId + ", IsGroup=" + IsGroup + ", ChatName=" + ChatName);
    //         await base.Initialize();
    //         // 保障 MemberCount / 自身帳號在首次失敗時再補取一次
    //         if (IsGroup && MemberCount == 0)
    //         {
    //             try
    //             {
    //                 var info = await MvvmCross.Platform.Mvx.Resolve<Services.IOneBotService>().GetGroupInfoAsync(ChatId, true);
    //                 if (info?.Status == "ok" && info.Data != null)
    //                 {
    //                     MemberCount = info.Data.MemberCount;
    //                     if (string.IsNullOrEmpty(ChatName)) ChatName = info.Data.GroupName;
    //                     Debug.WriteLine($"[ChatRoomViewModel] 二次補取群組資訊成功: ChatId={ChatId}, MemberCount={MemberCount}, Name={ChatName}");
    //                 }
    //                 else
    //                 {
    //                     Debug.WriteLine($"[ChatRoomViewModel] 二次補取群組資訊失敗/無資料 ChatId={ChatId}");
    //                 }
    //             }
    //             catch (Exception ex) { Debug.WriteLine("[ChatRoomViewModel] 重取群組信息失敗: " + ex.Message); }
    //         }
    //         Debug.WriteLine("[ChatRoomViewModel] Initialize 結束, Messages.Count=" + (Messages?.Count ?? 0));
    //     }

    //     // 繼承的媒體上傳命令: UploadImageCommand, UploadVoiceCommand, UploadVideoCommand
    //     // 後續可加入：Emoji 面板開關、附件面板開關、合併轉發預覽加載狀態等屬性。
    // }
}
