using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Common.CalcDto {
    [Serializable]
    public class CalcLocationDto
    {
        public CalcLocationDto([NotNull]string name, int id, [NotNull]string guid)
        {
            Name = name;
            ID = id;
            Guid = guid;
        }
        [NotNull]
        public string Name { get; }
        public int ID { get; }
        [NotNull]
        public string Guid { get; }
        [ItemNotNull]
        [NotNull]
        public List<CalcDeviceDto > LightDevices { get; } = new List<CalcDeviceDto>();

        public void AddLightDevice([NotNull]CalcDeviceDto clightdevice)
        {
            LightDevices.Add(clightdevice);
        }
    }
}