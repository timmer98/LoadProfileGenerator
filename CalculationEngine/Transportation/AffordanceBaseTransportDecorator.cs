﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Automation;
using Automation.ResultFiles;
using CalculationEngine.Helper;
using CalculationEngine.HouseholdElements;
using CalculationEngine.OnlineLogging;
using Common;
using Common.CalcDto;
using Common.Enums;
using Common.JSON;
using Common.SQLResultLogging.Loggers;
using JetBrains.Annotations;

namespace CalculationEngine.Transportation {
    public class AffordanceBaseTransportDecorator : CalcBase, ICalcAffordanceBase {
        [NotNull]
        private readonly ICalcAffordanceBase _sourceAffordance;
        [NotNull]
        private readonly TransportationHandler _transportationHandler;
        [NotNull]
        private readonly ILogFile _lf;
        [NotNull]
        private readonly HouseholdKey _householdkey;
        [NotNull]
        private readonly CalcParameters _calcParameters;

        //TODO: fix the requirealldesires flag in the constructor
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields")]
        //[CanBeNull]
        //private CalcAffordanceBase _MainAffordance;
        public AffordanceBaseTransportDecorator([NotNull] ICalcAffordanceBase sourceAffordance,
            [NotNull] CalcSite site, [NotNull] TransportationHandler transportationHandler,
            [NotNull] string name,  [NotNull] ILogFile lf, [NotNull] HouseholdKey householdkey, [NotNull] CalcParameters calcParameters,
                                                [NotNull] string guid)
            : base(name, guid)
        {
            if (!site.Locations.Contains(sourceAffordance.ParentLocation)) {
                throw new LPGException("Wrong site. Bug. Please report.");
            }
            _householdkey= householdkey;
            _calcParameters = calcParameters;
            lf.OnlineLoggingData.AddTransportationStatus( new TransportationStatus(new TimeStep(0,0,false), householdkey, "Initializing affordance base transport decorator for " + name));
            _lf = lf;
            Site = site;
            _transportationHandler = transportationHandler;
            _sourceAffordance = sourceAffordance;
        }

        [NotNull]
        public string PrettyNameForDumping => Name + " (including transportation)";

        [NotNull]
        public CalcSite Site { get; }

        //BitArray ICalcAffordanceBase.IsBusyArray { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public void Activate([NotNull] TimeStep startTime, [NotNull] string activatorName, [NotNull] CalcLocation personSourceLocation,
            [NotNull] out ICalcProfile personTimeProfile)
        {
            if (_myLastTimeEntry.TimeOfLastEvalulation != startTime) {
                throw new LPGException("trying to activate without first checking if the affordance is busy is a bug. Please report.");
            }

            CalcTravelRoute route = _myLastTimeEntry.PreviouslySelectedRoutes[personSourceLocation];
            int routeduration = route.Activate(startTime,activatorName, out var usedDevices);

            if (routeduration == 0) {
                _lf.OnlineLoggingData.AddTransportationStatus(
                    new TransportationStatus(startTime, _householdkey,
                    "\tActivating " + Name + " at " + startTime + " with no transportation and moving from " + personSourceLocation + " to " + _sourceAffordance.ParentLocation.Name + " for affordance " + _sourceAffordance.Name));
            }
            else {
                _lf.OnlineLoggingData.AddTransportationStatus(new TransportationStatus(
                    startTime,
                    _householdkey,
                    "\tActivating " + Name + " at " + startTime + " with a transportation duration of " + routeduration + " for moving from " + personSourceLocation + " to " + _sourceAffordance.ParentLocation.Name));
            }

            TimeStep affordanceStartTime = startTime.AddSteps(routeduration);
            if (affordanceStartTime.InternalStep < _calcParameters.InternalTimesteps) {
                _sourceAffordance.Activate(affordanceStartTime, activatorName,  personSourceLocation,
                     out var sourcePersonProfile);
            CalcProfile newPersonProfile = new CalcProfile(
                    "Travel Profile for Route " + route.Name + " to affordance " + _sourceAffordance.Name,
                    System.Guid.NewGuid().ToString(),
                    CalcProfile.MakeListwithValue1AndCustomDuration(routeduration),  ProfileType.Absolute,
                    sourcePersonProfile.DataSource);
                newPersonProfile.AppendProfile(sourcePersonProfile);
                personTimeProfile = newPersonProfile;
                string usedDeviceNames = String.Join(", ", usedDevices.Select(x => x.Device.Name + "(" + x.DurationInSteps + ")"));
                _lf.OnlineLoggingData.AddTransportationEvent(_householdkey, activatorName, startTime, personSourceLocation.CalcSite?.Name??"",
                    Site.Name,
                    route.Name, usedDeviceNames, routeduration, sourcePersonProfile.StepValues.Count,
                    _sourceAffordance.Name, usedDevices);
            }
            else {
                //this is if the simulation ends during a transport
                CalcProfile newPersonProfile = new CalcProfile(
                    "Travel Profile for Route " + route.Name + " to affordance " + _sourceAffordance.Name,
                    System.Guid.NewGuid().ToString(),
                    CalcProfile.MakeListwithValue1AndCustomDuration(routeduration),  ProfileType.Absolute,
                    _sourceAffordance.Name);
                personTimeProfile = newPersonProfile;
                string usedDeviceNames = String.Join(", ", usedDevices.Select(x => x.Device.Name + "(" + x.DurationInSteps + ")"));
                _lf.OnlineLoggingData.AddTransportationEvent(_householdkey, activatorName, startTime,
                    personSourceLocation.CalcSite?.Name ?? "",
                    Site.Name,
                    route.Name, usedDeviceNames, routeduration, newPersonProfile.StepValues.Count,
                    _sourceAffordance.Name,usedDevices);
            }
        }

        [NotNull]
        public string AffCategory => _sourceAffordance.AffCategory;

        public ColorRGB AffordanceColor => _sourceAffordance.AffordanceColor;

        public ActionAfterInterruption AfterInterruption => _sourceAffordance.AfterInterruption;

        public int CalcAffordanceSerial => _sourceAffordance.CalcAffordanceSerial;

        public CalcAffordanceType CalcAffordanceType => _sourceAffordance.CalcAffordanceType;

        //public ICalcProfile CollectPersonProfile() => _sourceAffordance.CollectPersonProfile();

        [NotNull]
        [ItemNotNull]
        public List<CalcSubAffordance> CollectSubAffordances(TimeStep time, [NotNull] NormalRandom nr, bool onlyInterrupting, [NotNull] Random r,
            [NotNull] CalcLocation srcLocation) =>
            _sourceAffordance.CollectSubAffordances(time, nr, onlyInterrupting, r, srcLocation);

        [CanBeNull]
        [ItemNotNull]
        public List<CalcAffordance.DeviceEnergyProfileTuple> Energyprofiles => _sourceAffordance.Energyprofiles;

        private class LastTimeEntry {
            public LastTimeEntry([NotNull] string personName, [NotNull] TimeStep timeOfLastEvalulation)
            {
                PersonName = personName;
                TimeOfLastEvalulation = timeOfLastEvalulation;
            }

            [NotNull]
            public string PersonName { get; }
            [NotNull]
            public TimeStep TimeOfLastEvalulation { get; }
            [NotNull]
            public Dictionary<CalcLocation, CalcTravelRoute>
                PreviouslySelectedRoutes { get; } = new Dictionary<CalcLocation, CalcTravelRoute>();
        }

        [NotNull]
        private LastTimeEntry _myLastTimeEntry = new LastTimeEntry("",new TimeStep(-1,0,false));

        public int DefaultPersonProfileLength => _sourceAffordance.DefaultPersonProfileLength;

        public bool IsBusy(TimeStep time, [NotNull] NormalRandom nr, [NotNull] Random r,
                           [NotNull] CalcLocation srcLocation, [NotNull] string calcPersonName,
            bool clearDictionaries = true)
        {
            if (_myLastTimeEntry.TimeOfLastEvalulation != time || _myLastTimeEntry.PersonName != calcPersonName) {
                _myLastTimeEntry = new LastTimeEntry(calcPersonName,time);
            }

            CalcTravelRoute route;
            if (_myLastTimeEntry.PreviouslySelectedRoutes.ContainsKey(srcLocation)) {
                route = _myLastTimeEntry.PreviouslySelectedRoutes[srcLocation];
            }
            else {
                route = _transportationHandler.GetTravelRouteFromSrcLoc(srcLocation, Site,time,calcPersonName);
                _myLastTimeEntry.PreviouslySelectedRoutes.Add(srcLocation, route);
            }

            if (route == null) {
                return true;
            }

            // ReSharper disable once PossibleInvalidOperationException
            int? travelDurationN =(int) route.GetDuration(time, calcPersonName,  r,_transportationHandler.AllMoveableDevices);
            if (travelDurationN == null) {
                throw new LPGException("Bug: couldn't calculate travel duration for route.");
            }

            TimeStep dstStartTime = time.AddSteps(travelDurationN.Value);
            if (dstStartTime.InternalStep > _calcParameters.InternalTimesteps) {
                //if the end of the activity is after the simulation, everything is ok.
                return false;
            }
            bool result = _sourceAffordance.IsBusy(dstStartTime, nr, r, srcLocation, calcPersonName,
                clearDictionaries);
            _lf.OnlineLoggingData.AddTransportationStatus(new TransportationStatus(
                time,
                _householdkey, "\t\t" + time  + " @" + srcLocation + " by " + calcPersonName
                                             + "Checking " + Name + " for busyness: " + result + " @time " + dstStartTime
                                             + " with the route " + route.Name + " and a travel duration of " + travelDurationN));
            return result;
        }

        //public BitArray IsBusyArray => _sourceAffordance.IsBusyArray;

        public bool IsInterruptable => _sourceAffordance.IsInterruptable;

        public bool IsInterrupting => _sourceAffordance.IsInterrupting;

        public int MaximumAge => _sourceAffordance.MaximumAge;

        public int MiniumAge => _sourceAffordance.MiniumAge;

        public bool NeedsLight => _sourceAffordance.NeedsLight;

        [NotNull]
        public CalcLocation ParentLocation => _sourceAffordance.ParentLocation;

        public PermittedGender PermittedGender => _sourceAffordance.PermittedGender;

        public bool RandomEffect => _sourceAffordance.RandomEffect;

        public bool RequireAllAffordances => _sourceAffordance.RequireAllAffordances;

        [NotNull]
        [ItemNotNull]
        public List<CalcDesire> Satisfactionvalues => _sourceAffordance.Satisfactionvalues;

        [NotNull]
        public string SourceTrait => _sourceAffordance.SourceTrait;

        [NotNull]
        [ItemNotNull]
        public List<CalcSubAffordance> SubAffordances => _sourceAffordance.SubAffordances;

        [CanBeNull]
        public string TimeLimitName => _sourceAffordance.TimeLimitName;
        public bool AreThereDuplicateEnergyProfiles()
        {
            return _sourceAffordance.AreThereDuplicateEnergyProfiles();
        }

        [CanBeNull]
        public string AreDeviceProfilesEmpty()
        {
            return _sourceAffordance.AreDeviceProfilesEmpty();
        }

        public int Weight => _sourceAffordance.Weight;
    }
}