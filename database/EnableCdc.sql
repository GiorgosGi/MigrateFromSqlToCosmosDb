USE [TradeSource];
GO

EXEC sys.sp_cdc_enable_db;
GO

EXEC sys.sp_cdc_enable_table
	@source_schema = N'dbo',
	@source_name = N'Trades',
	@role_name = N'cdc_reader',
	@supports_net_changes = 0;
GO

EXEC sys.sp_cdc_change_job
	@job_type = N'cleanup',
	@retention = 4320;
GO

SELECT capture_instance, start_lsn, supports_net_changes
FROM cdc.change_tables
WHERE source_object_id = OBJECT_ID(N'dbo.Trades');
GO
