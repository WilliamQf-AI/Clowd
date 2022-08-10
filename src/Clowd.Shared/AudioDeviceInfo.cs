﻿using System;

namespace Clowd
{
    public record AudioDeviceInfo
    {
        public string DeviceId { get; set; }
        public string DeviceType { get; set; }
    }

    public interface IAudioLevelListener : IDisposable
    {
        AudioDeviceInfo Device { get; }
        double PeakLevel { get; }
    }
}
