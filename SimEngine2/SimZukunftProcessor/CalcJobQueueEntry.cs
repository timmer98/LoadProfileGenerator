﻿using System.IO;
using JetBrains.Annotations;

namespace SimEngine2.SimZukunftProcessor {
    public class CalcJobQueueEntry {
        public CalcJobQueueEntry([NotNull] FileInfo jsonFile, int index)
        {
            JsonFile = jsonFile;
            Index = index;
        }

        [NotNull]
        public FileInfo JsonFile { get; set; }
        public int Index { get; set; }
    }
}