﻿using AH.Control.Api.Business;
using AH.Control.Api.Entities;
using AH.Protocol.Lan;
using AH.Protocol.Lan.Message;
using AH.Protocol.Library;
using AH.Protocol.Library.Message;
using AH.Protocol.Library.Module;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace AH.Control.Api.Protocol
{
    public class AutoHomeProtocol
    {
        public AutoHomeOptions Options { get; set; }
        public ModuleComponent ModuleComponent { get; set; }
        public AhProtocol AutoHome { get; set; }
        public LanProtocol Lan { get; set; }
        public LedRibbonRGBCenter LedRibbonRgb { get; set; }

        public AutoHomeProtocol(IOptions<AutoHomeOptions> options, ModuleComponent moduleComponent)
        {
            Options = options.Value;
            ModuleComponent = moduleComponent;

            LedRibbonRgb = new LedRibbonRGBCenter(this, moduleComponent);

            Lan = new LanProtocol(Options.ReceivePort, Options.SendPort);
            AutoHome = new AhProtocol(Options.UID, Lan);
            AutoHome.Receiver += AutoHome_Receiver;
        }

        public void BroadcastInfoRequest()
        {
            var host = Dns.GetHostEntryAsync(Dns.GetHostName()).GetAwaiter().GetResult();

            foreach (var address in host.AddressList.Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork))
            {
                var ip = address.GetAddressBytes();
                ip[3] = 255;
                var send = new IPAddress(ip);

                var msg = new LanMessage(0, send, MessageType.InfoRequest);
                AutoHome.Send(msg);
            }
        }

        public void InfoRequest(ModuleEntity entity)
        {
            var address = new IPAddress(entity.Address);
            var msg = new LanMessage(entity.UID, address, MessageType.InfoRequest);
            AutoHome.Send(msg);
        }

        private void AutoHome_Receiver(MessageBase message)
        {
            var lanMsg = message as LanMessage;
            switch (lanMsg.Type)
            {
                case MessageType.InfoResponse: ProcessInfoResponse(lanMsg); break;
                case MessageType.ModuleMessage: ProcessModuleMessage(lanMsg); break;
                case MessageType.ApiPing: SendPong(lanMsg); break;
            }
        }

        private void ProcessInfoResponse(LanMessage message)
        {
            var content = new InfoContent();
            message.ParseContent(content);

            ModuleComponent.UpdateAddressForUID(message.SenderUID, message.SenderIPAddress, content);
        }

        private void ProcessModuleMessage(LanMessage message)
        {
            var entity = ModuleComponent.GetByUID(message.SenderUID);
            if (entity == null)
                return;

            switch (entity.Type)
            {
                case ModuleType.LedRibbonRgb: LedRibbonRgb.ProcessMessage(entity, message); break;
            }
        }

        private void SendPong(LanMessage message)
        {
            var content = new PongContent(Options.ApiPort);
            var msg = new LanMessage(message.SenderUID, message.SenderIPAddress, MessageType.ApiPong, content);
            AutoHome.Send(msg);
        }

        public void SendValue(ModuleEntity entity)
        {
            switch (entity.Type)
            {
                case ModuleType.LedRibbonRgb: LedRibbonRgb.SendValue(entity); break;
            }
        }
    }
}