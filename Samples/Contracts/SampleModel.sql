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
    [IsConformed] NVARCHAR(256) NOT NULL,
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
    [CubeId] NVARCHAR(128) NOT NULL,
    CONSTRAINT [PK_Measure] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

-- Table: System
CREATE TABLE [dbo].[System] (
    [Id] NVARCHAR(128) NOT NULL,
    [SystemName] NVARCHAR(256) NOT NULL,
    [Version] NVARCHAR(256) NULL,
    [DeploymentDate] NVARCHAR(256) NULL,
    [SystemTypeId] NVARCHAR(128) NOT NULL,
    CONSTRAINT [PK_System] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

-- Table: SystemCube
CREATE TABLE [dbo].[SystemCube] (
    [Id] NVARCHAR(128) NOT NULL,
    [ProcessingMode] NVARCHAR(256) NULL,
    [CubeId] NVARCHAR(128) NOT NULL,
    [SystemId] NVARCHAR(128) NOT NULL,
    CONSTRAINT [PK_SystemCube] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

-- Table: SystemDimension
CREATE TABLE [dbo].[SystemDimension] (
    [Id] NVARCHAR(128) NOT NULL,
    [ConformanceLevel] NVARCHAR(256) NULL,
    [DimensionId] NVARCHAR(128) NOT NULL,
    [SystemId] NVARCHAR(128) NOT NULL,
    CONSTRAINT [PK_SystemDimension] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

-- Table: SystemFact
CREATE TABLE [dbo].[SystemFact] (
    [Id] NVARCHAR(128) NOT NULL,
    [LoadPattern] NVARCHAR(256) NULL,
    [FactId] NVARCHAR(128) NOT NULL,
    [SystemId] NVARCHAR(128) NOT NULL,
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
ALTER TABLE [dbo].[Measure] WITH CHECK ADD CONSTRAINT [FK_Measure_Cube_CubeId] FOREIGN KEY([CubeId]) REFERENCES [dbo].[Cube]([Id]);
GO

ALTER TABLE [dbo].[System] WITH CHECK ADD CONSTRAINT [FK_System_SystemType_SystemTypeId] FOREIGN KEY([SystemTypeId]) REFERENCES [dbo].[SystemType]([Id]);
GO

ALTER TABLE [dbo].[SystemCube] WITH CHECK ADD CONSTRAINT [FK_SystemCube_Cube_CubeId] FOREIGN KEY([CubeId]) REFERENCES [dbo].[Cube]([Id]);
GO

ALTER TABLE [dbo].[SystemCube] WITH CHECK ADD CONSTRAINT [FK_SystemCube_System_SystemId] FOREIGN KEY([SystemId]) REFERENCES [dbo].[System]([Id]);
GO

ALTER TABLE [dbo].[SystemDimension] WITH CHECK ADD CONSTRAINT [FK_SystemDimension_Dimension_DimensionId] FOREIGN KEY([DimensionId]) REFERENCES [dbo].[Dimension]([Id]);
GO

ALTER TABLE [dbo].[SystemDimension] WITH CHECK ADD CONSTRAINT [FK_SystemDimension_System_SystemId] FOREIGN KEY([SystemId]) REFERENCES [dbo].[System]([Id]);
GO

ALTER TABLE [dbo].[SystemFact] WITH CHECK ADD CONSTRAINT [FK_SystemFact_Fact_FactId] FOREIGN KEY([FactId]) REFERENCES [dbo].[Fact]([Id]);
GO

ALTER TABLE [dbo].[SystemFact] WITH CHECK ADD CONSTRAINT [FK_SystemFact_System_SystemId] FOREIGN KEY([SystemId]) REFERENCES [dbo].[System]([Id]);
GO

