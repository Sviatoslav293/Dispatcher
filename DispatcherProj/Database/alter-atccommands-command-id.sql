IF OBJECT_ID(N'dbo.ATCCommands', N'U') IS NULL
BEGIN
	THROW 50001, 'Required table dbo.ATCCommands does not exist.', 1;
END;
GO

IF COL_LENGTH(N'dbo.ATCCommands', N'commandId') IS NULL
BEGIN
	ALTER TABLE dbo.ATCCommands
		ADD commandId uniqueidentifier NULL;
END;
GO

IF NOT EXISTS
(
	SELECT 1
	FROM sys.indexes
	WHERE object_id = OBJECT_ID(N'dbo.ATCCommands')
	  AND name = N'UX_ATCCommands_CommandId'
)
BEGIN
	CREATE UNIQUE NONCLUSTERED INDEX UX_ATCCommands_CommandId
		ON dbo.ATCCommands(commandId)
		WHERE commandId IS NOT NULL;
END;
GO
