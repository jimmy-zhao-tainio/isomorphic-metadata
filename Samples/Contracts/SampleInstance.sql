-- Data insertion script
INSERT INTO [dbo].[Cube] ([Id], [CubeName], [Purpose], [RefreshMode]) VALUES (N'1', N'Sales Performance', N'Monthly revenue and margin tracking.', N'Scheduled');
INSERT INTO [dbo].[Cube] ([Id], [CubeName], [Purpose], [RefreshMode]) VALUES (N'2', N'Finance Overview', N'Quarterly financial statements.', N'Manual');
INSERT INTO [dbo].[Dimension] ([Id], [DimensionName], [IsConformed], [HierarchyCount]) VALUES (N'1', N'Customer', N'True', N'2');
INSERT INTO [dbo].[Dimension] ([Id], [DimensionName], [IsConformed], [HierarchyCount]) VALUES (N'2', N'Product', N'False', N'1');
INSERT INTO [dbo].[Fact] ([Id], [FactName], [Grain], [MeasureCount], [BusinessArea]) VALUES (N'1', N'SalesFact', N'Order Line', N'12', N'');
INSERT INTO [dbo].[Measure] ([Id], [MeasureName], [MDX], [CubeId]) VALUES (N'1', N'number_of_things', N'count', N'1');
INSERT INTO [dbo].[SystemType] ([Id], [TypeName], [Description]) VALUES (N'1', N'Internal', N'Managed within the corporate data center.');
INSERT INTO [dbo].[SystemType] ([Id], [TypeName], [Description]) VALUES (N'2', N'External', N'Hosted by a third-party vendor.');
INSERT INTO [dbo].[System] ([Id], [SystemName], [Version], [DeploymentDate], [SystemTypeId]) VALUES (N'1', N'Enterprise Analytics Platform', N'2.1', N'2024-11-15', N'1');
INSERT INTO [dbo].[System] ([Id], [SystemName], [Version], [DeploymentDate], [SystemTypeId]) VALUES (N'2', N'Analytics Sandbox', N'1.4', N'2023-06-01', N'2');
INSERT INTO [dbo].[SystemCube] ([Id], [ProcessingMode], [CubeId], [SystemId]) VALUES (N'1', N'InMemory', N'1', N'1');
INSERT INTO [dbo].[SystemCube] ([Id], [ProcessingMode], [CubeId], [SystemId]) VALUES (N'2', N'DirectQuery', N'2', N'2');
INSERT INTO [dbo].[SystemDimension] ([Id], [ConformanceLevel], [DimensionId], [SystemId]) VALUES (N'1', N'Enterprise', N'1', N'1');
INSERT INTO [dbo].[SystemDimension] ([Id], [ConformanceLevel], [DimensionId], [SystemId]) VALUES (N'2', N'Sandbox', N'2', N'2');
INSERT INTO [dbo].[SystemFact] ([Id], [LoadPattern], [FactId], [SystemId]) VALUES (N'1', N'Incremental', N'1', N'1');
