/******* Script to create tables for NBatch ********/
/***================================= BatchJob ========================================***/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[BatchJob](
	[JobName] [nvarchar](100) NOT NULL,
	[CreateDate] [datetime] NOT NULL,
	[LastRun] [datetime] NOT NULL,
 CONSTRAINT [PK_BatchJob_1] PRIMARY KEY CLUSTERED 
(
	[JobName] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

ALTER TABLE [dbo].[BatchJob] ADD  CONSTRAINT [DF_BatchJob_CreateDate]  DEFAULT (sysutcdatetime()) FOR [CreateDate]
GO

/***================================= BatchStep ========================================***/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[BatchStep](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[StepName] [nvarchar](100) NOT NULL,
	[JobName] [nvarchar](100) NOT NULL,
	[StepIndex] [bigint] NOT NULL,
	[NumberOfItemsProcessed] [int] NOT NULL,
	[Error] [bit] NOT NULL CONSTRAINT [DF_BatchStep_ErrorSkipped]  DEFAULT ((0)),
	[Skipped] [bit] NOT NULL CONSTRAINT [DF_BatchStep_Done]  DEFAULT ((0)),
	[RunDate] [datetime] NOT NULL CONSTRAINT [DF_BatchStep_RunDate]  DEFAULT (sysutcdatetime()),
 CONSTRAINT [PK_BatchStep] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

ALTER TABLE [dbo].[BatchStep]  WITH CHECK ADD  CONSTRAINT [FK_BatchStep_BatchJob] FOREIGN KEY([JobName])
REFERENCES [dbo].[BatchJob] ([JobName])
GO

ALTER TABLE [dbo].[BatchStep] CHECK CONSTRAINT [FK_BatchStep_BatchJob]
GO


CREATE NONCLUSTERED INDEX [IX_StepName] ON [dbo].[BatchStep]
(
	[StepName] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO

/***================================= BatchStepException ========================================***/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[BatchStepException](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[StepIndex] [bigint] NOT NULL,
	[StepName] [nvarchar](100) NOT NULL,
	[JobName] [nvarchar](100) NOT NULL,
	[ExceptionMsg] [nvarchar](500) NULL,
	[ExceptionDetails] [nvarchar](1500) NULL,
	[CreateDate] [datetime] NOT NULL CONSTRAINT [DF_BatchStepException_CreateDate]  DEFAULT (sysutcdatetime()),
 CONSTRAINT [PK_BatchStepException] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

ALTER TABLE [dbo].[BatchStepException]  WITH CHECK ADD  CONSTRAINT [FK_BatchStepException_BatchJob] FOREIGN KEY([JobName])
REFERENCES [dbo].[BatchJob] ([JobName])
GO

ALTER TABLE [dbo].[BatchStepException] CHECK CONSTRAINT [FK_BatchStepException_BatchJob]
GO



CREATE NONCLUSTERED INDEX [IX_StepName] ON [dbo].[BatchStepException]
(
	[StepName] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO










