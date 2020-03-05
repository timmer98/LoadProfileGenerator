﻿using Common;
using JetBrains.Annotations;

namespace ChartCreator2.PDF {
    internal class SumProfilePages : ChartPageBase {
        public SumProfilePages():base(
            "This shows the energy use during the simulation.",
            "SumProfiles",
            "SumProfiles.*.png",
            "Sum Profiles"
            ) {
            MyTargetDirectory = TargetDirectory.Charts;
        }

        [NotNull]
        protected override string GetGraphTitle([NotNull] string filename) {
            var arr = filename.Split('.');

            return "Summed up curve for " + arr[1] + " from " + filename;
        }
    }
}