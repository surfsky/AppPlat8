using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace App.Pages.Chats
{
    /// <summary>
    /// 简单的 SignalR 聊天 Hub
    /// </summary>
    public class ChatHub : Hub
    {
        //---------------------------------------------------------
        // 供客户端调用的方法
        //---------------------------------------------------------
        /// <summary>供客户端调用的方法</summary>
        public async void HiServer(string user, string message)
        {
            var caller = Clients.Caller;
            await Broadcast(user, message);
        }

        //---------------------------------------------------------
        // 服务器方方法
        //---------------------------------------------------------
        /// <summary>给所有客户端广播</summary>
        public async Task Broadcast(string user, string message)
        {
            Thread.Sleep(2000);
            await Clients.All.SendAsync("ChatMessage", user, message, DateTime.Now.ToString("HH:mm:ss"));
        }

        /// <summary>给某个用户发消息</summary>
        public async Task SendTo(string userId, string user, string message)
        {
            Thread.Sleep(2000);
            await Clients.User(userId).SendAsync("ChatMessage", user, message, DateTime.Now.ToString("HH:mm:ss"));
        }

        /// <summary>给某个组发消息</summary>
        public async Task SendToGroup(string groupId, string user, string message)
        {
            Thread.Sleep(2000);
            await Clients.Group(groupId).SendAsync("ChatMessage", user, message, DateTime.Now.ToString("HH:mm:ss"));
        }
    }
}
