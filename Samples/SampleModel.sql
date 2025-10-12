IF DB_ID(N'EnterpriseBIPlatform') IS NULL
BEGIN
    CREATE DATABASE [EnterpriseBIPlatform];
END
GO
USE [EnterpriseBIPlatform];
GO

-- Table: Cube
CREATE TABLE [dbo].[Cube] (
    [Id] NVARCHAR(128) NOT NULL,
    [CubeName] NVARCHAR(256) NOT NULL,
    [Purpose] NVARCHAR(256) NULL,
    [RefreshMode] NVARCHAR(256) NULL,
    CONSTRAINT [PK_Cube] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

-- Table: Dimension
CREATE TABLE [dbo].[Dimension] (
    [Id] NVARCHAR(128) NOT NULL,
    [DimensionName] NVARCHAR(256) NOT NULL,
    [IsConformed] BIT NOT NULL,
    [HierarchyCount] NVARCHAR(256) NULL,
    CONSTRAINT [PK_Dimension] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

-- Table: Fact
CREATE TABLE [dbo].[Fact] (
    [Id] NVARCHAR(128) NOT NULL,
    [FactName] NVARCHAR(256) NOT NULL,
    [Grain] NVARCHAR(256) NULL,
    [MeasureCount] NVARCHAR(256) NULL,
    [BusinessArea] NVARCHAR(256) NULL,
    CONSTRAINT [PK_Fact] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

-- Table: Measure
CREATE TABLE [dbo].[Measure] (
    [Id] NVARCHAR(128) NOT NULL,
    [MeasureName] NVARCHAR(256) NOT NULL,
    [MDX] NVARCHAR(256) NULL,
    [CubeId] NVARCHAR(256) NOT NULL,
    [CubeId2] NVARCHAR(128) NOT NULL,
    CONSTRAINT [PK_Measure] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

-- Table: System
CREATE TABLE [dbo].[System] (
    [Id] NVARCHAR(128) NOT NULL,
    [SystemName] NVARCHAR(256) NOT NULL,
    [Version] NVARCHAR(256) NULL,
    [DeploymentDate] NVARCHAR(256) NULL,
    [SystemTypeId] NVARCHAR(256) NOT NULL,
    [SystemTypeId2] NVARCHAR(128) NOT NULL,
    CONSTRAINT [PK_System] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

-- Table: SystemCube
CREATE TABLE [dbo].[SystemCube] (
    [Id] NVARCHAR(128) NOT NULL,
    [ProcessingMode] NVARCHAR(256) NULL,
    [SystemId] NVARCHAR(256) NOT NULL,
    [CubeId] NVARCHAR(256) NOT NULL,
    [CubeId2] NVARCHAR(128) NOT NULL,
    [SystemId2] NVARCHAR(128) NOT NULL,
    CONSTRAINT [PK_SystemCube] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

-- Table: SystemDimension
CREATE TABLE [dbo].[SystemDimension] (
    [Id] NVARCHAR(128) NOT NULL,
    [ConformanceLevel] NVARCHAR(256) NULL,
    [SystemId] NVARCHAR(256) NOT NULL,
    [DimensionId] NVARCHAR(256) NOT NULL,
    [DimensionId2] NVARCHAR(128) NOT NULL,
    [SystemId2] NVARCHAR(128) NOT NULL,
    CONSTRAINT [PK_SystemDimension] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

-- Table: SystemFact
CREATE TABLE [dbo].[SystemFact] (
    [Id] NVARCHAR(128) NOT NULL,
    [LoadPattern] NVARCHAR(256) NULL,
    [SystemId] NVARCHAR(256) NOT NULL,
    [FactId] NVARCHAR(256) NOT NULL,
    [FactId2] NVARCHAR(128) NOT NULL,
    [SystemId2] NVARCHAR(128) NOT NULL,
    CONSTRAINT [PK_SystemFact] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

-- Table: SystemType
CREATE TABLE [dbo].[SystemType] (
    [Id] NVARCHAR(128) NOT NULL,
    [TypeName] NVARCHAR(256) NOT NULL,
    [Description] NVARCHAR(256) NULL,
    CONSTRAINT [PK_SystemType] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

-- Foreign keys
ALTER TABLE [dbo].[Measure] WITH CHECK ADD CONSTRAINT [FK_Measure_Cube] FOREIGN KEY([CubeId2]) REFERENCES [dbo].[Cube]([Id]);
GO

ALTER TABLE [dbo].[System] WITH CHECK ADD CONSTRAINT [FK_System_SystemType] FOREIGN KEY([SystemTypeId2]) REFERENCES [dbo].[SystemType]([Id]);
GO

ALTER TABLE [dbo].[SystemCube] WITH CHECK ADD CONSTRAINT [FK_SystemCube_Cube] FOREIGN KEY([CubeId2]) REFERENCES [dbo].[Cube]([Id]);
GO

ALTER TABLE [dbo].[SystemCube] WITH CHECK ADD CONSTRAINT [FK_SystemCube_System] FOREIGN KEY([SystemId2]) REFERENCES [dbo].[System]([Id]);
GO

ALTER TABLE [dbo].[SystemDimension] WITH CHECK ADD CONSTRAINT [FK_SystemDimension_Dimension] FOREIGN KEY([DimensionId2]) REFERENCES [dbo].[Dimension]([Id]);
GO

ALTER TABLE [dbo].[SystemDimension] WITH CHECK ADD CONSTRAINT [FK_SystemDimension_System] FOREIGN KEY([SystemId2]) REFERENCES [dbo].[System]([Id]);
GO

ALTER TABLE [dbo].[SystemFact] WITH CHECK ADD CONSTRAINT [FK_SystemFact_Fact] FOREIGN KEY([FactId2]) REFERENCES [dbo].[Fact]([Id]);
GO

ALTER TABLE [dbo].[SystemFact] WITH CHECK ADD CONSTRAINT [FK_SystemFact_System] FOREIGN KEY([SystemId2]) REFERENCES [dbo].[System]([Id]);
GO

