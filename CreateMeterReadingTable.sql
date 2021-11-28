USE [EnsekTest]
GO

/****** Object:  Table [dbo].[MeterReading]    Script Date: 11/28/2021 6:29:21 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[MeterReading](
	[AccountId] [int] NOT NULL,
	[MeterReadingDateTime] [datetime] NOT NULL,
	[MeterReadValue] [int] NOT NULL
) ON [PRIMARY]
GO


