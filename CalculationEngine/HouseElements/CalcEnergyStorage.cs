//-----------------------------------------------------------------------

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
using System.Collections.Generic;
using Automation.ResultFiles;
using CalculationEngine.HouseholdElements;
using CalculationEngine.OnlineDeviceLogging;
using CalculationEngine.OnlineLogging;
using Common;
using Common.CalcDto;
using Common.JSON;
using JetBrains.Annotations;

namespace CalculationEngine.HouseElements {
    public class CalcEnergyStorage : CalcBase {
        private readonly OefcKey _devProcessorKey;

        [CanBeNull] private readonly EnergyStorageLogfile _elf;

        [NotNull] private readonly EnergyStorageHeaderEntry _headerEntry;

        [NotNull] private readonly HouseholdKey _householdKey;

        [NotNull] private readonly CalcLoadTypeDto _inputLoadType;

        private readonly double _maximumStorageRate;
        private readonly double _maximumWithdrawRate;
        private readonly double _minimumStorageRate;
        private readonly double _minimumWithdrawRate;

        [NotNull] private readonly IOnlineDeviceActivationProcessor _odap;

        [ItemNotNull] [NotNull]
        private readonly List<CalcEnergyStorageSignal> _signals = new List<CalcEnergyStorageSignal>();

        private readonly double _storageCapacity;
        private double _currentFillLevel;
        [CanBeNull] private TimeStep _currentTimeStep;
        private double _previousDelta;

        public CalcEnergyStorage([NotNull] string pName,  [NotNull] IOnlineDeviceActivationProcessor odap,
                                 [NotNull] CalcLoadTypeDto loadType,
                                 double maximumStorageRate, double maximumWithdrawRate, double minimumStorageRate,
                                 double minimumWithdrawRate,
                                 double initialFill, double storageCapacity, [CanBeNull] EnergyStorageLogfile elf,
                                 [NotNull] HouseholdKey householdKey, [NotNull] string guid)
            : base(pName,  guid)
        {
            _devProcessorKey = new OefcKey(householdKey, OefcDeviceType.Storage, guid, "-1", loadType.Guid,"Energy Storage");
            _odap = odap;
            _inputLoadType = loadType;
            _householdKey = householdKey;
            _odap.RegisterDevice(Name, _devProcessorKey, "Energy Storage Device", _inputLoadType);

            _maximumStorageRate = maximumStorageRate;
            _maximumWithdrawRate = maximumWithdrawRate;
            _minimumStorageRate = minimumStorageRate;
            _minimumWithdrawRate = minimumWithdrawRate;
            var correctInitialFill = initialFill / loadType.ConversionFactor;
            PreviousFillLevel = correctInitialFill;
            _storageCapacity = storageCapacity / loadType.ConversionFactor;
            _currentFillLevel = correctInitialFill;
            _elf = elf;
            var capacity = storageCapacity + " " + loadType.UnitOfSum + "; initial fill " + initialFill + " " +
                           loadType.UnitOfSum;
            _headerEntry = new EnergyStorageHeaderEntry(Name, capacity, _inputLoadType.FileName);

            _elf?.RegisterStorage(Name, _headerEntry);
        }

        public double PreviousFillLevel { get; private set; }

        public double StorageCapacity => _storageCapacity;

        public void AddSignal([NotNull] CalcEnergyStorageSignal signal)
        {
            _signals.Add(signal);
            signal.ProcessorKey =
                new OefcKey(_householdKey, OefcDeviceType.Signal, signal.Guid, Guid, signal.DstLoadType.Guid, "Energy Storage Signal");
            _odap.RegisterDevice(Name + signal, signal.ProcessorKey, string.Empty,
                signal.DstLoadType.ConvertToDto());
            _headerEntry.AddSignal(signal.ToString());
        }

        public bool ProcessOneTimestep([NotNull] [ItemNotNull] List<OnlineEnergyFileRow> fileRows, [NotNull] TimeStep timeStep,
                                       [CanBeNull] [ItemNotNull] List<string> log)
        {
            if (timeStep != _currentTimeStep) {
                _currentTimeStep = timeStep;
                PreviousFillLevel = _currentFillLevel;
                _previousDelta = double.MinValue;
            }

            var madeChanges = false;
            var fileRow = GetRowForLoadType(_inputLoadType, fileRows);
            if (fileRow == null) {
                return false;
            }

            var sumvalue = fileRow.SumFresh;
            var column = _odap.Oefc.GetColumnNumber(_inputLoadType, _devProcessorKey);
            sumvalue -= fileRow.EnergyEntries[column];
            // store
            double deltaAmount = 0;
            var storableAmount = _storageCapacity - PreviousFillLevel;
            if (sumvalue < 0 && PreviousFillLevel < _storageCapacity) {
                if (sumvalue * -1 >= _minimumStorageRate) {
                    if (sumvalue * -1 < storableAmount) {
                        deltaAmount = sumvalue * -1;
                    }
                    else {
                        deltaAmount = storableAmount;
                    }

                    if (deltaAmount > _maximumStorageRate) {
                        deltaAmount = _maximumStorageRate;
                    }
                }
                else {
                    deltaAmount = 0;
                }
            }

            // withdraw
            if (sumvalue > 0 && PreviousFillLevel > 0) {
                if (sumvalue >= _minimumWithdrawRate) {
                    if (sumvalue <= PreviousFillLevel) {
                        deltaAmount = sumvalue * -1;
                    }
                    else {
                        deltaAmount = PreviousFillLevel * -1;
                    }

                    if (deltaAmount * -1 > _maximumWithdrawRate) {
                        deltaAmount = _maximumWithdrawRate * -1;
                    }
                }
                else {
                    deltaAmount = 0;
                }
            }

            _currentFillLevel = PreviousFillLevel + deltaAmount;
            fileRow.EnergyEntries[column] = deltaAmount;
            if (Math.Abs(_previousDelta - deltaAmount) > 0.0000001) {
                log?.Add(Name + " set fill level to " + _currentFillLevel + " " + _inputLoadType.UnitOfSum);
                _previousDelta = deltaAmount;

                madeChanges = true;
                _elf?.SetValue(Name, _currentFillLevel, timeStep, _householdKey, _inputLoadType);
            }

            foreach (var signal in _signals) {
                var value = signal.GetValue(timeStep, _storageCapacity, _currentFillLevel);
                var row = GetRowForLoadType(signal.DstLoadType.ConvertToDto(), fileRows);
                if (row == null) {
                    throw new LPGException("Row could not be found");
                }

                var signalcolumn = _odap.Oefc.GetColumnNumber(signal.DstLoadType.ConvertToDto(),
                    signal.ProcessorKey);
                if (Math.Abs(row.EnergyEntries[signalcolumn] - value) > 0.0000001) {
                    madeChanges = true;
                    log?.Add(Name + " set signal " + signal.DstLoadType.Name + " to " + value);
                    row.EnergyEntries[signalcolumn] = value;
                }
            }

            return madeChanges;
        }

        [CanBeNull]
        private static OnlineEnergyFileRow GetRowForLoadType([NotNull] CalcLoadTypeDto lt,
                                                             [NotNull] [ItemNotNull] List<OnlineEnergyFileRow> fileRows)
        {
            foreach (var onlineEnergyFileRow in fileRows) {
                if (onlineEnergyFileRow.LoadType == lt) {
                    return onlineEnergyFileRow;
                }
            }

            return null;
        }
    }
}