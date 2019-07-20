// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using BaseEntity.Configuration;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public class DatabaseMetadataPlugin : IPlugin
  {
    private readonly IPropertyMetaCreatorFactory _propertyMetaCreatorFactory;
    private readonly IDataExporterRegistry _dataExporterRegistry;
    private readonly IDataImporterRegistry _dataImporterRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseMetadataPlugin" /> class.
    /// </summary>
    /// <param name="propertyMetaCreatorFactory">The property meta creator factory.</param>
    /// <param name="dataExporterRegistry">The data exporter registry.</param>
    /// <param name="dataImporterRegistry">The data importer registry.</param>
    public DatabaseMetadataPlugin(IPropertyMetaCreatorFactory propertyMetaCreatorFactory,
                                  IDataExporterRegistry dataExporterRegistry,
                                  IDataImporterRegistry dataImporterRegistry)
    {
      _propertyMetaCreatorFactory = propertyMetaCreatorFactory;
      _dataExporterRegistry = dataExporterRegistry;
      _dataImporterRegistry = dataImporterRegistry;
    }

    /// <summary>
    /// CheckLicense
    /// </summary>
    public void CheckLicense()
    {
    }

    /// <summary>
    /// Called at the completion of Risk initialization
    /// </summary>
    public void Init()
    {
      RegisterDataImporters();
      RegisterDataExporters();
      RegisterPropertyMetaCreators();
      RegisterAuditHistoryWriterTypes();
    }

    private void RegisterDataImporters()
    {
      // One per property meta
      _dataImporterRegistry.RegisterPropertyImporter(typeof(ObjectIdPropertyMeta), null);
      _dataImporterRegistry.RegisterPropertyImporter(typeof(VersionPropertyMeta), null);
      _dataImporterRegistry.RegisterPropertyImporter(typeof(BooleanPropertyMeta), PropertyImporter.ImportBoolean);
      _dataImporterRegistry.RegisterPropertyImporter(typeof(ComponentPropertyMeta), PropertyImporter.ImportComponent);
      _dataImporterRegistry.RegisterPropertyImporter(typeof(ComponentCollectionPropertyMeta), PropertyImporter.ImportComponentCollection);
      _dataImporterRegistry.RegisterPropertyImporter(typeof(ElementCollectionPropertyMeta), PropertyImporter.ImportElementCollection);
      _dataImporterRegistry.RegisterPropertyImporter(typeof(GuidPropertyMeta), PropertyImporter.ImportGuid);
      _dataImporterRegistry.RegisterPropertyImporter(typeof(DateTimePropertyMeta), PropertyImporter.ImportDateTime);
      _dataImporterRegistry.RegisterPropertyImporter(typeof(EnumPropertyMeta), PropertyImporter.ImportEnum);
      _dataImporterRegistry.RegisterPropertyImporter(typeof(OneToOnePropertyMeta), PropertyImporter.ImportOneToOne);
      _dataImporterRegistry.RegisterPropertyImporter(typeof(ManyToOnePropertyMeta), PropertyImporter.ImportManyToOne);
      _dataImporterRegistry.RegisterPropertyImporter(typeof(OneToManyPropertyMeta), PropertyImporter.ImportOneToMany);
      _dataImporterRegistry.RegisterPropertyImporter(typeof(ManyToManyPropertyMeta), PropertyImporter.ImportManyToMany);
      _dataImporterRegistry.RegisterPropertyImporter(typeof(NumericPropertyMeta), PropertyImporter.ImportNumeric);
      _dataImporterRegistry.RegisterPropertyImporter(typeof(StringPropertyMeta), PropertyImporter.ImportString);
      _dataImporterRegistry.RegisterPropertyImporter(typeof(BinaryBlobPropertyMeta), PropertyImporter.ImportBinary);
      _dataImporterRegistry.RegisterPropertyImporter(typeof(ArrayOfDoublesPropertyMeta), PropertyImporter.ImportArrayOfDoubles);

      // One per value type (property, key, or element value)
      _dataImporterRegistry.RegisterValueImporter(typeof(Boolean), PropertyImporter.ImportBoolean);
      _dataImporterRegistry.RegisterValueImporter(typeof(DateTime), PropertyImporter.ImportDateTime);
      _dataImporterRegistry.RegisterValueImporter(typeof(Enum), PropertyImporter.ImportEnum);
      _dataImporterRegistry.RegisterValueImporter(typeof(Double), PropertyImporter.ImportDouble);
      _dataImporterRegistry.RegisterValueImporter(typeof(Guid), PropertyImporter.ImportGuid);
      _dataImporterRegistry.RegisterValueImporter(typeof(Int32), PropertyImporter.ImportInt32);
      _dataImporterRegistry.RegisterValueImporter(typeof(Int64), PropertyImporter.ImportInt64);
      _dataImporterRegistry.RegisterValueImporter(typeof(double?), PropertyImporter.ImportNullableDouble);
      _dataImporterRegistry.RegisterValueImporter(typeof(int?), PropertyImporter.ImportNullableInt32);
      _dataImporterRegistry.RegisterValueImporter(typeof(long?), PropertyImporter.ImportNullableInt64);
      _dataImporterRegistry.RegisterValueImporter(typeof(DateTime?), PropertyImporter.ImportNullableDateTime);
      _dataImporterRegistry.RegisterValueImporter(typeof(String), PropertyImporter.ImportString);
      _dataImporterRegistry.RegisterValueImporter(typeof(double[]), PropertyImporter.ImportArrayOfDoubles);
    }

    private void RegisterDataExporters()
    {
      // One per property meta
      _dataExporterRegistry.RegisterPropertyExporter(typeof(ObjectIdPropertyMeta), PropertyExporter.ExportStringPropertyMeta);
      _dataExporterRegistry.RegisterPropertyExporter(typeof(VersionPropertyMeta), PropertyExporter.ExportStringPropertyMeta);
      _dataExporterRegistry.RegisterPropertyExporter(typeof(BooleanPropertyMeta), PropertyExporter.ExportBooleanPropertyMeta);
      _dataExporterRegistry.RegisterPropertyExporter(typeof(ComponentPropertyMeta), PropertyExporter.ExportComponentPropertyMeta);
      _dataExporterRegistry.RegisterPropertyExporter(typeof(ComponentCollectionPropertyMeta), PropertyExporter.ExportComponentCollectionPropertyMeta);
      _dataExporterRegistry.RegisterPropertyExporter(typeof(ElementCollectionPropertyMeta), PropertyExporter.ExportElementCollectionPropertyMeta);
      _dataExporterRegistry.RegisterPropertyExporter(typeof(DateTimePropertyMeta), PropertyExporter.ExportDateTimePropertyMeta);
      _dataExporterRegistry.RegisterPropertyExporter(typeof(GuidPropertyMeta), PropertyExporter.ExportGuidPropertyMeta);
      _dataExporterRegistry.RegisterPropertyExporter(typeof(EnumPropertyMeta), PropertyExporter.ExportEnumPropertyMeta);
      _dataExporterRegistry.RegisterPropertyExporter(typeof(OneToOnePropertyMeta), PropertyExporter.ExportOneToOnePropertyMeta);
      _dataExporterRegistry.RegisterPropertyExporter(typeof(ManyToOnePropertyMeta), PropertyExporter.ExportManyToOnePropertyMeta);
      _dataExporterRegistry.RegisterPropertyExporter(typeof(OneToManyPropertyMeta), PropertyExporter.ExportOneToManyPropertyMeta);
      _dataExporterRegistry.RegisterPropertyExporter(typeof(ManyToManyPropertyMeta), PropertyExporter.ExportManyToManyPropertyMeta);
      _dataExporterRegistry.RegisterPropertyExporter(typeof(NumericPropertyMeta), PropertyExporter.ExportNumericPropertyMeta);
      _dataExporterRegistry.RegisterPropertyExporter(typeof(StringPropertyMeta), PropertyExporter.ExportStringPropertyMeta);
      _dataExporterRegistry.RegisterPropertyExporter(typeof(BinaryBlobPropertyMeta), PropertyExporter.ExportBinaryBlobPropertyMeta);
      _dataExporterRegistry.RegisterPropertyExporter(typeof(ArrayOfDoublesPropertyMeta), PropertyExporter.ExportArrayOfDoublesPropertyMeta);

      // One per value type (property, key, or element value)
      _dataExporterRegistry.RegisterValueExporter(typeof(Boolean), PropertyExporter.ExportBoolean);
      _dataExporterRegistry.RegisterValueExporter(typeof(DateTime), PropertyExporter.ExportDateTime);
      _dataExporterRegistry.RegisterValueExporter(typeof(Enum), PropertyExporter.ExportEnum);
      _dataExporterRegistry.RegisterValueExporter(typeof(Guid), PropertyExporter.ExportGuid);
      _dataExporterRegistry.RegisterValueExporter(typeof(Int32), PropertyExporter.ExportString);
      _dataExporterRegistry.RegisterValueExporter(typeof(Int64), PropertyExporter.ExportString);
      _dataExporterRegistry.RegisterValueExporter(typeof(String), PropertyExporter.ExportString);
      _dataExporterRegistry.RegisterValueExporter(typeof(Byte[]), PropertyExporter.ExportBinary);
      _dataExporterRegistry.RegisterValueExporter(typeof(Double), PropertyExporter.ExportDouble);
      _dataExporterRegistry.RegisterValueExporter(typeof(double[]), PropertyExporter.ExportArrayOfDoubles);
    }

    private void RegisterPropertyMetaCreators()
    {
      _propertyMetaCreatorFactory.RegisterPropertyMetaCreator(typeof(BinaryBlobPropertyAttribute), new BinaryBlobPropertyMetaCreator());
      _propertyMetaCreatorFactory.RegisterPropertyMetaCreator(typeof(BooleanPropertyAttribute), new BooleanPropertyMetaCreator());
      _propertyMetaCreatorFactory.RegisterPropertyMetaCreator(typeof(ComponentPropertyAttribute), new ComponentPropertyMetaCreator());
      _propertyMetaCreatorFactory.RegisterPropertyMetaCreator(typeof(ComponentCollectionPropertyAttribute), new ComponentCollectionPropertyMetaCreator());
      _propertyMetaCreatorFactory.RegisterPropertyMetaCreator(typeof(DateTimePropertyAttribute), new DateTimePropertyMetaCreator());
      _propertyMetaCreatorFactory.RegisterPropertyMetaCreator(typeof(ElementCollectionPropertyAttribute), new ElementCollectionPropertyMetaCreator());
      _propertyMetaCreatorFactory.RegisterPropertyMetaCreator(typeof(EnumPropertyAttribute), new EnumPropertyMetaCreator());
      _propertyMetaCreatorFactory.RegisterPropertyMetaCreator(typeof(GuidPropertyAttribute), new GuidPropertyMetaCreator());
      _propertyMetaCreatorFactory.RegisterPropertyMetaCreator(typeof(ManyToOnePropertyAttribute), new ManyToOnePropertyMetaCreator());
      _propertyMetaCreatorFactory.RegisterPropertyMetaCreator(typeof(ManyToManyPropertyAttribute), new ManyToManyPropertyMetaCreator());
      _propertyMetaCreatorFactory.RegisterPropertyMetaCreator(typeof(NumericPropertyAttribute), new NumericPropertyMetaCreator());
      _propertyMetaCreatorFactory.RegisterPropertyMetaCreator(typeof(ObjectIdPropertyAttribute), new ObjectIdPropertyMetaCreator());
      _propertyMetaCreatorFactory.RegisterPropertyMetaCreator(typeof(OneToManyPropertyAttribute), new OneToManyPropertyMetaCreator());
      _propertyMetaCreatorFactory.RegisterPropertyMetaCreator(typeof(OneToOnePropertyAttribute), new OneToOnePropertyMetaCreator());
      _propertyMetaCreatorFactory.RegisterPropertyMetaCreator(typeof(StringPropertyAttribute), new StringPropertyMetaCreator());
      _propertyMetaCreatorFactory.RegisterPropertyMetaCreator(typeof(VersionPropertyAttribute), new VersionPropertyMetaCreator());
      _propertyMetaCreatorFactory.RegisterPropertyMetaCreator(typeof(ArrayOfDoublesPropertyAttribute), new ArrayOfDoublesPropertyMetaCreator());
    }

    private void RegisterAuditHistoryWriterTypes()
    {
      //_snapshotWriterRegistry.RegisterSnapshotWriterType(typeof(AuditHistoryWriter),
      //                                                   typeof(ArrayOfDoublesSnapshot),
      //                                                   typeof(ArrayOfDoublesAuditHistoryTypeWriter));
      //_snapshotWriterRegistry.RegisterSnapshotWriterType(typeof(AuditHistoryWriter), typeof(BinaryBlobSnapshot), typeof(BinaryBlobAuditHistoryTypeWriter));
      //_snapshotWriterRegistry.RegisterSnapshotWriterType(typeof(AuditHistoryWriter), typeof(BooleanSnapshot), typeof(BooleanAuditHistoryTypeWriter));
      //_snapshotWriterRegistry.RegisterSnapshotWriterType(typeof(AuditHistoryWriter), typeof(ComponentKey), typeof(ComponentKeyAuditHistoryTypeWriter));
      //_snapshotWriterRegistry.RegisterSnapshotWriterType(typeof(AuditHistoryWriter), typeof(DateTimeSnapshot), typeof(DateTimeAuditHistoryTypeWriter));
      //_snapshotWriterRegistry.RegisterSnapshotWriterType(typeof(AuditHistoryWriter), typeof(DoubleSnapshot), typeof(DoubleAuditHistoryTypeWriter));
      //_snapshotWriterRegistry.RegisterSnapshotWriterType(typeof(AuditHistoryWriter), typeof(GuidSnapshot), typeof(GuidAuditHistoryTypeWriter));
      //_snapshotWriterRegistry.RegisterSnapshotWriterType(typeof(AuditHistoryWriter), typeof(IListSnapshot), typeof(ListAuditHistoryTypeWriter));
      //_snapshotWriterRegistry.RegisterSnapshotWriterType(typeof(AuditHistoryWriter), typeof(IMapSnapshot), typeof(MapAuditHistoryTypeWriter));
      //_snapshotWriterRegistry.RegisterSnapshotWriterType(typeof(AuditHistoryWriter), typeof(Int32Snapshot), typeof(Int32AuditHistoryTypeWriter));
      //_snapshotWriterRegistry.RegisterSnapshotWriterType(typeof(AuditHistoryWriter), typeof(Int64Snapshot), typeof(Int64AuditHistoryTypeWriter));
      //_snapshotWriterRegistry.RegisterSnapshotWriterType(typeof(AuditHistoryWriter), typeof(IScalarSnapshot), typeof(ScalarAuditHistoryTypeWriter));
      //_snapshotWriterRegistry.RegisterSnapshotWriterType(typeof(AuditHistoryWriter),
      //                                                   typeof(NullableDateTimeSnapshot),
      //                                                   typeof(NullableDateTimeAuditHistoryTypeWriter));
      //_snapshotWriterRegistry.RegisterSnapshotWriterType(typeof(AuditHistoryWriter), typeof(ISetSnapshot), typeof(SetAuditHistoryTypeWriter));
      //_snapshotWriterRegistry.RegisterSnapshotWriterType(typeof(AuditHistoryWriter),
      //                                                   typeof(NullableDoubleSnapshot),
      //                                                   typeof(NullableDoubleAuditHistoryTypeWriter));
      //_snapshotWriterRegistry.RegisterSnapshotWriterType(typeof(AuditHistoryWriter), typeof(NullableInt32Snapshot), typeof(NullableInt32AuditHistoryTypeWriter));
      //_snapshotWriterRegistry.RegisterSnapshotWriterType(typeof(AuditHistoryWriter), typeof(NullableInt64Snapshot), typeof(NullableInt64AuditHistoryTypeWriter));
      //_snapshotWriterRegistry.RegisterSnapshotWriterType(typeof(AuditHistoryWriter), typeof(ObjectIdSnapshot), typeof(ObjectIdAuditHistoryTypeWriter));
      //_snapshotWriterRegistry.RegisterSnapshotWriterType(typeof(AuditHistoryWriter), typeof(ObjectRefSnapshot), typeof(ObjectRefAuditHistoryTypeWriter));
      //_snapshotWriterRegistry.RegisterSnapshotWriterType(typeof(AuditHistoryWriter), typeof(ObjectSnapshot), typeof(ObjectAuditHistoryTypeWriter));
      //_snapshotWriterRegistry.RegisterSnapshotWriterType(typeof(AuditHistoryWriter), typeof(StringSnapshot), typeof(StringAuditHistoryTypeWriter));

      //_snapshotWriterRegistry.RegisterSnapshotDeltaWriterType(typeof(AuditHistoryWriter),
      //                                                        typeof(BagCollectionItemDelta),
      //                                                        typeof(BagCollectionItemAuditHistorySnapshotDeltaTypeWriter));
      //_snapshotWriterRegistry.RegisterSnapshotDeltaWriterType(typeof(AuditHistoryWriter),
      //                                                        typeof(ICollectionDelta),
      //                                                        typeof(CollectionAuditHistorySnapshotDeltaTypeWriter));
      //_snapshotWriterRegistry.RegisterSnapshotDeltaWriterType(typeof(AuditHistoryWriter),
      //                                                        typeof(IScalarDelta),
      //                                                        typeof(ScalarAuditHistorySnapshotDeltaTypeWriter));
      //_snapshotWriterRegistry.RegisterSnapshotDeltaWriterType(typeof(AuditHistoryWriter),
      //                                                        typeof(ListCollectionItemDelta),
      //                                                        typeof(ListCollectionItemAuditHistorySnapshotDeltaTypeWriter));
      //_snapshotWriterRegistry.RegisterSnapshotDeltaWriterType(typeof(AuditHistoryWriter),
      //                                                        typeof(MapCollectionItemDelta),
      //                                                        typeof(MapCollectionItemAuditHistorySnapshotDeltaTypeWriter));
      //_snapshotWriterRegistry.RegisterSnapshotDeltaWriterType(typeof(AuditHistoryWriter), typeof(ObjectDelta), typeof(ObjectAuditHistorySnapshotDeltaTypeWriter));
      //_snapshotWriterRegistry.RegisterSnapshotDeltaWriterType(typeof(AuditHistoryWriter),
      //                                                        typeof(SetCollectionItemDelta),
      //                                                        typeof(SetCollectionItemAuditHistorySnapshotDeltaTypeWriter));
    }


  }
}