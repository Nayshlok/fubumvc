﻿using System;
using FubuMVC.Core;
using FubuMVC.Core.Diagnostics.Packaging;

namespace FubuMVC.Katana
{
    public class KatanaHostingDeactivator : IDeactivator
    {
        private readonly KatanaSettings _settings;

        public KatanaHostingDeactivator(KatanaSettings settings)
        {
            _settings = settings;
        }

        public void Deactivate(IActivationLog log)
        {
            if (_settings.EmbeddedServer != null)
            {
                Console.WriteLine("Shutting down the embedded Katana server");
                log.Trace("Shutting down the embedded Katana server");
                _settings.EmbeddedServer.Dispose();
            }
        }
    }
}