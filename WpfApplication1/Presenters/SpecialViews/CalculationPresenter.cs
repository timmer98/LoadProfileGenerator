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

#region selected

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using System.Threading;
using Automation;
using Automation.ResultFiles;
using CalculationController.Queue;
using Common;
using Common.Enums;
using Common.JSON;
using Database;
using Database.Tables;
using Database.Tables.BasicElements;
using Database.Tables.Houses;
using Database.Tables.Transportation;
using JetBrains.Annotations;
using LoadProfileGenerator.Presenters.BasicElements;
using LoadProfileGenerator.Views.SpecialViews;

#endregion

namespace LoadProfileGenerator.Presenters.SpecialViews {

    public class LPGDispatcher : ILPGDispatcher {
        public Dispatcher Dispatcher { get; }

        public LPGDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }


        public void BeginInvoke(Delegate method, object arg)
        {
            throw new NotImplementedException();
        }

        public bool IsCorrectThread()
        {
            if (Thread.CurrentThread != Dispatcher.Thread)
            {
                return true;
            }
            return false;
        }
    }
    public class CalculationPresenter : PresenterBaseWithAppPresenter<CalculateView> {
        [JetBrains.Annotations.NotNull] private readonly ApplicationPresenter _applicationPresenter;
        [JetBrains.Annotations.NotNull] private readonly EnergyIntensityConverter _eic = new EnergyIntensityConverter();

        [CanBeNull] private string _dstPath;
        private bool _calculateTransportation;

        private TimeSpan _maximumInternalTimeResolution;

        [CanBeNull] private ICalcObject _selectedCalcObject;

        private CalcObjectType _selectedCalcObjectType;
        [CanBeNull] private ChargingStationSet _selectedChargingStationSet;
        [CanBeNull] private TransportationDeviceSet _selectedTransportationDeviceSet;
        [CanBeNull] private TravelRouteSet _selectedTravelRouteSet;
        private bool _isTransportationSettingsEnabled;

        public bool EnableIdleMode {
            get => Sim.MyGeneralConfig.EnableIdlemodeBool;

            set => Sim.MyGeneralConfig.EnableIdlemodeBool = value;
        }
        public CalculationPresenter([JetBrains.Annotations.NotNull] ApplicationPresenter applicationPresenter, [JetBrains.Annotations.NotNull] CalculateView view) : base(view, "Headerstring", applicationPresenter)
        {
            CalcObjects = new ObservableCollection<ICalcObject>();
            _applicationPresenter = applicationPresenter;
            DefaultTimeSteps = new ObservableCollection<string>();
            ExternalTimeSteps = new ObservableCollection<string>();
            // load the previously selected item again
            var cfg = Sim.MyGeneralConfig;
            SelectedCalcObjectType = CalcObjectTypeHelper.GetFromString(cfg.LastSelectedCalcType);
            foreach (var calcObject in CalcObjects) {
                if (calcObject.Name == Sim.MyGeneralConfig.LastSelectedCalcObject) {
                    SelectedCalcObject = calcObject;
                }
            }

            if (cfg.LastSelectedTransportationSetting == "TRUE") {
                CalculateTransportation = true;
            }
            else {
                CalculateTransportation = false;
            }

            if (!string.IsNullOrWhiteSpace(cfg.LastSelectedRouteSet)) {
                _selectedTravelRouteSet = Sim.TravelRouteSets.FindFirstByName(cfg.LastSelectedRouteSet) ?? Sim.TravelRouteSets[0];
            }

            if (!string.IsNullOrWhiteSpace(cfg.LastSelectedChargingStationSet)) {
                _selectedChargingStationSet = Sim.ChargingStationSets.FindFirstByName(cfg.LastSelectedChargingStationSet) ?? Sim.ChargingStationSets[0];
            }

            if (!string.IsNullOrWhiteSpace(cfg.LastSelectedTransportationDeviceSet)) {
                _selectedTransportationDeviceSet = Sim.TransportationDeviceSets.FindFirstByName(cfg.LastSelectedTransportationDeviceSet) ?? Sim.TransportationDeviceSets[0];
            }
            else {
                _selectedTransportationDeviceSet = Sim.TransportationDeviceSets.Items[0];
            }
        }

        [ItemNotNull]
        [JetBrains.Annotations.NotNull]
        [UsedImplicitly]
        public ObservableCollection<ICalcObject> CalcObjects { get; }

        [JetBrains.Annotations.NotNull]
        [UsedImplicitly]
        public Dictionary<CalcObjectType, string> CalcObjectTypes => CalcObjectTypeHelper.CalcObjectTypeEnumDictionary;

        [ItemNotNull]
        [JetBrains.Annotations.NotNull]
        [UsedImplicitly]
        public ObservableCollection<ChargingStationSet> ChargingStationSets => Sim.ChargingStationSets.Items;

        [ItemNotNull]
        [JetBrains.Annotations.NotNull]
        [UsedImplicitly]
        public ObservableCollection<string> DefaultTimeSteps { get; }

        [CanBeNull]
        [UsedImplicitly]
        public string DstPath {
            get => _dstPath;
            set {
                _dstPath = value;
                OnPropertyChanged(nameof(DstPath));
            }
        }

        [UsedImplicitly]
        public bool CalculateTransportation {
            get => _calculateTransportation;
            set {
                _calculateTransportation = value;
                if (_calculateTransportation) {
                    Sim.MyGeneralConfig.LastSelectedTransportationSetting = "TRUE";
                }
                else {
                    Sim.MyGeneralConfig.LastSelectedTransportationSetting = "FALSE";
                }

                Sim.MyGeneralConfig.SaveToDB();
                RecalcDstPath();
            }
        }

        [ItemNotNull]
        [JetBrains.Annotations.NotNull]
        [UsedImplicitly]
        public ObservableCollection<EnergyIntensityConverter.EnergyIntensityForDisplay> EnergyIntensities => _eic.All;

        [JetBrains.Annotations.NotNull]
        [UsedImplicitly]
        public EnergyIntensityConverter.EnergyIntensityForDisplay EnergyIntensity {
            get => _eic.GetAllDisplayElement(Sim.MyGeneralConfig.SelectedEnergyIntensity);
            set {
                Sim.MyGeneralConfig.SelectedEnergyIntensity = value.EnergyIntensityType;
                OnPropertyChanged(nameof(EnergyIntensity));
            }
        }

        [JetBrains.Annotations.NotNull]
        [UsedImplicitly]
        public string ExternalTimeResolution {
            get => Sim.MyGeneralConfig.ExternalTimeResolution;
            set {
                Sim.MyGeneralConfig.ExternalTimeResolution = value;
                OnPropertyChanged(nameof(ExternalTimeResolution));
            }
        }

        [ItemNotNull]
        [JetBrains.Annotations.NotNull]
        [UsedImplicitly]
        public ObservableCollection<string> ExternalTimeSteps { get; }

        [JetBrains.Annotations.NotNull]
        [UsedImplicitly]
        public GeneralConfig GConfig => Sim.MyGeneralConfig;

        [CanBeNull]
        [UsedImplicitly]
        public GeographicLocation GeographicLocation {
            get {
                var id = Sim.MyGeneralConfig.GeographicLocation;
                if (id != -1) {
                    foreach (var geoloc in Sim.GeographicLocations.Items) {
                        if (geoloc.IntID == id) {
                            return geoloc;
                        }
                    }
                }

                return null;
            }
            set {
                if (value == null) {
                    return;
                }

                Sim.MyGeneralConfig.GeographicLocation = value.IntID;
                OnPropertyChanged(nameof(GeographicLocation));
            }
        }

        [ItemNotNull]
        [JetBrains.Annotations.NotNull]
        [UsedImplicitly]
        public ObservableCollection<GeographicLocation> GeographicLocations => Sim.GeographicLocations.Items;

        [JetBrains.Annotations.NotNull]
        [UsedImplicitly]
        public string Headerstring => "Calculation";

        [JetBrains.Annotations.NotNull]
        [UsedImplicitly]
        public string InternalTimeResolution {
            get => Sim.MyGeneralConfig.InternalTimeResolution;
            set {
                Sim.MyGeneralConfig.InternalTimeResolution = value;
                var oldExternalTimeResolution = ExternalTimeResolution;

                var ts = Sim.MyGeneralConfig.InternalStepSize;
                ExternalTimeSteps.Clear();
                ExternalTimeSteps.Add(ts.ToString());
                var ts2 = new TimeSpan(ts.Ticks * 2);
                ExternalTimeSteps.Add(ts2.ToString());
                ts2 = new TimeSpan(ts.Ticks * 3);
                ExternalTimeSteps.Add(ts2.ToString());
                ts2 = new TimeSpan(ts.Ticks * 5);
                ExternalTimeSteps.Add(ts2.ToString());
                ts2 = new TimeSpan(ts.Ticks * 10);
                ExternalTimeSteps.Add(ts2.ToString());
                ts2 = new TimeSpan(ts.Ticks * 15);
                ExternalTimeSteps.Add(ts2.ToString());
                ts2 = new TimeSpan(ts.Ticks * 60);
                ExternalTimeSteps.Add(ts2.ToString());
                ts2 = new TimeSpan(ts.Ticks * 3600);
                ExternalTimeSteps.Add(ts2.ToString());
                ts2 = new TimeSpan(ts.Ticks * 86400);
                ExternalTimeSteps.Add(ts2.ToString());
                var sameresexists = false;
                foreach (var externalTimeStep in ExternalTimeSteps) {
                    if (externalTimeStep == oldExternalTimeResolution) {
                        sameresexists = true;
                    }
                }

                if (sameresexists) {
                    ExternalTimeResolution = oldExternalTimeResolution;
                }
                else {
                    ExternalTimeResolution = InternalTimeResolution;
                }

                OnPropertyChanged(nameof(InternalTimeResolution));
            }
        }

        [UsedImplicitly]
        public bool IsInCalc { get; private set; }

        [UsedImplicitly]
        public bool IsNotInCalc => !IsInCalc;



        [JetBrains.Annotations.NotNull]
        [UsedImplicitly]
        public Dictionary<LoadTypePriority, string> LoadTypePriorities => LoadTypePriorityHelper.LoadTypePriorityDictionaryAll;

        [UsedImplicitly]
        public TimeSpan MaximumInternalTimeResolution {
            get => _maximumInternalTimeResolution;
            set {
                _maximumInternalTimeResolution = value;
                OnPropertyChanged(nameof(MaximumInternalTimeResolution));
                UpdateInternalTimesteps();
            }
        }

        [CanBeNull]
        public string NameForJsonExportOutputDirectory { get; set; } = "My Export";

        public bool EnableFlexibility
        {
            get => Sim.MyGeneralConfig.EnableFlexibilityBool;

            set => Sim.MyGeneralConfig.EnableFlexibilityBool = value;
        }

        [JetBrains.Annotations.NotNull]
        [UsedImplicitly]
        public string RandomSeed {
            get {
                if (Sim.MyGeneralConfig.RandomSeed == -1) {
                    return "Randomized";
                }

                return Sim.MyGeneralConfig.RandomSeed.ToString(CultureInfo.CurrentCulture);
            }
        }

        [UsedImplicitly]
        public bool ResumeSettlement { get; set; }

        [CanBeNull]
        [UsedImplicitly]
        public ICalcObject SelectedCalcObject {
            get => _selectedCalcObject;
            set {
                _selectedCalcObject = value;
                if (_selectedCalcObject != null) {
                    Sim.MyGeneralConfig.LastSelectedCalcObject = _selectedCalcObject.Name;
                }
                Sim.MyGeneralConfig.SaveToDB();
                RecalcForCalcObject();
                OnPropertyChanged(nameof(SelectedCalcObjectType));
                OnPropertyChanged(nameof(SelectedCalcObject));
            }
        }

        public bool IsTransportationSettingsEnabled {
            get => _isTransportationSettingsEnabled;
            set {
                if (value == _isTransportationSettingsEnabled) {
                    return;
                }

                _isTransportationSettingsEnabled = value;
                OnPropertyChanged(nameof(IsTransportationSettingsEnabled));
            }
        }

        [UsedImplicitly]
        public CalcObjectType SelectedCalcObjectType {
            get => _selectedCalcObjectType;
            set {
                _selectedCalcObjectType = value;
                if (_selectedCalcObjectType == CalcObjectType.House) {
                    IsTransportationSettingsEnabled = false;
                }
                else {
                    IsTransportationSettingsEnabled = true;
                }

                Sim.MyGeneralConfig.LastSelectedCalcType = CalcObjectTypes[value];
                RefreshCalcObjectList();
            }
        }

        [CanBeNull]
        [UsedImplicitly]
        public ChargingStationSet SelectedChargingStationSet {
            get => _selectedChargingStationSet;
            set {
                _selectedChargingStationSet = value;
                if (_selectedChargingStationSet != null) {
                    Sim.MyGeneralConfig.LastSelectedChargingStationSet = value?.Name;
                }

                Sim.MyGeneralConfig.SaveToDB();
                RecalcDstPath();
            }
        }

        [CanBeNull]
        [UsedImplicitly]
        public TemperatureProfile SelectedTemperatureProfile {
            get {
                var id = Sim.MyGeneralConfig.SelectedTemperatureProfile;
                if (id != -1) {
                    foreach (var temperaturProfile in Sim.TemperatureProfiles.Items) {
                        if (temperaturProfile.IntID == id) {
                            return temperaturProfile;
                        }
                    }
                }

                return null;
            }
            set {
                if (value != null) {
                    Sim.MyGeneralConfig.SelectedTemperatureProfile = value.IntID;
                }
                else {
                    Sim.MyGeneralConfig.SelectedTemperatureProfile = -1;
                }

                OnPropertyChanged(nameof(SelectedTemperatureProfile));
            }
        }

        [CanBeNull]
        [UsedImplicitly]
        public TransportationDeviceSet SelectedTransportationDeviceSet {
            get => _selectedTransportationDeviceSet;
            set {
                _selectedTransportationDeviceSet = value;
                if (_selectedTransportationDeviceSet != null) {
                    Sim.MyGeneralConfig.LastSelectedTransportationDeviceSet = value?.Name;
                }

                Sim.MyGeneralConfig.SaveToDB();
                RecalcDstPath();
            }
        }

        [CanBeNull]
        [UsedImplicitly]
        public TravelRouteSet SelectedTravelRouteSet {
            get => _selectedTravelRouteSet;

            set {
                _selectedTravelRouteSet = value;
                if (_selectedTravelRouteSet != null) {
                    Sim.MyGeneralConfig.LastSelectedRouteSet = value?.Name;
                }

                Sim.MyGeneralConfig.SaveToDB();
                RecalcDstPath();
            }
        }

        [ItemNotNull]
        [JetBrains.Annotations.NotNull]
        [UsedImplicitly]
        public ObservableCollection<TemperatureProfile> TemperatureProfiles => Sim.TemperatureProfiles.Items;

        [JetBrains.Annotations.NotNull]
        [UsedImplicitly]
        public GeneralConfig ThisConfig => Sim.MyGeneralConfig;

        [ItemNotNull]
        [JetBrains.Annotations.NotNull]
        [UsedImplicitly]
        public ObservableCollection<TransportationDeviceSet> TransportationDeviceSets => Sim.TransportationDeviceSets.Items;

        [ItemNotNull]
        [JetBrains.Annotations.NotNull]
        [UsedImplicitly]
        public ObservableCollection<TravelRouteSet> TravelRouteSets => Sim.TravelRouteSets.Items;

        public override void Close(bool saveToDB, bool removeLast = false)
        {
            _applicationPresenter.CloseTab(this, removeLast);
        }

        public override bool Equals(object obj) => obj is CalculationPresenter;

        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wrap
            {
                const int hash = 17;
                // Suitable nullity checks etc, of course :)
                return hash * 23 + TabHeaderPath.GetHashCode();
            }
        }

        public void RefreshTargets()
        {
            RefreshCalcObjectList();
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public void RunSimulation([CanBeNull] string resultpath)
        {
            if (string.IsNullOrWhiteSpace(resultpath)) {
                Logger.Error("Please enter a path!");
                return;
            }

            if (SelectedTemperatureProfile == null) {
                Logger.Error("Please select a temperature profile!");
                return;
            }

            if (_selectedCalcObject == null) {
                Logger.Error("Please select something to calculate!");
                return;
            }

            if (GeographicLocation == null) {
                Logger.Error("Please select a geographic location!");
                return;
            }

            try {
                var path = new DirectoryInfo(resultpath);
                if (!path.Exists) {
                    path.Create();
                }

                var tmpfile = Path.Combine(resultpath, "temporaryfile.tmp");
                using (var sw = new StreamWriter(tmpfile)) {
                    sw.WriteLine("Test");
                    sw.Close();
                }

                File.Delete(tmpfile);
            }
            catch (Exception ex) {
                MessageWindowHandler.Mw.ShowInfoMessage(
                    "The destination path you entered for the simulation doesn't seem " + "to be writeable. The path is:" + Environment.NewLine + Environment.NewLine + resultpath +
                    Environment.NewLine + Environment.NewLine + "Are you sure this is actually the correct path? You can change the default path in the settings." + Environment.NewLine +
                    Environment.NewLine + "The Windows error message when trying to write to the path you specified was:" + Environment.NewLine + Environment.NewLine + ex.Message, "Error");
                Logger.Exception(ex);
                return;
            }

            SetIsInCalc(true);

            if (_selectedCalcObject == null) {
                Logger.Error("Please select a calc object!");
                return;
            }

            TransportationDeviceSet tds = null;
            TravelRouteSet trs = null;
            if (CalculateTransportation && _selectedCalcObject.CalcObjectType == CalcObjectType.ModularHousehold) {
                tds = SelectedTransportationDeviceSet;
                if (tds == null) {
                    Logger.Error("Please select a transportation device set!");
                    return;
                }

                trs = SelectedTravelRouteSet;
                if (trs == null) {
                    Logger.Error("Please select a travel route set!");
                    return;
                }
            }

            if (CalculateTransportation && _selectedCalcObject.CalcObjectType == CalcObjectType.House) {
                var house = (House)_selectedCalcObject;
                foreach (var mhh in house.Households) {
                    if (mhh.TransportationDeviceSet == null) {
                        MessageWindowHandler.Mw.ShowErrorMessage("No transportation device set in the house for household " + mhh.Name, "Can't calculate.");
                        return;
                    }

                    if (mhh.ChargingStationSet == null) {
                        MessageWindowHandler.Mw.ShowErrorMessage("No charging station set for household " + mhh.Name, "Can't calculate.");
                        return;
                    }

                    if (mhh.TravelRouteSet == null) {
                        MessageWindowHandler.Mw.ShowErrorMessage("No transportation device set for household " + mhh.Name, "Can't calculate.");
                        return;
                    }
                }
            }

            CalculationProfiler calculationProfiler = new CalculationProfiler();
            var csps = new CalcStartParameterSet(ReportFinishHouse, ReportFinishHousehold,
                // ReSharper disable once AssignNullToNotNullAttribute
                _applicationPresenter.OpenItem, new LPGDispatcher(_applicationPresenter.MainDispatcher), GeographicLocation,
                // ReSharper disable once AssignNullToNotNullAttribute
                SelectedTemperatureProfile,
                // ReSharper disable once AssignNullToNotNullAttribute
                _selectedCalcObject, EnergyIntensity.EnergyIntensityType, ReportCancel, ResumeSettlement, null, Sim.MyGeneralConfig.SelectedLoadTypePriority, tds, trs,
                Sim.MyGeneralConfig.AllEnabledOptions(), Sim.MyGeneralConfig.StartDateDateTime, Sim.MyGeneralConfig.EndDateDateTime, Sim.MyGeneralConfig.InternalStepSize,
                Sim.MyGeneralConfig.CSVCharacter, Sim.MyGeneralConfig.RandomSeed, Sim.MyGeneralConfig.ExternalStepSize, Sim.MyGeneralConfig.WriteExcelColumnBool,
                Sim.MyGeneralConfig.ShowSettlingPeriodBool, 3, Sim.MyGeneralConfig.RepetitionCount,
                calculationProfiler, SelectedChargingStationSet, null, Sim.MyGeneralConfig.DeviceProfileHeaderMode,
                false, resultpath, _calculateTransportation, Sim.MyGeneralConfig.EnableIdlemodeBool,
                Sim.MyGeneralConfig.DecimalSeperator, Sim.MyGeneralConfig.EnableFlexibilityBool);
            var cs = new CalcStarter(Sim);
            //_calculationProfiler.Clear();
#pragma warning disable S2930 // "IDisposables" should be disposed
#pragma warning disable CC0022 // Should dispose object
            var task1 = new Thread(() => cs.Start(csps));
#pragma warning restore CC0022 // Should dispose object
#pragma warning restore S2930 // "IDisposables" should be disposed
            task1.Name = "MainThread";
            task1.Start();
        }

        [JetBrains.Annotations.NotNull]
        public string WriteCalculationJsonSpecForCommandLine([JetBrains.Annotations.NotNull] string resultpath)
        {
            if (SelectedCalcObject == null) {
                const string s = "Nothing selected to calculate";
                Logger.Error(s);
                return s;
            }

            if (SelectedCalcObject.CalcObjectType != CalcObjectType.House) {
                const string s = "The json calculation only works for houses";
                Logger.Error(s);
                return s;
            }

            if (SelectedCalcObject.CalcObjectType == CalcObjectType.House && Sim.MyGeneralConfig.SelectedLoadTypePriority < LoadTypePriority.RecommendedForHouses) {
                const string s = "Load type priority not suitable for houses ";
                Logger.Error(s);
                return s;
            }
            var houseJob = new HouseCreationAndCalculationJob("scenario","year","district", HouseDefinitionType.HouseData);
            House house = (House)SelectedCalcObject;
            HouseData hd = house.MakeHouseData();
            houseJob.House = hd;
            JsonCalcSpecification jcs = new JsonCalcSpecification { DeleteAllButPDF = false, CalcOptions = new List<CalcOption>()};
            foreach (CalcOption enabledOption in Sim.MyGeneralConfig.AllEnabledOptions()) {
                jcs.CalcOptions.Add(enabledOption);
            }

            jcs.StartDate = Sim.MyGeneralConfig.StartDateDateTime;
            jcs.EndDate = Sim.MyGeneralConfig.EndDateDateTime;
            jcs.EnergyIntensityType = Sim.MyGeneralConfig.SelectedEnergyIntensity;
            jcs.LoadTypePriority = Sim.MyGeneralConfig.SelectedLoadTypePriority;
            jcs.RandomSeed = Sim.MyGeneralConfig.RandomSeed;
            jcs.TemperatureProfile = SelectedTemperatureProfile?.GetJsonReference();
            jcs.GeographicLocation = GeographicLocation?.GetJsonReference();
            jcs.SkipExisting = false;
            jcs.OutputDirectory = NameForJsonExportOutputDirectory;
            string databasepath = Sim.ConnectionString.Replace("Data Source=", "");
            houseJob.PathToDatabase = databasepath;

            houseJob.CalcSpec = jcs;
            HouseJobSerializer.WriteJsonToFile(resultpath,houseJob);
            return "Successfully written to " + resultpath;
        }

        private void AddSingleTimespan(int min, int second)
        {
            var ts = new TimeSpan(0, 0, min, second);
            if (ts <= _maximumInternalTimeResolution) {
                DefaultTimeSteps.Add(ts.ToString());
            }
        }

        private void RecalcDstPath()
        {
            string s1 =  Path.Combine(GConfig.DestinationPath, AutomationUtili.CleanFileName(_selectedCalcObject?.Name ?? ""));
            if (!CalculateTransportation) {
                DstPath = s1;
                return;
            }

            if (SelectedCalcObjectType == CalcObjectType.ModularHousehold) {
                string s = " (" + SelectedTravelRouteSet + ", " + SelectedTransportationDeviceSet + ", " +
                           SelectedChargingStationSet + ")";
                s = AutomationUtili.CleanFileName(s);
                DstPath = s1 + s;
            }
            else {
                string s = " (with transportation)";
                s = AutomationUtili.CleanFileName(s);
                DstPath = s1 + s;
            }
        }

        private void RecalcForCalcObject()
        {
            if (_selectedCalcObject != null) {
                RecalcDstPath();
                // ReSharper disable once PossibleNullReferenceException
                MaximumInternalTimeResolution = _selectedCalcObject.CalculateMaximumInternalTimeResolution();
                InternalTimeResolution = MaximumInternalTimeResolution.ToString();
                if (_selectedCalcObject.DefaultGeographicLocation != null) {
                    GeographicLocation = _selectedCalcObject.DefaultGeographicLocation;
                }

                if (_selectedCalcObject.DefaultTemperatureProfile != null) {
                    SelectedTemperatureProfile = _selectedCalcObject.DefaultTemperatureProfile;
                }

                Sim.MyGeneralConfig.SelectedEnergyIntensity = _selectedCalcObject.EnergyIntensityType;
                OnPropertyChanged(nameof(EnergyIntensity));
            }
            else {
                DstPath = GConfig.DestinationPath;
            }
        }

        private void RefreshCalcObjectList()
        {
            switch (_selectedCalcObjectType) {
                case CalcObjectType.ModularHousehold:
                    CalcObjects.SynchronizeWithList(Sim.ModularHouseholds.Items.Cast<ICalcObject>().ToList());
                    break;
                case CalcObjectType.House:
                    CalcObjects.SynchronizeWithList(Sim.Houses.Items.Cast<ICalcObject>().ToList());
                    break;
                case CalcObjectType.Settlement:
                    CalcObjects.SynchronizeWithList(Sim.Settlements.Items.Cast<ICalcObject>().ToList());
                    break;
                default:
                    throw new LPGNotImplementedException("Missing Calc Object Type");
            }

            if (CalcObjects.Count > 0) {
                _selectedCalcObject = CalcObjects[0];
            }

            RecalcForCalcObject();
            OnPropertyChanged(nameof(SelectedCalcObjectType));

            OnPropertyChanged(nameof(SelectedCalcObject));
        }

        private bool ReportCancel()
        {
            SetIsInCalc(false);

            return false;
        }

        [SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalse")]
        private bool ReportFinishHouse(bool everythingOk, [JetBrains.Annotations.NotNull] string name, [ItemNotNull] [JetBrains.Annotations.NotNull] ObservableCollection<ResultFileEntry> rfes)
        {
            if (everythingOk) {
                throw new LPGException("Show house results");
                //var rp = new SettlementResultsPresenter(_applicationPresenter,new SettlementResultView(), name, results, rfes);
                //_applicationPresenter.OpenItem(rp);
                //MessageWindows.ShowInfoMessage("All done!", "Finished");
            }

            SetIsInCalc(false);
            return everythingOk;
        }

        private bool ReportFinishHousehold(bool everythingOk, [JetBrains.Annotations.NotNull] string name, [JetBrains.Annotations.NotNull] string resultPath)
        {
            if (everythingOk) {
                var rp = new ResultPresenter(_applicationPresenter, new ResultView(), name, resultPath);
                _applicationPresenter.OpenItem(rp);
                MessageWindowHandler.Mw.ShowInfoMessage("All done!", "Finished");
            }

            SetIsInCalc(false);
            return everythingOk;
        }


        private void SetIsInCalc(bool value)
        {
            IsInCalc = value;
            OnPropertyChanged(nameof(IsInCalc));
            OnPropertyChanged(nameof(IsNotInCalc));
        }

        private void UpdateInternalTimesteps()
        {
            DefaultTimeSteps.Clear();
            AddSingleTimespan(0, 1);
            AddSingleTimespan(0, 10);
            AddSingleTimespan(1, 0);
            AddSingleTimespan(5, 0);
            AddSingleTimespan(15, 0);
        }
    }
}