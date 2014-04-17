﻿using System;
using System.Diagnostics;
using System.Net;
using EzSteam;
using SteamKit2;

namespace SteamMobile.Rooms
{
    public class SteamRoom : Room
    {
        public Chat Chat { get; private set; }

        public readonly SteamID SteamId;
        public readonly bool EchoWebStates;

        private bool _hasConnected;
        private Stopwatch _lastMessage;

        public SteamRoom(RoomInfo roomInfo)
            : base(roomInfo)
        {
            _lastMessage = Stopwatch.StartNew();

            SteamId = new SteamID(ulong.Parse(RoomInfo["SteamId"]));
            EchoWebStates = (RoomInfo["EchoWebStates"] ?? "true").ToLower() == "true";
        }

        public override void SendLine(HistoryLine line)
        {
            var chatLine = line as ChatLine;
            if (chatLine != null && Chat != null && chatLine.UserType == "RohBot")
            {
                Chat.Send(string.Format("[{0}] {1}", WebUtility.HtmlDecode(chatLine.Sender), WebUtility.HtmlDecode(chatLine.Content)));
            }

            var stateLine = line as StateLine;
            if (EchoWebStates && stateLine != null && Chat != null && stateLine.ForType == "RohBot")
            {
                string fmt;
                switch (stateLine.State)
                {
                    case "Enter":
                        fmt = "<{0}> entered chat.";
                        break;
                    case "Left":
                        fmt = "<{0}> left chat.";
                        break;
                    case "Disconnected":
                        fmt = "<{0}> disconnected.";
                        break;
                    default:
                        fmt = null;
                        break;
                }

                if (fmt != null)
                {
                    Chat.Send(string.Format(fmt, WebUtility.HtmlDecode(stateLine.For)));
                }
            }

            if (stateLine != null && Chat != null && stateLine.State == "Action")
            {
                Chat.Send(stateLine.Content);
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

        public override void SendHistory(Connection connection)
        {
            base.SendHistory(connection);

            if (Chat == null)
                connection.SendSysMessage("Not connected to Steam.");
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
                Program.Logger.Info("Rejoining " + RoomInfo.ShortName);
                _lastMessage.Restart();
                Chat.Leave(ChatLeaveReason.Disconnected);
                return;
            }

            if (Program.Steam.Status != Steam.ConnectionStatus.Connected || Chat != null)
                return;
            
            _hasConnected = false;
            Chat = Program.Steam.Bot.Join(SteamId);

            Chat.OnEnter += sender =>
            {
                _hasConnected = true;
                Program.Logger.Info("Entered " + RoomInfo.ShortName);
                SendPersistentSysMessage("Connected to Steam.");
            };

            Chat.OnLeave += (sender, reason) =>
            {
                if (_hasConnected)
                {
                    _hasConnected = false;
                    Program.Logger.Info("Left " + RoomInfo.ShortName + ": " + reason);
                    SendPersistentSysMessage("Lost connection to Steam.");
                }

                Chat = null;
            };

            Chat.OnMessage += HandleMessage;
            Chat.OnUserEnter += HandleEnter;
            Chat.OnUserLeave += HandleLeave;
        }

        private void SendPersistentSysMessage(string str)
        {
            var line = new ChatLine(Util.GetCurrentTimestamp(), RoomInfo.ShortName, "Steam", Program.Settings.PersonaName, "0", "", str, false);
            base.SendLine(line);
        }

        private void HandleMessage(Chat sender, Persona messageSender, string message)
        {
            _lastMessage.Restart();

            var senderName = messageSender.Name;
            var senderId = messageSender.Id.ConvertToUInt64().ToString("D");
            var inGame = messageSender.Playing != null && messageSender.Playing.ToUInt64() != 0;

            var line = new ChatLine(Util.GetCurrentTimestamp(), RoomInfo.ShortName, "Steam", senderName, senderId, "", message, inGame);
            SendLine(line);

            Command.Handle(new CommandTarget(this, messageSender.Id), message, "~");
        }

        private void HandleEnter(Chat sender, Persona user)
        {
            _lastMessage.Restart();

            var message = user.Name + " entered chat.";

            var line = new StateLine(Util.GetCurrentTimestamp(), RoomInfo.ShortName, "Enter", user.Name, user.Id.ConvertToUInt64().ToString("D"), "Steam", "", "0", "", message);
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
            var byType = sourceUser != null ? "Steam" : "";

            var line = new StateLine(Util.GetCurrentTimestamp(), RoomInfo.ShortName, reason.ToString(), user.Name, user.Id.ConvertToUInt64().ToString("D"), "Steam", by, byId, byType, message);
            SendLine(line);
        }
    }
}
