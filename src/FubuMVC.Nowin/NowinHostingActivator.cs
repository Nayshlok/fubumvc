﻿using System;
using System.Collections.Generic;
using FubuMVC.Core;
using FubuMVC.Core.Diagnostics.Packaging;

namespace FubuMVC.Nowin
{
    public class NowinHostingActivator : IActivator
    {
        private readonly FubuRuntime _runtime;
        private readonly NowinSettings _settings;

        public NowinHostingActivator(FubuRuntime runtime, NowinSettings settings)
        {
            _runtime = runtime;
            _settings = settings;
        }

        public void Activate(IActivationLog log)
        {
            if (!_settings.AutoHostingEnabled)
            {
                log.Trace("Embedded Nowin hosting is not enabled");
                return;
            }

            Console.WriteLine("Starting Nowin hosting at port " + _settings.Port);
            log.Trace("Starting Nowin hosting at port " + _settings.Port);

            _settings.EmbeddedServer = new EmbeddedFubuMvcServer(_runtime, port:_settings.Port);
        }
    }
}