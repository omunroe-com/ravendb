﻿using System.ComponentModel;
using Raven.Server.Config.Attributes;
using Raven.Server.Config.Settings;
using Sparrow;

namespace Raven.Server.Config.Categories
{
    public class HttpConfiguration : ConfigurationCategory
    {
        [Description("Set Kestrel's minimum required data rate in bytes per second. This option should configured together with 'Http.MinDataRateGracePeriod'")]
        [DefaultValue(null)]
        [SizeUnit(SizeUnit.Bytes)]
        [ConfigurationEntry("Http.MinDataRateBytesPerSec")]
        public Size? MinDataRatePerSecond { get; set; }

        [Description("Set Kestrel's allowed request and reponse grace in seconds. This option should configured together with 'Http.MinDataRateBytesPerSec'")]
        [DefaultValue(null)]
        [TimeUnit(TimeUnit.Seconds)]
        [ConfigurationEntry("Http.MinDataRateGracePeriodInSec")]
        public TimeSetting? MinDataRateGracePeriod { get; set; }

        [Description("Set Kestrel's MaxRequestBufferSize")]
        [DefaultValue(null)]
        [SizeUnit(SizeUnit.Kilobytes)]
        [ConfigurationEntry("Http.MaxRequestBufferSizeInKb")]
        public Size? MaxRequestBufferSize { get; set; }
    }
}