﻿//-----------------------------------------------------------------------

// <copyright>
//
// Copyright (c) TU Chemnitz, Prof. Technische Thermodynamik
// Written by Noah Pflugradt.
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
//
// Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//  Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer
// in the documentation and/or other materials provided with the distribution.
//  All advertising materials mentioning features or use of this software must display the following acknowledgement:
//  “This product includes software developed by the TU Chemnitz, Prof. Technische Thermodynamik and its contributors.”
//  Neither the name of the University nor the names of its contributors may be used to endorse or promote products
//  derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE UNIVERSITY 'AS IS' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING,
// BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE UNIVERSITY OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, S
// PECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; L
// OSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT,
// STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
// ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

// </copyright>

//-----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Media;
using CalculationEngine.Helper;
using CalculationEngine.Transportation;
using Common;
using Common.Enums;
using Common.JSON;
using JetBrains.Annotations;

namespace CalculationEngine.HouseholdElements {
    [SuppressMessage("ReSharper", "ConvertToAutoProperty")]
    public abstract class CalcAffordanceBase : CalcBase, ICalcAffordanceBase {
        private static int _calcAffordanceBaseSerialTracker;

        private readonly ActionAfterInterruption _actionAfterInterruption;
        [ItemNotNull] [NotNull] private readonly BitArray _isBusyArray;

        protected CalcAffordanceBase([NotNull] string pName,  [NotNull] CalcLocation loc,
                                     [NotNull] [ItemNotNull] List<CalcDesire> satisfactionvalues,
                                     int miniumAge, int maximumAge, PermittedGender permittedGender, bool needsLight,
                                     bool randomEffect,
                                     [NotNull] string pAffCategory, bool isInterruptable, bool isInterrupting,
                                     ActionAfterInterruption actionAfterInterruption, int weight,
                                     bool requireAllAffordances,
                                     CalcAffordanceType calcAffordanceType, [NotNull] CalcParameters calcParameters,
                                     [NotNull] string guid,
                                     [ItemNotNull] [NotNull] BitArray isBusyArray,
                                         [CanBeNull] CalcSite site = null) : base(pName, guid)
        {
            CalcAffordanceType = calcAffordanceType;
            Site = site;
            ParentLocation = loc;
            Satisfactionvalues = satisfactionvalues;
            _isBusyArray = new BitArray(calcParameters.InternalTimesteps);
            //copy to make sure that it is a separate instance
            for (var i = 0; i < isBusyArray.Length; i++)
            {
                _isBusyArray[i] = isBusyArray[i];
            }
            Weight = weight;
            RequireAllAffordances = requireAllAffordances;
            MiniumAge = miniumAge;
            MaximumAge = maximumAge;
            PermittedGender = permittedGender;
            NeedsLight = needsLight;
            RandomEffect = randomEffect;
            AffCategory = pAffCategory;
            IsInterruptable = isInterruptable;
            IsInterrupting = isInterrupting;
            _actionAfterInterruption = actionAfterInterruption;
            CalcAffordanceSerial = _calcAffordanceBaseSerialTracker;
#pragma warning disable S3010 // Static fields should not be updated in constructors
            _calcAffordanceBaseSerialTracker++;
#pragma warning restore S3010 // Static fields should not be updated in constructors
        }

        [SuppressMessage("ReSharper", "MemberCanBeProtected.Global")]
        [NotNull]
        [ItemNotNull]
        public BitArray IsBusyArray => _isBusyArray;

        [SuppressMessage("ReSharper", "UnusedParameter.Global")]
        public abstract void Activate([NotNull] TimeStep startTime, [NotNull] string activatorName,
                                      [NotNull] CalcLocation personSourceLocation,
                                      [NotNull] out ICalcProfile personTimeProfile);

        [NotNull]
        public string AffCategory { get; }

        public abstract Color AffordanceColor { get; }

        public ActionAfterInterruption AfterInterruption => _actionAfterInterruption;

        [CanBeNull]
        public abstract string AreDeviceProfilesEmpty();

        public abstract bool AreThereDuplicateEnergyProfiles();

        public int CalcAffordanceSerial { get; }

        public CalcAffordanceType CalcAffordanceType { get; }

        [NotNull]
        [ItemNotNull]
        public abstract List<CalcSubAffordance> CollectSubAffordances([NotNull] TimeStep time, [NotNull] NormalRandom nr,
                                                                      bool onlyInterrupting,
                                                                      [NotNull] Random r,
                                                                      [NotNull] CalcLocation srcLocation);

        public abstract int DefaultPersonProfileLength { get; }

        [CanBeNull]
        [ItemNotNull]
        public abstract List<CalcAffordance.DeviceEnergyProfileTuple> Energyprofiles { get; }

        //public abstract ICalcProfile CollectPersonProfile();

        [SuppressMessage("ReSharper", "UnusedParameter.Global")]
        public abstract bool IsBusy([NotNull] TimeStep time, [NotNull] NormalRandom nr, [NotNull] Random r,
                                    [NotNull] CalcLocation srcLocation, [NotNull] string calcPersonName,
                                    bool clearDictionaries = true);

        public bool IsInterruptable { get; }

        public bool IsInterrupting { get; }

        public int MaximumAge { get; }

        public int MiniumAge { get; }

        public bool NeedsLight { get; }

        [NotNull]
        public CalcLocation ParentLocation { get; }

        public PermittedGender PermittedGender { get; }

        [NotNull]
        public string PrettyNameForDumping => Name;

        //public abstract int PersonProfileDuration { get; }

        public bool RandomEffect { get; }

        public bool RequireAllAffordances { get; }

        [NotNull]
        [ItemNotNull]
        public List<CalcDesire> Satisfactionvalues { get; }

        [CanBeNull]
        public CalcSite Site { get; }

        [NotNull]
        public abstract string SourceTrait { get; }

        [NotNull]
        [ItemNotNull]
        public abstract List<CalcSubAffordance> SubAffordances { get; }

        [CanBeNull]
        public abstract string TimeLimitName { get; }

        public int Weight { get; }
    }
}