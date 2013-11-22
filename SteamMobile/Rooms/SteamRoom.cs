﻿using System;
using System.Diagnostics;
using System.Net;
using EzSteam;
using SteamMobile.Packets;

namespace SteamMobile.Rooms
{
    public class SteamRoom : Room
    {
        public Chat Chat { get; private set; }

        private bool _hasConnected;
        private Stopwatch _lastMessage;

        public SteamRoom(RoomInfo roomInfo)
            : base(roomInfo)
        {
            _lastMessage = Stopwatch.StartNew();
        }

        public override void SendLine(HistoryLine line)
        {
            var chatLine = line as ChatLine;
            if (chatLine != null && Chat != null && chatLine.UserType == "RohBot")
            {
                Chat.Send(string.Format("[{0}] {1}", WebUtility.HtmlDecode(chatLine.Sender), WebUtility.HtmlDecode(chatLine.Content)));
            }

            base.SendLine(line);
        }

        public override void Send(string str)
        {
            if (Chat != null)
            {
                Chat.Send(str);
            }

            base.Send(str);
        }

        public override void Leave()
        {
            if (Chat != null)
            {
                Chat.Leave(ChatLeaveReason.Left);
                Chat = null;
            }

            base.Leave();
        }

        public override void SendHistory(Session session)
        {
            base.SendHistory(session);

            if (Chat == null)
            {
                session.Send(new SysMessage
                {
                    Date = Util.GetCurrentUnixTimestamp(),
                    Content = "Not connected to Steam."
                });
            }
        }

        public override void Update()
        {
            if (!IsActive)
            {
                if (Chat != null)
                    Chat.Leave(ChatLeaveReason.Left);

                return;
            }

            if (Chat != null && _lastMessage.Elapsed >= TimeSpan.FromMinutes(30))
            {
                _lastMessage.Restart();
                Chat.Leave(ChatLeaveReason.Disconnected);
                return;
            }

            if (Program.Steam.Status != Steam.ConnectionStatus.Connected || Chat != null)
                return;
            
            _hasConnected = false;
            Chat = Program.Steam.Bot.Join(ulong.Parse(RoomInfo["SteamId"]));

            Chat.OnEnter += sender =>
            {
                _hasConnected = true;
                Program.Logger.Info("Entered " + RoomInfo.ShortName);

                Program.SessionManager.Broadcast(new SysMessage
                {
                    Date = Util.GetCurrentUnixTimestamp(),
                    Content = "Connected to Steam."
                }, s => s.Room == RoomInfo.ShortName);
            };

            Chat.OnLeave += (sender, reason) =>
            {
                if (_hasConnected)
                {
                    Program.Logger.Info("Left " + RoomInfo.ShortName + ": " + reason);
                    _hasConnected = false;

                    Program.SessionManager.Broadcast(new SysMessage
                    {
                        Date = Util.GetCurrentUnixTimestamp(),
                        Content = "Lost connection to Steam."
                    }, s => s.Room == RoomInfo.ShortName);
                }

                Chat = null;
            };

            Chat.OnMessage += HandleMessage;
            Chat.OnUserEnter += HandleEnter;
            Chat.OnUserLeave += HandleLeave;
        }

        private void HandleMessage(Chat sender, Persona messageSender, string message)
        {
            _lastMessage.Restart();

            var senderName = messageSender.Name;
            var senderId = messageSender.Id.ConvertToUInt64().ToString("D");
            var inGame = messageSender.Playing != null && messageSender.Playing.ToUInt64() != 0;

            var line = new ChatLine(Util.GetCurrentUnixTimestamp(), RoomInfo.ShortName, "Steam", senderName, senderId, "", message, inGame);
            SendLine(line);

            Command.Handle(new CommandTarget(this, messageSender.Id), message, "~");
        }

        private void HandleEnter(Chat sender, Persona user)
        {
            _lastMessage.Restart();

            var message = user.Name + " entered chat.";

            var line = new StateLine(Util.GetCurrentUnixTimestamp(), RoomInfo.ShortName, "Enter", user.Name, user.Id.ConvertToUInt64().ToString("D"), "", "0", message);
            SendLine(line);
        }

        private void HandleLeave(Chat sender, Persona user, ChatLeaveReason reason, Persona sourceUser)
        {
            _lastMessage.Restart();

            var message = user.Name;
            switch (reason)
            {
                case ChatLeaveReason.Left:
                    message += " left chat.";
                    break;
                case ChatLeaveReason.Disconnected:
                    message += " disconnected.";
                    break;
                case ChatLeaveReason.Kicked:
                    message += string.Format(" was kicked by {0}.", sourceUser.Name);
                    break;
                case ChatLeaveReason.Banned:
                    message += string.Format(" was banned by {0}.", sourceUser.Name);
                    break;
            }

            var by = sourceUser != null ? sourceUser.Name : "";
            var byId = sourceUser != null ? sourceUser.Id.ConvertToUInt64().ToString("D") : "0";

            var line = new StateLine(Util.GetCurrentUnixTimestamp(), RoomInfo.ShortName, reason.ToString(), user.Name, user.Id.ConvertToUInt64().ToString("D"), by, byId, message);
            SendLine(line);
        }
    }
}
