/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using CitadelCore.IO;
using CitadelCore.Net.Http;
using CitadelCore.Net.Proxy;
using CitadelCore.Windows.Net.Proxy;
using FilterProvider.Common.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CVProxyConfiguration = FilterProvider.Common.Data.ProxyConfiguration;
using CVHttpMessageInfo = FilterProvider.Common.Data.HttpMessageInfo;
using CVMessageType = FilterProvider.Common.Data.MessageType;
using CVProxyNextAction = FilterProvider.Common.Data.ProxyNextAction;
using Citadel.Core.WinAPI;
using CitadelService.Services;

namespace CitadelService.Platform
{
    public class WindowsProxyServerWrapper : IProxyServer
    {
        private ProxyServer m_proxyServer;

        private CVProxyConfiguration m_proxyConfig;

        private FilterServiceProvider m_provider;

        public WindowsProxyServerWrapper(FilterServiceProvider provider, CVProxyConfiguration config)
        {
            m_proxyConfig = config;
            m_provider = provider;

            m_proxyServer = new WindowsProxyServer(new ProxyServerConfiguration()
            {
                AuthorityName = config.AuthorityName,
                HttpMessageStreamedInspectionHandler = OnHttpMessageStreamedInspection,
                HttpMessageWholeBodyInspectionHandler = OnHttpMessageWholeBodyInspection,
                BadCertificateHandler = OnBadCertificate,
                NewHttpMessageHandler = OnHttpMessageBegin,
                FirewallCheckCallback = m_provider.OnAppFirewallCheck
            });
        }
        
        private CVProxyNextAction getCVProxyNextAction(ProxyNextAction action)
        {
            // This one's rather dirty because it assumes our common ProxyNextAction is going to always have
            // the same 
            return (CVProxyNextAction)Enum.Parse(typeof(CVProxyNextAction), action.ToString());
        }

        private CVHttpMessageInfo GetCVHttpMessageInfo(HttpMessageInfo messageInfo)
        {
            return new CVHttpMessageInfo()
            {
                Body = messageInfo.Body,
                ContentType = messageInfo.BodyContentType,
                Headers = messageInfo.Headers,
                MessageType = messageInfo.MessageType == MessageType.Request ? CVMessageType.Request : CVMessageType.Response,
                ProxyNextAction = getCVProxyNextAction(messageInfo.ProxyNextAction),
                StatusCode = messageInfo.StatusCode,
                Url = messageInfo.Url
            };
        }

        private void OnHttpMessageBegin(HttpMessageInfo messageInfo)
        {
            m_proxyConfig.NewHttpMessageHandler(GetCVHttpMessageInfo(messageInfo));
        }

        private void OnBadCertificate(HttpMessageInfo messageInfo)
        {
            m_proxyConfig.BadCertificateHandler(GetCVHttpMessageInfo(messageInfo));
        }

        private void OnHttpMessageWholeBodyInspection(HttpMessageInfo messageInfo)
        {
            m_proxyConfig.HttpMessageWholeBodyInspectionHandler(GetCVHttpMessageInfo(messageInfo));
        }

        private void OnHttpMessageStreamedInspection(HttpMessageInfo messageInfo, StreamOperation operation, Memory<byte> buffer, out bool dropConnection)
        {
            throw new NotImplementedException();
        }
        
        public bool IsRunning => m_proxyServer.IsRunning;

        public void Start()
        {
            m_proxyServer.Start();
        }

        public void Stop()
        {
            m_proxyServer.Stop();
        }
    }
}
