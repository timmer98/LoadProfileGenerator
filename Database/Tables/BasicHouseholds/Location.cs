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

#region

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Automation.ResultFiles;
using Common;
using Database.Database;
using Database.Helpers;
using Database.Tables.BasicElements;
using JetBrains.Annotations;

#endregion

namespace Database.Tables.BasicHouseholds {
    public class Location : DBBaseElement {
        public const string TableName = "tblLocations";
        [ItemNotNull] [NotNull] private readonly ObservableCollection<LocationDevice> _locDevs = new ObservableCollection<LocationDevice>();

        public Location([NotNull] string name, [CanBeNull]int? pID, [NotNull] string connectionString,
                        [NotNull] string guid) : base(name, TableName, connectionString, guid)
        {
            ID = pID;
            AreNumbersOkInNameForIntegrityCheck = true;
            TypeDescription = "Location";
        }

        [ItemNotNull]
        [NotNull]
        public ObservableCollection<LocationDevice> LocationDevices => _locDevs;

        public void AddDevice([NotNull] IAssignableDevice device, bool save = true)
        {
            if (device.ConnectionString != ConnectionString) {
                throw new LPGException("A device from another DB was just added!");
            }
            var locdev = new LocationDevice(null, device, IntID, ConnectionString, device.Name, System.Guid.NewGuid().ToString());
            _locDevs.Add(locdev);
            if (save) {
                locdev.SaveToDB();
            }
            _locDevs.Sort();
        }

        [NotNull]
        private static Location AssignFields([NotNull] DataReader dr, [NotNull] string connectionString, bool ignoreMissingFields,
            [NotNull] AllItemCollections aic)
        {
            var name = dr.GetString("Name","(no name)");
            var id = dr.GetIntFromLong("ID");
            var guid = GetGuid(dr, ignoreMissingFields);
            return new Location(name, id, connectionString, guid);
        }

        [ItemNotNull]
        [NotNull]
        public IEnumerable<DeviceActionGroup> CollectDeviceActionGroups()
        {
            var dcs = new List<DeviceActionGroup>();
            foreach (var locdev in _locDevs) {
                if (locdev.Device?.AssignableDeviceType == AssignableDeviceType.DeviceActionGroup) {
                    dcs.Add((DeviceActionGroup) locdev.Device);
                }
            }
            return dcs;
        }

        [ItemNotNull]
        [NotNull]
        public IEnumerable<DeviceCategory> CollectDeviceCategories()
        {
            var dcs = new List<DeviceCategory>();
            foreach (var locdev in _locDevs) {
                dcs.Add(locdev.DeviceCategory);
            }
            return dcs;
        }

        [NotNull]
        [UsedImplicitly]
        public static DBBase CreateNewItem([NotNull] Func<string, bool> isNameTaken, [NotNull] string connectionString) => new Location(
            FindNewName(isNameTaken, "New Location "), null, connectionString, System.Guid.NewGuid().ToString());

        public void DeleteDevice([NotNull] LocationDevice ld)
        {
            ld.DeleteFromDB();
            _locDevs.Remove(ld);
        }

        [NotNull]
        public override DBBase ImportFromGenericItem([NotNull] DBBase toImport, [NotNull] Simulator dstSim)
            => ImportFromItem((Location)toImport,dstSim);

        [ItemNotNull]
        [NotNull]
        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        public override List<UsedIn> CalculateUsedIns([NotNull] Simulator sim)
        {
            var used = new List<UsedIn>();
            foreach (var housetype in sim.HouseTypes.It) {
                foreach (var houseTypeDevice in housetype.HouseDevices) {
                    if (houseTypeDevice.Location == this) {
                        used.Add(new UsedIn(housetype, "House Type - Autonomous - " + houseTypeDevice.Name));
                    }
                }
            }

            foreach (var householdTrait in sim.HouseholdTraits.It) {
                foreach (var hhtLocation in householdTrait.Locations) {
                    if (hhtLocation.Location == this) {
                        var affs = string.Empty;
                        var builder = new StringBuilder();
                        builder.Append(affs);
                        foreach (var affordanceLocation in hhtLocation.AffordanceLocations) {
                            builder.Append(affordanceLocation.Affordance?.PrettyName).Append(Environment.NewLine);
                        }
                        affs = builder.ToString().Trim();
                        used.Add(new UsedIn(householdTrait, "Household Trait", affs));
                    }
                }
                foreach (var autodev in householdTrait.Autodevs) {
                    if (autodev.Location == this) {
                        used.Add(new UsedIn(householdTrait, "Household Trait - Autonomous - " + autodev.Name));
                    }
                }
            }
            return used;
        }

        [NotNull]
        [UsedImplicitly]
        public static Location ImportFromItem([NotNull] Location toImport,[NotNull] Simulator dstSim)
        {
            var loc = new Location(toImport.Name, null, dstSim.ConnectionString, toImport.Guid);
            loc.SaveToDB();
            foreach (var locationDevice in toImport.LocationDevices) {
                var iad = GetAssignableDeviceFromListByName(dstSim.RealDevices.MyItems,
                    dstSim.DeviceCategories.MyItems, dstSim.DeviceActions.MyItems, dstSim.DeviceActionGroups.MyItems,
                    locationDevice.Device);
                if (iad == null) {
                    Logger.Error("Could not find the device when importing. Skipping.");
                    continue;
                }
                loc.AddDevice(iad);
            }
            return loc;
        }

        private static bool IsCorrectLocationDeviceParent([NotNull] DBBase parent, [NotNull] DBBase child)
        {
            var hd = (LocationDevice) child;
            if (parent.ID == hd.LocationID) {
                var loc = (Location) parent;
                loc.LocationDevices.Add(hd);
                return true;
            }
            return false;
        }

        protected override bool IsItemLoadedCorrectly([NotNull] out string message)
        {
            message = "";
            return true;
        }

        public static void LoadFromDatabase([ItemNotNull] [NotNull] ObservableCollection<Location> result, [NotNull] string connectionString,
            [ItemNotNull] [NotNull] ObservableCollection<RealDevice> devices, [ItemNotNull] [NotNull] ObservableCollection<DeviceCategory> deviceCategories,
            [ItemNotNull] [NotNull] ObservableCollection<VLoadType> loadTypes, bool ignoreMissingTables)
        {
            var aic = new AllItemCollections();
            LoadAllFromDatabase(result, connectionString, TableName, AssignFields, aic, ignoreMissingTables, true);
            var ld = new ObservableCollection<LocationDevice>();
            LocationDevice.LoadFromDatabase(ld, connectionString, devices, deviceCategories, loadTypes,
                ignoreMissingTables);
            SetSubitems(new List<DBBase>(result), new List<DBBase>(ld), IsCorrectLocationDeviceParent,
                ignoreMissingTables);
        }

        public override void SaveToDB()
        {
            base.SaveToDB();
            foreach (var locationDevice in _locDevs) {
                locationDevice.SaveToDB();
            }
        }

        protected override void SetSqlParameters([NotNull] Command cmd)
        {
            cmd.AddParameter("Name", "@myname", Name);
        }
    }
}