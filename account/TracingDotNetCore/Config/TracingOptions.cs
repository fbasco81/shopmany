﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TracingDotNetCore.Config
{
    public class TracingOptions
    {
        public string TracerTarget { get; set; } = ""; //, // Jaeger, "datadog"
        public bool EnableOpenTracingAutoTracing { get; set; } = true;
    }
}
