SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

SET ANSI_PADDING ON
GO

CREATE TABLE [dbo].[BatchStepException](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[StepName] [varchar](100) NOT NULL,
	[LineNumber] [int] NOT NULL,
	[ExceptionMsg] [varchar](500) NULL,
	[ExceptionDetails] [varchar](1500) NULL,
	[JobName] [varchar](100) NOT NULL,
	[CreateDate] [datetime] NOT NULL,
 CONSTRAINT [PK_BatchStepException] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

SET ANSI_PADDING OFF
GO

ALTER TABLE [dbo].[BatchStepException]  WITH CHECK ADD  CONSTRAINT [FK_BatchStepException_BatchJob] FOREIGN KEY([JobName])
REFERENCES [dbo].[BatchJob] ([JobName])
GO

ALTER TABLE [dbo].[BatchStepException] CHECK CONSTRAINT [FK_BatchStepException_BatchJob]
GO