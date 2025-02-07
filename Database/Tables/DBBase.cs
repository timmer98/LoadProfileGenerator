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

#region save

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Automation;
using Automation.ResultFiles;
using Common;
using Common.Enums;
using Database.Database;
using Database.Helpers;
using Database.Tables.BasicElements;
using Database.Tables.BasicHouseholds;
using Database.Tables.Houses;
using Database.Tables.ModularHouseholds;
using Database.Tables.Transportation;
using JetBrains.Annotations;

#endregion

namespace Database.Tables
{
    public static class HashSetUtility {
        [JetBrains.Annotations.NotNull]
        public static HashSet<T> ToHashSet<T>([JetBrains.Annotations.NotNull] this IEnumerable<T> list)
        {
            HashSet<T> mySet = new HashSet<T>();
            foreach (var item in list) {
                mySet.Add(item);
            }
            return mySet;
        }
    }
    public abstract class DBBase : BasicElement, INotifyPropertyChanged
    {
        public void CheckIfAllPropertiesWereCovered<T>(List<string> checkedProperties, [JetBrains.Annotations.NotNull] T obj)
        {
            var myPropertyInfos = obj.GetType().GetProperties();
            List<PropertyInfo> propertiesToSync = new List<PropertyInfo>();
            foreach (var info in myPropertyInfos)
            {
                if (checkedProperties.Contains(info.Name))
                {
                    continue;
                }

                if (Attribute.IsDefined(info, typeof(IgnoreForJsonSyncAttribute)))
                {
                    continue;
                }

                if (!info.CanWrite) {
                    continue;
                }
                propertiesToSync.Add(info);
            }

            if (propertiesToSync.Count > 0) {
                string s = "";
                foreach (var myvar in propertiesToSync) {
                    s += "ValidateAndUpdateValueAsNeeded(nameof(" + myvar.Name + "), checkedProperties, () => " + myvar.Name + " == json." +
                         myvar.Name + ", () => ";
                    if (myvar.PropertyType.IsValueType || myvar.PropertyType == typeof(string)) {
                        s += myvar.Name + " = json." +myvar.Name+");\n";
                    }
                    else
                    {
                        s += myvar.Name + " = sim.xxx.FindByJsonReference(json." + myvar.Name + "));\n";
                    }

                }
                throw new LPGException("Forgotten Sync of properties in " + obj.GetType() + "\n"+s);
            }
        }

        public void ValidateAndUpdateValueAsNeeded(string propertyName,
                                                   [JetBrains.Annotations.NotNull] List<string> checkedProperties,
                                                   [CanBeNull] object db, [CanBeNull] object json, Action update)
        {

            if (!Equals(db, json))
            {
                Logger.Info("found a difference in " + propertyName);
               update();
                NeedsUpdate = true;
            }

            if (checkedProperties.Contains(propertyName))
            {
                throw new LPGException("Tried to check property twice");
            }
            checkedProperties.Add(propertyName);
        }
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public static void SynchronizeList<T, U>([ItemNotNull] [JetBrains.Annotations.NotNull] ObservableCollection<T> dbitems,
                                                 [ItemNotNull] [JetBrains.Annotations.NotNull] List<U> jsonItems, [ItemNotNull][JetBrains.Annotations.NotNull]
                                                 out List<U> itemsToCreate) where T : DBBase, IRelevantGuidProvider where U : IGuidObject
        {
            var jsonGuids = jsonItems.Select(x => x.Guid).ToHashSet();
            List<T> dbItemsToRemove = new List<T>();
            foreach (var dbitem in dbitems)
            {
                if (!jsonGuids.Contains(dbitem.RelevantGuid))
                {
                    dbItemsToRemove.Add(dbitem);
                }
            }

            foreach (T dbBase in dbItemsToRemove)
            {
                dbBase.DeleteFromDB();
                dbitems.Remove(dbBase);
            }

            var dbGuids = dbitems.Select(x => x.Guid).ToHashSet();
            itemsToCreate = new List<U>();
            foreach (var jsonItem in jsonItems)
            {
                if (!dbGuids.Contains(jsonItem.Guid))
                {
                    Logger.Info("Need to create new object " + jsonItem.Guid + " of type " + typeof(T).Name);
                    itemsToCreate.Add(jsonItem);
                }
            }
            dbitems.Sort();
        }
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public static void SynchronizeListWithCreation<Tdb, TJson>([ItemNotNull] [JetBrains.Annotations.NotNull] ObservableCollection<Tdb> dbitems,
                                                 [ItemNotNull] [JetBrains.Annotations.NotNull] List<TJson> jsonItems,
                                                 Func<TJson, Simulator, Tdb> AddNewObjectFromJto, Simulator sim)
            where Tdb : DBBase,  IJSonSubElement<TJson>  where TJson : IGuidObject
        {
            var jsonGuids = jsonItems.Select(x => x.Guid).ToHashSet();
            List<Tdb> dbItemsToRemove = new List<Tdb>();
            foreach (var dbitem in dbitems)
            {
                if (!jsonGuids.Contains(dbitem.RelevantGuid))
                {
                    dbItemsToRemove.Add(dbitem);
                }
            }

            foreach (Tdb dbBase in dbItemsToRemove)
            {
                dbBase.DeleteFromDB();
                dbitems.Remove(dbBase);
            }

            var dbGuids = dbitems.Select(x => x.RelevantGuid).ToHashSet();
            foreach (var jsonItem in jsonItems)
            {
                if (!dbGuids.Contains(jsonItem.Guid)) {
                    var obj = AddNewObjectFromJto(jsonItem, sim);
                    Logger.Info("Created new object " + jsonItem.Guid + " of type " + typeof(Tdb).Name);
                    obj.SynchronizeDataFromJson(jsonItem,sim);
                    Logger.Info("Synchronized item " + obj.Guid + " of type " + typeof(Tdb).Name);
                }
                else {
                    var itemToSync = dbitems.Single(x => x.RelevantGuid == jsonItem.Guid);
                    itemToSync.SynchronizeDataFromJson(jsonItem,sim);
                }
            }
            dbitems.Sort();
        }



        [JetBrains.Annotations.NotNull]
        public JsonReference GetJsonReference()
        {
            return new JsonReference(Name,Guid);
        }
        public enum LoadResults
        {
            AllOk,
            TableNotFound
        }
        [SuppressMessage("ReSharper", "MemberCanBeProtected.Global")]
        public static bool IsLoading { get; set; }

        [JetBrains.Annotations.NotNull] private readonly string _tableName;
#pragma warning disable CA2235 // Mark all non-serializable fields
            [CanBeNull]
        private int? _id;
#pragma warning restore CA2235 // Mark all non-serializable fields
        private bool _needsUpdate;

        protected DBBase([JetBrains.Annotations.NotNull]string pName, [JetBrains.Annotations.NotNull] string tableName, [JetBrains.Annotations.NotNull] string connectionString,[JetBrains.Annotations.NotNull] StrGuid guid):base(pName)
        {
            _guid = guid;
            if(guid==null) {
                throw new LPGException("Guid was null");
            }

            _tableName = tableName;
            ConnectionString = connectionString;
            FunctionsToCallAfterDelete = new List<Action<DBBase>>();
        }

        protected DBBase([JetBrains.Annotations.NotNull]string pName, [CanBeNull] int? pID, [JetBrains.Annotations.NotNull] string tableName, [JetBrains.Annotations.NotNull] string connectionString,
                         [JetBrains.Annotations.NotNull] StrGuid guid):base(pName)
        {
            _guid = guid;
            if (guid == null)
            {
                throw new LPGException("Guid was null");
            }
            _id = pID;
            _tableName = tableName;
            ConnectionString = connectionString;
            FunctionsToCallAfterDelete = new List<Action<DBBase>>();
        }
        [IgnoreForJsonSync]
        public bool AreNumbersOkInNameForIntegrityCheck { get; protected set; }
        [JetBrains.Annotations.NotNull]
        [IgnoreForJsonSync]
        public string ConnectionString { get; }

        [ItemNotNull]
        [JetBrains.Annotations.NotNull]
#pragma warning disable CA2235 // Mark all non-serializable fields
        public List<Action<DBBase>> FunctionsToCallAfterDelete { get; }
#pragma warning restore CA2235 // Mark all non-serializable fields

        [JetBrains.Annotations.NotNull]
        public string HeaderString => TypeDescription + " - " + base.Name;

        [CanBeNull]
        [IgnoreForJsonSync]
        public int? ID
        {
            get => _id;
            protected set => _id = value;
        }

        private StrGuid _guid;
        public StrGuid Guid
        {
            get => _guid;
            set => SetValueWithNotify(value, ref _guid, nameof(Guid));
        }

        [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "int")]
        [SuppressMessage("Microsoft.Design", "CA1065:DoNotRaiseExceptionsInUnexpectedLocations")]
        public int IntID => IntegerID();

        public override string Name
        {
            get => base.Name;
            set
            {
                if (base.Name == value)
                {
                    return;
                }
                base.Name = value;
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(PrettyName));
                OnPropertyChanged(nameof(HeaderString));
            }
        }

        protected bool NeedsUpdate
        {
            get => _needsUpdate;
            set
            {
                if (!NeedsUpdateAllowed)
                {
                    throw new LPGException("tried to call needs update even though it's not allowed right now");
                }
                _needsUpdate = value;
            }
        }

        [UsedImplicitly]
        public static bool NeedsUpdateAllowed { get; set; } = true;

        [JetBrains.Annotations.NotNull]
        [SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalse")]
        public virtual string PrettyName {
            get {
                if (Name != null) {
                    return Name;
                }
                return "(no name)";
            }
        }

        [UsedImplicitly]
        public static bool ShowDeleteMessage { get; set; } = true;

        [UsedImplicitly]
        [JetBrains.Annotations.NotNull]
        [IgnoreForJsonSync]
        public string TypeDescription { get; set; } = "ERROR";

        #region INotifyPropertyChanged Members

        [field: NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        protected static void DeleteAllForOneParent(int parentId, [JetBrains.Annotations.NotNull] string parentFieldName, [JetBrains.Annotations.NotNull] string tableName,
            [JetBrains.Annotations.NotNull] string connectionString)
        {
            using (var con = new Connection(connectionString))
            {
                con.Open();
                using (var cmd = new Command(con))
                {
                    var cmdstring = "DELETE FROM " + tableName + " WHERE " + parentFieldName + " = @parentId";
                    cmd.AddParameter("parentId", parentId);
                    cmd.ExecuteNonQuery(cmdstring);
                }
                if (Config.ShowDeleteMessages)
                {
                    Logger.Info("Deleted all the entries for the ParentID " + parentId + " in the table " + tableName +
                                " from the data base.");
                }
            }
        }

        protected static void DeleteByID(int id, [JetBrains.Annotations.NotNull] string tableName, [JetBrains.Annotations.NotNull] string connectionString)
        {
            using (var con = new Connection(connectionString))
            {
                con.Open();
                using (var cmd = new Command(con))
                {
                    cmd.DeleteByID(tableName, id);
                }
            }
        }

        public virtual void DeleteFromDB()
        {
            if (ID != null)
            {
                using (var con = new Connection(ConnectionString))
                {
                    con.Open();
                    using (var cmd = new Command(con))
                    {
                        cmd.DeleteByID(_tableName, (int)ID);
                    }
                    if (Config.ShowDeleteMessages)
                    {
                        Logger.Info("Deleted the item of type " + GetType() + " with the name " + Name +
                                    " and the ID " + ID + " from the data base.");
                    }

                    foreach (var func in FunctionsToCallAfterDelete)
                    {
                        func(this);
                    }
                }
            }
        }

        public static bool DoesTableExist([JetBrains.Annotations.NotNull] string connectionString, [JetBrains.Annotations.NotNull] string tableName)
        {
            using (var con = new Connection(connectionString))
            {
                con.Open();
                using (var cmd = new Command(con))
                {
                    using (var dr =
                        cmd.ExecuteReader("SELECT name FROM sqlite_master WHERE type='table' AND name LIKE'" +
                                          tableName + "'"))
                    {
                        while (dr.Read())
                        {
                            var s = dr.GetString("name");
                            if (string.Equals(s, tableName, StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        public void ExecuteInsert([JetBrains.Annotations.NotNull]Connection con)
        {
            if (ID != null)
            {
                return;
            }
            using (var cmd = new Command(con))
            {
                SetSqlParameters(cmd);
                SetBaseParameters(cmd,false);
                ID = cmd.ExecuteInsert(_tableName);
            }
            NeedsUpdate = false;
        }

        [JetBrains.Annotations.NotNull]
        public static string FindNewName([JetBrains.Annotations.NotNull] Func<string, bool> isNameTaken, [JetBrains.Annotations.NotNull] string basename)
        {
            var i = 1;
            while (isNameTaken(basename + i))
            {
                i++;
            }
            return basename + i;
        }

        [CanBeNull]
        protected static IAssignableDevice GetAssignableDeviceFromListByName([JetBrains.Annotations.NotNull][ItemNotNull] ObservableCollection<RealDevice> devices,
            [JetBrains.Annotations.NotNull][ItemNotNull] ObservableCollection<DeviceCategory> deviceCategories, [JetBrains.Annotations.NotNull][ItemNotNull]  ObservableCollection<DeviceAction> deviceActions,
            [JetBrains.Annotations.NotNull][ItemNotNull] ObservableCollection<DeviceActionGroup> deviceActionGroups,[CanBeNull] IAssignableDevice oldAssignableDevice)
        {
            if (oldAssignableDevice == null) {
                return null;
            }
            IAssignableDevice iad;
            switch (oldAssignableDevice.AssignableDeviceType)
            {
                case AssignableDeviceType.Device:
                    var dev = GetItemFromListByName(devices, oldAssignableDevice.Name);
                    iad = dev;
                    break;
                case AssignableDeviceType.DeviceCategory:
                    {
                        var devcat = GetItemFromListByName(deviceCategories, oldAssignableDevice.Name);
                        iad = devcat;
                    }
                    break;
                case AssignableDeviceType.DeviceAction:
                    {
                        var devcat = GetItemFromListByName(deviceActions, oldAssignableDevice.Name);
                        iad = devcat;
                    }
                    break;
                case AssignableDeviceType.DeviceActionGroup:
                    {
                        var devcat = GetItemFromListByName(deviceActionGroups, oldAssignableDevice.Name);
                        iad = devcat;
                    }
                    break;
                default: throw new LPGException("AssignableDeviceType is missing! This is a bug.");
            }
            return iad;
        }

        [CanBeNull]
        protected static ICalcObject GetICalcObjectFromList(
            [ItemNotNull] [JetBrains.Annotations.NotNull] ObservableCollection<ModularHousehold> modularHouseholds,
            [ItemNotNull] [CanBeNull] ObservableCollection<House> houses, [ItemNotNull] [CanBeNull] ObservableCollection<Settlement> settlements,
            [JetBrains.Annotations.NotNull] ICalcObject oldCalcObject)
        {
            ICalcObject iac;
            if (oldCalcObject.GetType() == typeof(ModularHousehold))
            {
                var chh = GetItemFromListByName(modularHouseholds, oldCalcObject.Name);
                iac = chh;
            }
            else if (oldCalcObject.GetType() == typeof(House))
            {
                if (houses == null)
                {
                    throw new LPGException("Tried to get a houses from an null houses list");
                }
                var house = GetItemFromListByName(houses, oldCalcObject.Name);
                iac = house;
            }
            else if (oldCalcObject.GetType() == typeof(Settlement))
            {
                if (settlements == null)
                {
                    throw new LPGException("Tried to get a settlement from an null settlement list");
                }
                var settlement = GetItemFromListByName(settlements, oldCalcObject.Name);
                iac = settlement;
            }
            else
            {
                throw new LPGException("ICalcObject Type is missing! This is a bug.");
            }
            return iac;
        }

        /// <summary>
        ///     gets item from list by name
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="col"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        /// ///
        /// <exception cref="LPGException">tried to call needs update even though it's not allowed right now</exception>
        [CanBeNull]
        protected static T GetItemFromListByName<T>([ItemNotNull] [JetBrains.Annotations.NotNull] ObservableCollection<T> col, [CanBeNull] string name) where T : DBBase
        {
            if (name == null)
            {
                return null;
            }
            foreach (var item in col)
            {
                if (string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }
            return null;
        }

        [CanBeNull]
        protected static T GetItemFromListByJsonReference<T>([ItemNotNull] [JetBrains.Annotations.NotNull] ObservableCollection<T> col, [CanBeNull] JsonReference reference) where T : DBBase
        {
            if (reference == null)
            {
                return null;
            }
            foreach (var item in col)
            {
                if (item.Guid == reference.Guid)
                {
                    return item;
                }
            }
            return null;
        }

        public static void HideDeleteMessages()
        {
            ShowDeleteMessage = false;
        }

        private int IntegerID()
        {
            if (ID == null)
            {
                throw new LPGException("Int == null");
            }
            return (int)ID;
        }

        protected abstract bool IsItemLoadedCorrectly([JetBrains.Annotations.NotNull] out string message);

        public override bool IsValid(string filter)
        {
            if (filter == null)
            {
                throw new LPGException("isvalid failed, s = null");
            }
            if (PrettyName.ToUpperInvariant().Contains(filter.ToUpperInvariant()))
            {
                return true;
            }
            return false;
        }

        public static LoadResults LoadAllFromDatabase<T>([ItemNotNull] [JetBrains.Annotations.NotNull] ObservableCollection<T> result, [JetBrains.Annotations.NotNull] string connectionString,
            [JetBrains.Annotations.NotNull] string pTableName, [JetBrains.Annotations.NotNull] Func<DataReader, string, bool, AllItemCollections, T> assignFields,
            [JetBrains.Annotations.NotNull] AllItemCollections allItemCollections, bool ignoreMissingTables, bool sort) where T : DBBase
        {
            int prevCount = GuidCreationCount;
            if (ignoreMissingTables)
            {
                if (!DoesTableExist(connectionString, pTableName))
                {
                    return LoadResults.TableNotFound;
                }
            }
            var objList = new List<T>();
            using (var con = new Connection(connectionString))
            {
                con.Open();

                using (var cmd = new Command(con))
                {
                    using (var dr = cmd.GetTableReader(pTableName))
                    {
                        while (dr.Read())
                        {
                            var newObj = assignFields(dr, connectionString, ignoreMissingTables, allItemCollections);
                            objList.Add(newObj);
                        }
                    }
                }
            }
            if (sort)
            {
                objList.Sort();
            }
            if (!ignoreMissingTables)
            {
                var items2Delete = new List<Tuple<T, string>>();
                foreach (var item in objList)
                {
                    if (!item.IsItemLoadedCorrectly(out var s))
                    {
                        items2Delete.Add(new Tuple<T, string>(item, s));
                    }
                }
                foreach (var item in items2Delete)
                {
                    Logger.Error("Cleaning database of stale item " + item.Item1.Name + " because " + item.Item2);
                    objList.Remove(item.Item1);
                    item.Item1.DeleteFromDB();
                }
                //if any guids were created, just save all the objects
                foreach (var dbBase in objList) {
                    if (prevCount != GuidCreationCount)
                    {
                        dbBase._needsUpdate = true;
                    }
                }
            }
            foreach (var obj in objList)
            {
                result.Add(obj);
            }
            return LoadResults.AllOk;
        }

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CanBeNull] string propertyName)
        {
            if (propertyName == null)
            {
                throw new LPGException("Property Name was null");
            }
            NeedsUpdate = true;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChangedNoUpdate([JetBrains.Annotations.NotNull] string propertyName)
        {
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(propertyName));
        }

        private void SetBaseParameters([JetBrains.Annotations.NotNull] Command cmd, bool setID)
        {
            if(cmd.IsParameterSet("Guid"))
            {
                throw new LPGException("Bug: Guid was set already");
            }
            if (cmd.IsParameterSet("ID"))
            {
                throw new LPGException("Bug: Guid was set already");
            }
            if (setID)
            {
                cmd.AddParameter("ID", "@myid", IntID, true);
            }

            if (Guid == null) {
                throw new LPGException("Guid was null");
            }
            cmd.AddParameter("Guid",  Guid.StrVal);
        }

        [JetBrains.Annotations.NotNull] public static readonly HashSet<StrGuid> GuidsToSave = new HashSet<StrGuid>();
        [JetBrains.Annotations.NotNull] [ItemNotNull] public static readonly List<string> TypesThatMadeGuids = new List<string>();
        public static int GuidCreationCount { get; set; }
        [JetBrains.Annotations.NotNull]
        public static StrGuid GetGuid([JetBrains.Annotations.NotNull] DataReader dr, bool ignoreMissingFields)
        {
            var oldguid = dr.GetString("Guid", false, "", ignoreMissingFields);
            if (string.IsNullOrWhiteSpace(oldguid))
            {
                var guid = System.Guid.NewGuid().ToStrGuid();
                GuidsToSave.Add(guid);
                GuidCreationCount++;
                string callingMethod = Utili.GetCallingMethodAndClass();
                if (!TypesThatMadeGuids.Contains(callingMethod)) {
                    TypesThatMadeGuids.Add(callingMethod);
                }

                return guid;
            }
            return StrGuid.FromString(oldguid);
        }

        public virtual void SaveToDB([JetBrains.Annotations.NotNull] Connection con)
        {
            if (ID != null)
            {
                if (NeedsUpdate)
                {
                        using (var cmd = new Command(con))
                        {
                            SetSqlParameters(cmd);
                            SetBaseParameters(cmd, true);
                            cmd.ExecuteUpdate(_tableName);
                        }
                        NeedsUpdate = false;
                }
            }
            else
            {
                    ExecuteInsert(con);
            }
        }
        public virtual void SaveToDB()
        {
            if (ID != null)
            {
                if (NeedsUpdate)
                {
                    using (var con = new Connection(ConnectionString))
                    {
                        using (var cmd = new Command(con))
                        {
                            con.Open();
                            SetSqlParameters(cmd);
                            SetBaseParameters(cmd, true);
                            cmd.ExecuteUpdate(_tableName);
                        }
                        NeedsUpdate = false;
                    }
                }
            }
            else
            {
                using (var con = new Connection(ConnectionString))
                {
                    con.Open();
                    ExecuteInsert(con);
                }
            }
        }

        protected abstract void SetSqlParameters([JetBrains.Annotations.NotNull] Command cmd);

        protected static void SetSubitems([ItemNotNull] [JetBrains.Annotations.NotNull] List<DBBase> parents, [ItemNotNull] [JetBrains.Annotations.NotNull] List<DBBase> children,
            [JetBrains.Annotations.NotNull] Func<DBBase, DBBase, bool> isCorrectParent, bool doNotdeleteFromDB)
        {
            foreach (var child in children)
            {
                var found = parents.Any(parent => isCorrectParent(parent, child));
                if (!found)
                {
                    var parentdescription = string.Empty;
                    if (!doNotdeleteFromDB)
                    {
                        child.DeleteFromDB();

                        if (parents.Count > 0)
                        {
                            parentdescription = parents[0].TypeDescription;
                        }
                        if (child.TypeDescription == "ERROR")
                        {
                            throw new LPGException("Type name not set for " + child.GetType() +
                                                   ". This is a bug. Please report.");
                        }
                        if (ShowDeleteMessage)
                        {
                            Logger.Error("Deleted orphaned " + child.TypeDescription + " with ID " + child.ID +
                                         " because the parent " + parentdescription + " was deleted.");
                        }
                    }
                    else
                    {
                        Logger.Info("During import could not find a parent for the item " + child.Name);
                    }
                }
            }
        }

        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify([CanBeNull]string value, [CanBeNull] ref string dstField,
            [CanBeNull] [CallerMemberName] string propertyName = null)
        {
            if (value == dstField)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }

        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify([CanBeNull]TemperatureProfile value, [CanBeNull] ref TemperatureProfile dstField,
            [CanBeNull] [CallerMemberName] string propertyName = null)
        {
            if (value == dstField)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }

        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify(ActionAfterInterruption value, ref ActionAfterInterruption dstField,
            [CanBeNull] [CallerMemberName] string propertyName = null)
        {
            if (value == dstField)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }


        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify(FlexibilityType value, ref FlexibilityType dstField,
                                          [CanBeNull][CallerMemberName] string propertyName = null)
        {
            if (value == dstField)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }
        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify(BodilyActivityLevel value, ref BodilyActivityLevel dstField,
                                          [CanBeNull] [CallerMemberName] string propertyName = null)
        {
            if (value == dstField)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }

        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify<T>([CanBeNull] T value, [CanBeNull] ref T dstField,
            bool acceptNull = false,
            [CanBeNull] [CallerMemberName] string propertyName = null) where T : class
        {
            if (value == null && !acceptNull)
            {
                return;
            }
            if (value?.Equals(dstField) == true)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }

        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify(CreationType value, ref CreationType dstField,
            [CanBeNull] [CallerMemberName] string propertyName = null)
        {
            if (value == dstField)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }

        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify(StrGuid value, ref StrGuid dstField,
                                          [CanBeNull] [CallerMemberName] string propertyName = null)
        {
            if (value == dstField)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }

        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify(VacationType value, ref VacationType dstField,
            [CanBeNull] [CallerMemberName] string propertyName = null)
        {
            if (value == dstField)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }

        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify(TemplateVacationType value, ref TemplateVacationType dstField,
            [CanBeNull] [CallerMemberName] string propertyName = null)
        {
            if (value == dstField)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }

        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify(EstimateType value, ref EstimateType dstField,
            [CanBeNull] [CallerMemberName] string propertyName = null)
        {
            if (value == dstField)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }

        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify([CanBeNull] DeviceCategory value, [CanBeNull]ref DeviceCategory dstField,
            [CanBeNull] [CallerMemberName] string propertyName = null)
        {
            if (value == dstField)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }

        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify(TimeProfileType value, ref TimeProfileType dstField,
            [CanBeNull] [CallerMemberName] string propertyName = null)
        {
            if (value == dstField)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }

        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify(TraitPriority value, ref TraitPriority dstField,
            [CanBeNull] [CallerMemberName] string propertyName = null)
        {
            if (value == dstField)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }

        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify(LoadTypePriority value, ref LoadTypePriority dstField,
            [CanBeNull] [CallerMemberName] string propertyName = null)
        {
            if (value == dstField)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }

        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify(PermittedGender value, ref PermittedGender dstField,
            [CanBeNull] [CallerMemberName] string propertyName = null)
        {
            if (value == dstField)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }
        /*
        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify(VariableCondition value, ref VariableCondition dstField,
            [CallerMemberName] string propertyName = null)
        {
            if (value == dstField) {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }*/

        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify(EnergyIntensityType value, ref EnergyIntensityType dstField,
            [CanBeNull] [CallerMemberName] string propertyName = null)
        {
            if (value == dstField)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }

        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify(TraitLimitType value, ref TraitLimitType dstField,
            [CanBeNull] [CallerMemberName] string propertyName = null)
        {
            if (value == dstField)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }

        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify(DayOfWeek value, ref DayOfWeek dstField,
            [CanBeNull] [CallerMemberName] string propertyName = null)
        {
            if (value == dstField)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }

        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify(TimeType value, ref TimeType dstField,
            [CanBeNull] [CallerMemberName] string propertyName = null)
        {
            if (value == dstField)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }

        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify(PermissionMode value, ref PermissionMode dstField,
            [CanBeNull] [CallerMemberName] string propertyName = null)
        {
            if (value == dstField)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }

        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify(SpeedUnit value, ref SpeedUnit dstField,
            [CanBeNull] [CallerMemberName] string propertyName = null)
        {
            if (value == dstField)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }

        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify(AnyAllTimeLimitCondition value, ref AnyAllTimeLimitCondition dstField,
            [CanBeNull] [CallerMemberName] string propertyName = null)
        {
            if (value == dstField)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }

        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify(int value, ref int dstField, [CanBeNull] [CallerMemberName] string propertyName = null)
        {
            if (value == dstField)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }

        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify([CanBeNull] int? value,[CanBeNull] ref int? dstField,
            [CanBeNull] [CallerMemberName] string propertyName = null)
        {
            if (value == dstField)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }

        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify(bool value, ref bool dstField,
            [CanBeNull] [CallerMemberName] string propertyName = null)
        {
            if (value == dstField)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }

        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify(decimal value, ref decimal dstField,
            [CanBeNull] [CallerMemberName] string propertyName = null)
        {
            if (value == dstField)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }

        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify(TimeSpan value, ref TimeSpan dstField,
            [CanBeNull] [CallerMemberName] string propertyName = null)
        {
            if (value == dstField)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }

        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify(DateTime value, ref DateTime dstField,
            [CanBeNull] [CallerMemberName] string propertyName = null)
        {
            if (value == dstField)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }

        [NotifyPropertyChangedInvocator]
        protected void SetValueWithNotify(double value, ref double dstField,
            [CanBeNull] [CallerMemberName] string propertyName = null)
        {
            if (Math.Abs(value - dstField) < Constants.Ebsilon)
            {
                return;
            }
            dstField = value;
            OnPropertyChanged(propertyName);
        }

        [SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalse")]
        public override string ToString()
        {
            if (Name != null) {
                return Name;
            }
            return "null";
        }
    }
}