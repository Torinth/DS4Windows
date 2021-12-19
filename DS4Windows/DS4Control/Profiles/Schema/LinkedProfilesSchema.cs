﻿using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using PropertyChanged;

namespace DS4WinWPF.DS4Control.Profiles.Schema
{
    [AddINotifyPropertyChangedInterface]
    public class LinkedProfiles : JsonSerializable<LinkedProfiles>
    {
        public Dictionary<PhysicalAddress, Guid> Assignments { get; set; } = new();
    }
}