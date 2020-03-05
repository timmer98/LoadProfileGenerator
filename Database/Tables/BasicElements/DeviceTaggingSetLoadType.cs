﻿using System.Collections.ObjectModel;
using System.Linq;
using Database.Database;
using Database.Tables.BasicHouseholds;
using JetBrains.Annotations;

namespace Database.Tables.BasicElements {
    public class DeviceTaggingSetLoadType : DBBase {
        public const string TableName = "tblDeviceTaggingSetLoadType";
        private readonly int _taggingSetID;

        [CanBeNull] private VLoadType _loadType;

        public DeviceTaggingSetLoadType([NotNull] string name, int taggingSetID, [CanBeNull] VLoadType loadType,
            [NotNull] string connectionString, [CanBeNull]int? pID, [NotNull] string guid) : base(name, pID, TableName,
            connectionString, guid)
        {
            _taggingSetID = taggingSetID;
            _loadType = loadType;
            TypeDescription = "Affordance Tagging Set Loadtype";
        }

        [CanBeNull]
        [UsedImplicitly]
        public VLoadType LoadType {
            get => _loadType;
            set => SetValueWithNotify(value, ref _loadType, false, nameof(LoadType));
        }

        public int TaggingSetID => _taggingSetID;

        public static void LoadFromDatabase([ItemNotNull] [NotNull] ObservableCollection<DeviceTaggingSetLoadType> result,
            [NotNull] string connectionString, bool ignoreMissingTables, [ItemNotNull] [NotNull] ObservableCollection<VLoadType> loadTypes)
        {
            var aic = new AllItemCollections(loadTypes: loadTypes);
            LoadAllFromDatabase(result, connectionString, TableName, AssignFields, aic, ignoreMissingTables, false);
        }

        public override string ToString() => Name;

        protected override bool IsItemLoadedCorrectly([NotNull] out string message)
        {
            if (_loadType == null) {
                message = "Loadtype was not found when loading " + TypeDescription;
                return false;
            }

            message = "";
            return true;
        }

        protected override void SetSqlParameters([NotNull] Command cmd)
        {
            cmd.AddParameter("DeviceTaggingSetID", _taggingSetID);
            if (_loadType != null) {
                cmd.AddParameter("LoadTypeID", _loadType.IntID);
            }
        }

        [NotNull]
        private static DeviceTaggingSetLoadType AssignFields([NotNull] DataReader dr, [NotNull] string connectionString,
            bool ignoreMissingFields, [NotNull] AllItemCollections aic)
        {
            var id = dr.GetIntFromLong("ID");
            var taggingSetID = dr.GetIntFromLong("DeviceTaggingSetID");
            var loadtypeID = dr.GetIntFromLong("LoadTypeID");
            var loadType = aic.LoadTypes.FirstOrDefault(lt => lt.ID == loadtypeID);
            var name = "(no name)";
            if (loadType != null) {
                name = loadType.Name;
            }
            var guid = GetGuid(dr, ignoreMissingFields);
            return new DeviceTaggingSetLoadType(name, taggingSetID,
                loadType, connectionString, id, guid);
        }
    }
}