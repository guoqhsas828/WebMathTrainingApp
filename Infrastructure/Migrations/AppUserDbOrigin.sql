IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

CREATE TABLE [Bill] (
    [BillId] int NOT NULL IDENTITY,
    [BillName] nvarchar(64) NULL,
    [GoodsReceivedNoteId] int NOT NULL,
    [VendorDONumber] nvarchar(900) NULL,
    [VendorInvoiceNumber] nvarchar(900) NULL,
    [BillDate] datetimeoffset NOT NULL,
    [BillDueDate] datetimeoffset NOT NULL,
    [BillTypeId] int NOT NULL,
    CONSTRAINT [PK_Bill] PRIMARY KEY ([BillId])
);

GO

CREATE TABLE [BillType] (
    [BillTypeId] int NOT NULL IDENTITY,
    [BillTypeName] nvarchar(900) NOT NULL,
    [Description] nvarchar(1024) NULL,
    CONSTRAINT [PK_BillType] PRIMARY KEY ([BillTypeId])
);

GO

CREATE TABLE [Branch] (
    [BranchId] int NOT NULL IDENTITY,
    [BranchName] nvarchar(64) NOT NULL,
    [Description] nvarchar(512) NULL,
    [CurrencyId] int NOT NULL,
    [Address] nvarchar(256) NULL,
    [City] nvarchar(128) NULL,
    [State] nvarchar(128) NULL,
    [ZipCode] nvarchar(32) NULL,
    [Phone] nvarchar(32) NULL,
    [Email] nvarchar(128) NULL,
    [ContactPerson] nvarchar(128) NULL,
    CONSTRAINT [PK_Branch] PRIMARY KEY ([BranchId])
);

GO

CREATE TABLE [CashBank] (
    [CashBankId] int NOT NULL IDENTITY,
    [CashBankName] nvarchar(64) NULL,
    [Description] nvarchar(1024) NULL,
    CONSTRAINT [PK_CashBank] PRIMARY KEY ([CashBankId])
);

GO

CREATE TABLE [CatalogBrand] (
    [CatalogBrandId] int NOT NULL IDENTITY,
    [Brand] nvarchar(128) NULL,
    [Description] nvarchar(1024) NULL,
    CONSTRAINT [PK_CatalogBrand] PRIMARY KEY ([CatalogBrandId])
);

GO

CREATE TABLE [Currency] (
    [CurrencyId] int NOT NULL IDENTITY,
    [CurrencyName] nvarchar(64) NOT NULL,
    [CurrencyCode] nvarchar(8) NOT NULL,
    [Description] nvarchar(1024) NULL,
    CONSTRAINT [PK_Currency] PRIMARY KEY ([CurrencyId])
);

GO

CREATE TABLE [Customer] (
    [CustomerId] int NOT NULL IDENTITY,
    [CustomerName] nvarchar(128) NOT NULL,
    [CustomerTypeId] int NOT NULL,
    [Address] nvarchar(256) NULL,
    [City] nvarchar(128) NULL,
    [State] nvarchar(128) NULL,
    [ZipCode] nvarchar(32) NULL,
    [Phone] nvarchar(32) NULL,
    [Email] nvarchar(128) NULL,
    [ContactPerson] nvarchar(128) NULL,
    CONSTRAINT [PK_Customer] PRIMARY KEY ([CustomerId])
);

GO

CREATE TABLE [CustomerType] (
    [CustomerTypeId] int NOT NULL IDENTITY,
    [CustomerTypeName] nvarchar(64) NOT NULL,
    [Description] nvarchar(1024) NULL,
    CONSTRAINT [PK_CustomerType] PRIMARY KEY ([CustomerTypeId])
);

GO

CREATE TABLE [GoodsReceivedNote] (
    [GoodsReceivedNoteId] int NOT NULL IDENTITY,
    [GoodsReceivedNoteName] nvarchar(128) NULL,
    [PurchaseOrderId] int NOT NULL,
    [GRNDate] datetimeoffset NOT NULL,
    [VendorDONumber] nvarchar(128) NULL,
    [VendorInvoiceNumber] nvarchar(128) NULL,
    [WarehouseId] int NOT NULL,
    [IsFullReceive] bit NOT NULL,
    CONSTRAINT [PK_GoodsReceivedNote] PRIMARY KEY ([GoodsReceivedNoteId])
);

GO

CREATE TABLE [Invoice] (
    [InvoiceId] int NOT NULL IDENTITY,
    [InvoiceName] nvarchar(128) NULL,
    [ShipmentId] int NOT NULL,
    [InvoiceDate] datetimeoffset NOT NULL,
    [InvoiceDueDate] datetimeoffset NOT NULL,
    [InvoiceTypeId] int NOT NULL,
    CONSTRAINT [PK_Invoice] PRIMARY KEY ([InvoiceId])
);

GO

CREATE TABLE [InvoiceType] (
    [InvoiceTypeId] int NOT NULL IDENTITY,
    [InvoiceTypeName] nvarchar(128) NOT NULL,
    [Description] nvarchar(1024) NULL,
    CONSTRAINT [PK_InvoiceType] PRIMARY KEY ([InvoiceTypeId])
);

GO

CREATE TABLE [NumberSequence] (
    [NumberSequenceId] int NOT NULL IDENTITY,
    [NumberSequenceName] nvarchar(128) NOT NULL,
    [Module] nvarchar(1024) NOT NULL,
    [Prefix] nvarchar(128) NOT NULL,
    [LastNumber] int NOT NULL,
    CONSTRAINT [PK_NumberSequence] PRIMARY KEY ([NumberSequenceId])
);

GO

CREATE TABLE [PaymentReceive] (
    [PaymentReceiveId] int NOT NULL IDENTITY,
    [PaymentReceiveName] nvarchar(128) NULL,
    [InvoiceId] int NOT NULL,
    [PaymentDate] datetimeoffset NOT NULL,
    [PaymentTypeId] int NOT NULL,
    [PaymentAmount] float NOT NULL,
    [IsFullPayment] bit NOT NULL,
    CONSTRAINT [PK_PaymentReceive] PRIMARY KEY ([PaymentReceiveId])
);

GO

CREATE TABLE [PaymentType] (
    [PaymentTypeId] int NOT NULL IDENTITY,
    [PaymentTypeName] nvarchar(128) NOT NULL,
    [Description] nvarchar(1024) NULL,
    CONSTRAINT [PK_PaymentType] PRIMARY KEY ([PaymentTypeId])
);

GO

CREATE TABLE [PaymentVoucher] (
    [PaymentVoucherId] int NOT NULL IDENTITY,
    [PaymentVoucherName] nvarchar(128) NULL,
    [BillId] int NOT NULL,
    [PaymentDate] datetimeoffset NOT NULL,
    [PaymentTypeId] int NOT NULL,
    [PaymentAmount] float NOT NULL,
    [CashBankId] int NOT NULL,
    [IsFullPayment] bit NOT NULL,
    CONSTRAINT [PK_PaymentVoucher] PRIMARY KEY ([PaymentVoucherId])
);

GO

CREATE TABLE [ProductType] (
    [ProductTypeId] int NOT NULL IDENTITY,
    [ProductTypeName] nvarchar(128) NOT NULL,
    [Description] nvarchar(1024) NULL,
    CONSTRAINT [PK_ProductType] PRIMARY KEY ([ProductTypeId])
);

GO

CREATE TABLE [PurchaseOrder] (
    [PurchaseOrderId] int NOT NULL IDENTITY,
    [PurchaseOrderName] nvarchar(128) NULL,
    [BranchId] int NOT NULL,
    [VendorId] int NOT NULL,
    [OrderDate] datetimeoffset NOT NULL,
    [DeliveryDate] datetimeoffset NOT NULL,
    [CurrencyId] int NOT NULL,
    [PurchaseTypeId] int NOT NULL,
    [Remarks] nvarchar(max) NULL,
    [Amount] float NOT NULL,
    [SubTotal] float NOT NULL,
    [Discount] float NOT NULL,
    [Tax] float NOT NULL,
    [Freight] float NOT NULL,
    [Total] float NOT NULL,
    CONSTRAINT [PK_PurchaseOrder] PRIMARY KEY ([PurchaseOrderId])
);

GO

CREATE TABLE [PurchaseType] (
    [PurchaseTypeId] int NOT NULL IDENTITY,
    [PurchaseTypeName] nvarchar(128) NOT NULL,
    [Description] nvarchar(1024) NULL,
    CONSTRAINT [PK_PurchaseType] PRIMARY KEY ([PurchaseTypeId])
);

GO

CREATE TABLE [SalesOrder] (
    [SalesOrderId] int NOT NULL IDENTITY,
    [SalesOrderName] nvarchar(128) NULL,
    [BranchId] int NOT NULL,
    [CustomerId] int NOT NULL,
    [OrderDate] datetimeoffset NOT NULL,
    [DeliveryDate] datetimeoffset NOT NULL,
    [CurrencyId] int NOT NULL,
    [CustomerRefNumber] nvarchar(128) NULL,
    [SalesTypeId] int NOT NULL,
    [Remarks] nvarchar(1024) NULL,
    [Amount] float NOT NULL,
    [SubTotal] float NOT NULL,
    [Discount] float NOT NULL,
    [Tax] float NOT NULL,
    [Freight] float NOT NULL,
    [Total] float NOT NULL,
    CONSTRAINT [PK_SalesOrder] PRIMARY KEY ([SalesOrderId])
);

GO

CREATE TABLE [SalesType] (
    [SalesTypeId] int NOT NULL IDENTITY,
    [SalesTypeName] nvarchar(128) NOT NULL,
    [Description] nvarchar(1024) NULL,
    CONSTRAINT [PK_SalesType] PRIMARY KEY ([SalesTypeId])
);

GO

CREATE TABLE [Shipment] (
    [ShipmentId] int NOT NULL IDENTITY,
    [ShipmentName] nvarchar(128) NULL,
    [SalesOrderId] int NOT NULL,
    [ShipmentDate] datetimeoffset NOT NULL,
    [ShipmentTypeId] int NOT NULL,
    [WarehouseId] int NOT NULL,
    [IsFullShipment] bit NOT NULL,
    CONSTRAINT [PK_Shipment] PRIMARY KEY ([ShipmentId])
);

GO

CREATE TABLE [ShipmentType] (
    [ShipmentTypeId] int NOT NULL IDENTITY,
    [ShipmentTypeName] nvarchar(128) NOT NULL,
    [Description] nvarchar(1024) NULL,
    CONSTRAINT [PK_ShipmentType] PRIMARY KEY ([ShipmentTypeId])
);

GO

CREATE TABLE [UnitOfMeasure] (
    [UnitOfMeasureId] int NOT NULL IDENTITY,
    [UnitOfMeasureName] nvarchar(128) NOT NULL,
    [Description] nvarchar(1024) NULL,
    CONSTRAINT [PK_UnitOfMeasure] PRIMARY KEY ([UnitOfMeasureId])
);

GO

CREATE TABLE [UserProfile] (
    [UserProfileId] int NOT NULL IDENTITY,
    [FirstName] nvarchar(128) NULL,
    [LastName] nvarchar(128) NULL,
    [Email] nvarchar(256) NULL,
    [Password] nvarchar(128) NULL,
    [ConfirmPassword] nvarchar(128) NULL,
    [OldPassword] nvarchar(128) NULL,
    [ProfilePicture] nvarchar(1024) NULL,
    [ApplicationUserId] nvarchar(900) NULL,
    [Continent] int NOT NULL,
    [ExperienceLevel] int NOT NULL,
    [UserStatus] int NOT NULL,
    [Created] datetime2 NOT NULL,
    [LastUpdated] datetime2 NOT NULL,
    [LatestLogin] datetime2 NOT NULL,
    [AchievedPoints] float NOT NULL,
    [AchievedLevel] int NOT NULL,
    CONSTRAINT [PK_UserProfile] PRIMARY KEY ([UserProfileId])
);

GO

CREATE TABLE [Vendor] (
    [VendorId] int NOT NULL IDENTITY,
    [VendorName] nvarchar(128) NOT NULL,
    [VendorTypeId] int NOT NULL,
    [Address] nvarchar(256) NULL,
    [City] nvarchar(128) NULL,
    [State] nvarchar(128) NULL,
    [ZipCode] nvarchar(32) NULL,
    [Phone] nvarchar(32) NULL,
    [Email] nvarchar(128) NULL,
    [ContactPerson] nvarchar(128) NULL,
    CONSTRAINT [PK_Vendor] PRIMARY KEY ([VendorId])
);

GO

CREATE TABLE [VendorType] (
    [VendorTypeId] int NOT NULL IDENTITY,
    [VendorTypeName] nvarchar(128) NOT NULL,
    [Description] nvarchar(1024) NULL,
    CONSTRAINT [PK_VendorType] PRIMARY KEY ([VendorTypeId])
);

GO

CREATE TABLE [Warehouse] (
    [WarehouseId] int NOT NULL IDENTITY,
    [WarehouseName] nvarchar(128) NOT NULL,
    [Description] nvarchar(1024) NULL,
    [BranchId] int NOT NULL,
    CONSTRAINT [PK_Warehouse] PRIMARY KEY ([WarehouseId])
);

GO


CREATE TABLE [Product] (
    [ProductId] int NOT NULL IDENTITY,
    [ProductName] nvarchar(128) NOT NULL,
    [ProductCode] nvarchar(128) NULL,
    [Barcode] nvarchar(128) NULL,
    [Description] nvarchar(1024) NULL,
    [ProductImageUrl] nvarchar(1024) NULL,
    [UnitOfMeasureId] int NOT NULL,
    [DefaultBuyingPrice] float NOT NULL,
    [DefaultSellingPrice] float NOT NULL,
    [BranchId] int NOT NULL,
    [CurrencyId] int NOT NULL,
    [ProductTypeId] int NOT NULL,
    [CatalogBrandId] int NOT NULL,
    CONSTRAINT [PK_Product] PRIMARY KEY ([ProductId]),
    CONSTRAINT [FK_Product_CatalogBrand_CatalogBrandId] FOREIGN KEY ([CatalogBrandId]) REFERENCES [CatalogBrand] ([CatalogBrandId]) ON DELETE CASCADE,
    CONSTRAINT [FK_Product_ProductType_ProductTypeId] FOREIGN KEY ([ProductTypeId]) REFERENCES [ProductType] ([ProductTypeId]) ON DELETE CASCADE
);

GO

CREATE TABLE [PurchaseOrderLine] (
    [PurchaseOrderLineId] int NOT NULL IDENTITY,
    [PurchaseOrderId] int NOT NULL,
    [ProductId] int NOT NULL,
    [Description] nvarchar(1024) NULL,
    [Quantity] float NOT NULL,
    [Price] float NOT NULL,
    [Amount] float NOT NULL,
    [DiscountPercentage] float NOT NULL,
    [DiscountAmount] float NOT NULL,
    [SubTotal] float NOT NULL,
    [TaxPercentage] float NOT NULL,
    [TaxAmount] float NOT NULL,
    [Total] float NOT NULL,
    CONSTRAINT [PK_PurchaseOrderLine] PRIMARY KEY ([PurchaseOrderLineId]),
    CONSTRAINT [FK_PurchaseOrderLine_PurchaseOrder_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [PurchaseOrder] ([PurchaseOrderId]) ON DELETE CASCADE
);

GO

CREATE TABLE [SalesOrderLine] (
    [SalesOrderLineId] int NOT NULL IDENTITY,
    [SalesOrderId] int NOT NULL,
    [ProductId] int NOT NULL,
    [Description] nvarchar(1024) NULL,
    [Quantity] float NOT NULL,
    [Price] float NOT NULL,
    [Amount] float NOT NULL,
    [DiscountPercentage] float NOT NULL,
    [DiscountAmount] float NOT NULL,
    [SubTotal] float NOT NULL,
    [TaxPercentage] float NOT NULL,
    [TaxAmount] float NOT NULL,
    [Total] float NOT NULL,
    CONSTRAINT [PK_SalesOrderLine] PRIMARY KEY ([SalesOrderLineId]),
    CONSTRAINT [FK_SalesOrderLine_Product_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Product] ([ProductId]) ON DELETE CASCADE,
    CONSTRAINT [FK_SalesOrderLine_SalesOrder_SalesOrderId] FOREIGN KEY ([SalesOrderId]) REFERENCES [SalesOrder] ([SalesOrderId]) ON DELETE CASCADE
);

GO

CREATE INDEX [IX_Product_CatalogBrandId] ON [Product] ([CatalogBrandId]);

GO

CREATE INDEX [IX_Product_ProductTypeId] ON [Product] ([ProductTypeId]);

GO

CREATE INDEX [IX_PurchaseOrderLine_PurchaseOrderId] ON [PurchaseOrderLine] ([PurchaseOrderId]);

GO

CREATE INDEX [IX_SalesOrderLine_ProductId] ON [SalesOrderLine] ([ProductId]);

GO

CREATE INDEX [IX_SalesOrderLine_SalesOrderId] ON [SalesOrderLine] ([SalesOrderId]);

GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20190625214920_InitAppUserDb', N'2.2.4-servicing-10062');

GO

